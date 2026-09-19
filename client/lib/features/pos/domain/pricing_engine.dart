import 'item_promo_models.dart';
import 'promo_code_models.dart';

/// Dart port of the backend's cart pricing — `ItemPromoPricingCalculator` plus
/// `TransactionService.RecalculateTotalAsync` — so the Cashier can total a cart
/// locally, instantly, without a server round-trip per click.
///
/// This is an on-screen *estimate*: the server re-prices the whole cart with its
/// own copy of these rules when the sale is checked out, and its numbers are the
/// ones that get recorded. Keep the two in step — the shared cases in
/// test/unit/features/pos/pricing_engine_test.dart mirror the backend's tests.
class PricingEngine {
  const PricingEngine._();

  /// RA 9994/RA 10754 Senior Citizen/PWD discount rate (same as the backend).
  static const seniorPwdDiscountRate = 0.20;

  static PricingResult price({
    required List<PricingLineInput> lines,
    required PricingRules rules,
    required bool seniorPwdApplied,
    String? promoCode,
    DateTime? now,
  }) {
    final clock = now ?? DateTime.now();
    final promoResults = _calculateItemPromos(
      lines,
      rules.activeBogo(clock),
      rules.activeCombo(clock),
      rules.activeItemDiscount(clock),
    );

    final itemPromoTotal = promoResults.values.fold<double>(
      0,
      (sum, result) => sum + result.discount,
    );
    final grossTotal = lines.fold<double>(
      0,
      (sum, line) => sum + line.quantity * line.unitPrice,
    );
    final subtotal = grossTotal - itemPromoTotal;

    final seniorPwdAmount =
        seniorPwdApplied ? _round2(subtotal * seniorPwdDiscountRate) : 0.0;

    var promoAmount = 0.0;
    String? appliedCode;
    final trimmed = promoCode?.trim();
    if (trimmed != null && trimmed.isNotEmpty) {
      final promo = rules.findPromoCode(trimmed);
      if (promo != null && promo.isActive && !_isExpired(promo, clock)) {
        appliedCode = promo.code;
        final remainingAfterSenior = _max(subtotal - seniorPwdAmount, 0);
        promoAmount =
            promo.discountType == PromoDiscountType.percentage
                ? _round2(subtotal * promo.discountValue / 100)
                : promo.discountValue;
        promoAmount = _min(promoAmount, remainingAfterSenior);
      }
      // An unknown/expired code is dropped, exactly as the server does
      // mid-cart, instead of erroring on every line edit.
    }

    final discountAmount = seniorPwdAmount + promoAmount;
    return PricingResult(
      lineDiscounts: {
        for (final entry in promoResults.entries) entry.key: entry.value,
      },
      grossSubtotal: grossTotal,
      itemPromoDiscountAmount: itemPromoTotal,
      seniorPwdDiscountAmount: seniorPwdAmount,
      promoDiscountAmount: promoAmount,
      discountAmount: discountAmount,
      totalAmount: subtotal - discountAmount,
      appliedPromoCode: appliedCode,
    );
  }

  static bool _isExpired(PromoCode promo, DateTime now) =>
      promo.expiresAt != null && !promo.expiresAt!.isAfter(now);

  // -------------------------------------------------------------------------
  // Item promos: BOGO, then Combo, then Item discount. Each pass tracks how much
  // of each line's quantity has already been "claimed" so one unit is never
  // discounted by two rules.
  // -------------------------------------------------------------------------
  static Map<String, LineDiscount> _calculateItemPromos(
    List<PricingLineInput> lines,
    List<BogoPromoRule> bogoRules,
    List<ComboPromoRule> comboRules,
    List<ItemDiscountPromoRule> itemDiscountRules,
  ) {
    final claimed = {for (final line in lines) line.lineId: 0.0};
    final discount = {for (final line in lines) line.lineId: 0.0};
    final label = <String, String?>{for (final line in lines) line.lineId: null};

    double unclaimed(PricingLineInput line) =>
        line.quantity - claimed[line.lineId]!;

    // --- BOGO ---
    for (final rule in bogoRules) {
      final triggerQty = lines
          .where((l) => l.itemId == rule.triggerItemId)
          .fold<double>(0, (sum, l) => sum + l.quantity);

      int groups;
      double freeUnits;
      if (rule.triggerItemId == rule.freeItemId) {
        // The same units can't be both trigger and free.
        final groupSize = rule.triggerQuantity + rule.freeQuantity;
        groups = groupSize > 0 ? (triggerQty / groupSize).floor() : 0;
        freeUnits = (groups * rule.freeQuantity).toDouble();
      } else {
        groups =
            rule.triggerQuantity > 0
                ? (triggerQty / rule.triggerQuantity).floor()
                : 0;
        final freeAvailable = lines
            .where((l) => l.itemId == rule.freeItemId)
            .fold<double>(0, (sum, l) => sum + unclaimed(l));
        freeUnits = _min((groups * rule.freeQuantity).toDouble(), freeAvailable);
      }

      if (freeUnits <= 0) {
        continue;
      }

      var remaining = freeUnits;
      for (final freeLine in lines.where((l) => l.itemId == rule.freeItemId)) {
        if (remaining <= 0) {
          break;
        }
        final take = _min(remaining, unclaimed(freeLine));
        if (take <= 0) {
          continue;
        }
        claimed[freeLine.lineId] = claimed[freeLine.lineId]! + take;
        discount[freeLine.lineId] =
            discount[freeLine.lineId]! + take * freeLine.unitPrice;
        label[freeLine.lineId] =
            rule.name.trim().isEmpty ? 'BUY 1 TAKE 1' : rule.name;
        remaining -= take;
      }
    }

    // --- Combo ---
    for (final rule in comboRules) {
      final aLines = lines.where((l) => l.itemId == rule.itemAId).toList();
      final bLines = lines.where((l) => l.itemId == rule.itemBId).toList();
      final aAvailable = aLines.fold<double>(0, (s, l) => s + unclaimed(l));
      final bAvailable = bLines.fold<double>(0, (s, l) => s + unclaimed(l));
      final pairs = _min(aAvailable, bAvailable);
      if (pairs <= 0) {
        continue;
      }

      final unitPriceA = aLines.isNotEmpty ? aLines.first.unitPrice : 0.0;
      final unitPriceB = bLines.isNotEmpty ? bLines.first.unitPrice : 0.0;
      final normalTotal = pairs * (unitPriceA + unitPriceB);
      final comboTotal = pairs * rule.comboPrice;
      final totalDiscount = _max(normalTotal - comboTotal, 0);

      if (totalDiscount <= 0) {
        // Matched but doesn't reduce the price — still claim the units.
        _claimAcrossLines(aLines, pairs, claimed, unclaimed);
        _claimAcrossLines(bLines, pairs, claimed, unclaimed);
        continue;
      }

      final discountA =
          normalTotal > 0
              ? totalDiscount * (pairs * unitPriceA) / normalTotal
              : totalDiscount / 2;
      final discountB = totalDiscount - discountA;
      final comboLabel = 'COMBO ₱${rule.comboPrice.toStringAsFixed(2)}';
      _claimAcrossLines(
        aLines,
        pairs,
        claimed,
        unclaimed,
        totalDiscount: discountA,
        discountSink: discount,
        labelSink: label,
        labelText: comboLabel,
      );
      _claimAcrossLines(
        bLines,
        pairs,
        claimed,
        unclaimed,
        totalDiscount: discountB,
        discountSink: discount,
        labelSink: label,
        labelText: comboLabel,
      );
    }

    // --- Item discount ---
    for (final rule in itemDiscountRules) {
      for (final line in lines.where((l) => l.itemId == rule.itemId)) {
        final unclaimedQty = unclaimed(line);
        if (unclaimedQty <= 0) {
          continue;
        }
        final reduction = switch (rule.discountType) {
          PromoDiscountType.percentage =>
            line.unitPrice * rule.discountValue / 100,
          PromoDiscountType.fixedAmount => _min(
            rule.discountValue,
            line.unitPrice,
          ),
          PromoDiscountType.fixedPrice => _max(
            line.unitPrice - rule.discountValue,
            0,
          ),
        };
        if (reduction <= 0) {
          continue;
        }
        claimed[line.lineId] = claimed[line.lineId]! + unclaimedQty;
        discount[line.lineId] = discount[line.lineId]! + reduction * unclaimedQty;
        label[line.lineId] = switch (rule.discountType) {
          PromoDiscountType.percentage => '${_trimNumber(rule.discountValue)}% OFF',
          PromoDiscountType.fixedAmount =>
            '₱${rule.discountValue.toStringAsFixed(2)} OFF',
          PromoDiscountType.fixedPrice => 'SALE PRICE',
        };
      }
    }

    return {
      for (final line in lines)
        line.lineId: LineDiscount(
          discount: _round2(discount[line.lineId]!),
          label: label[line.lineId],
        ),
    };
  }

  static void _claimAcrossLines(
    List<PricingLineInput> orderedLines,
    double quantityToClaim,
    Map<String, double> claimed,
    double Function(PricingLineInput) unclaimed, {
    double? totalDiscount,
    Map<String, double>? discountSink,
    Map<String, String?>? labelSink,
    String? labelText,
  }) {
    var remainingQty = quantityToClaim;
    for (var i = 0; i < orderedLines.length && remainingQty > 0; i++) {
      final line = orderedLines[i];
      final take = _min(remainingQty, unclaimed(line));
      if (take <= 0) {
        continue;
      }
      claimed[line.lineId] = claimed[line.lineId]! + take;
      if (totalDiscount != null && discountSink != null) {
        final share =
            quantityToClaim > 0 ? totalDiscount * (take / quantityToClaim) : 0.0;
        discountSink[line.lineId] = discountSink[line.lineId]! + share;
        if (labelSink != null) {
          labelSink[line.lineId] = labelText;
        }
      }
      remainingQty -= take;
    }
  }

  /// Rounds to 2 decimals, halves to even — C#'s `Math.Round(decimal, 2)`
  /// default, which is what the server uses for these amounts.
  static double _round2(double value) {
    final scaled = value * 100;
    final floor = scaled.floorToDouble();
    final diff = scaled - floor;
    double rounded;
    if ((diff - 0.5).abs() < 1e-9) {
      rounded = floor % 2 == 0 ? floor : floor + 1;
    } else {
      rounded = scaled.roundToDouble();
    }
    return rounded / 100;
  }

  static String _trimNumber(double value) =>
      value == value.roundToDouble()
          ? value.toStringAsFixed(0)
          : value.toString();

  static double _min(double a, double b) => a < b ? a : b;
  static double _max(double a, double b) => a > b ? a : b;
}

/// One cart line as the engine sees it. [lineId] only has to be unique within
/// the cart and stable in the order lines were added.
class PricingLineInput {
  const PricingLineInput({
    required this.lineId,
    required this.itemId,
    required this.quantity,
    required this.unitPrice,
  });

  final String lineId;
  final String itemId;
  final double quantity;
  final double unitPrice;
}

class LineDiscount {
  const LineDiscount({required this.discount, required this.label});

  final double discount;
  final String? label;
}

class PricingResult {
  const PricingResult({
    required this.lineDiscounts,
    required this.grossSubtotal,
    required this.itemPromoDiscountAmount,
    required this.seniorPwdDiscountAmount,
    required this.promoDiscountAmount,
    required this.discountAmount,
    required this.totalAmount,
    required this.appliedPromoCode,
  });

  final Map<String, LineDiscount> lineDiscounts;

  /// Sum of line totals before any discount (the receipt's "subtotal").
  final double grossSubtotal;
  final double itemPromoDiscountAmount;
  final double seniorPwdDiscountAmount;
  final double promoDiscountAmount;

  /// Senior/PWD + promo-code discount (item promos are separate, as on the server).
  final double discountAmount;
  final double totalAmount;

  /// The promo code that actually applied — null when it was unknown/expired.
  final String? appliedPromoCode;
}

/// The automatic promo rules and promo codes a cart is priced against.
class PricingRules {
  const PricingRules({
    this.bogo = const [],
    this.combo = const [],
    this.itemDiscount = const [],
    this.promoCodes = const [],
  });

  static const empty = PricingRules();

  final List<BogoPromoRule> bogo;
  final List<ComboPromoRule> combo;
  final List<ItemDiscountPromoRule> itemDiscount;
  final List<PromoCode> promoCodes;

  List<BogoPromoRule> activeBogo(DateTime now) =>
      bogo.where((r) => _active(r.isActive, r.startsAt, r.endsAt, now)).toList();

  List<ComboPromoRule> activeCombo(DateTime now) =>
      combo.where((r) => _active(r.isActive, r.startsAt, r.endsAt, now)).toList();

  List<ItemDiscountPromoRule> activeItemDiscount(DateTime now) =>
      itemDiscount
          .where((r) => _active(r.isActive, r.startsAt, r.endsAt, now))
          .toList();

  PromoCode? findPromoCode(String code) {
    for (final promo in promoCodes) {
      if (promo.code.toLowerCase() == code.toLowerCase()) {
        return promo;
      }
    }
    return null;
  }

  static bool _active(
    bool isActive,
    DateTime? startsAt,
    DateTime? endsAt,
    DateTime now,
  ) =>
      isActive &&
      (startsAt == null || !startsAt.isAfter(now)) &&
      (endsAt == null || !endsAt.isBefore(now));
}
