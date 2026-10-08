import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/pos/domain/item_promo_models.dart';
import 'package:purch_client/features/pos/domain/pricing_engine.dart';
import 'package:purch_client/features/pos/domain/promo_code_models.dart';

// Runs shared/pricing-scenarios.json — the same cases the web's TypeScript engine and the backend's
// calculator run — so the copies of the pricing rules cannot drift apart unnoticed.
const _tolerance = 0.02;

PromoDiscountType _type(String name) =>
    PromoDiscountType.values.firstWhere((t) => t.name == name);

DateTime? _date(Object? value) =>
    value == null ? null : DateTime.parse(value as String);

PricingRules _rules(Map<String, dynamic>? json) {
  if (json == null) {
    return PricingRules.empty;
  }
  var n = 0;
  String id() => 'r${n++}';
  return PricingRules(
    bogo: [
      for (final r
          in (json['bogo'] as List? ?? []).cast<Map<String, dynamic>>())
        BogoPromoRule(
          id: id(),
          name: r['name'] as String,
          triggerItemId: r['triggerItemId'] as String,
          triggerQuantity: r['triggerQuantity'] as int,
          freeItemId: r['freeItemId'] as String,
          freeQuantity: r['freeQuantity'] as int,
          startsAt: _date(r['startsAt']),
          endsAt: _date(r['endsAt']),
          isActive: r['isActive'] as bool? ?? true,
        ),
    ],
    combo: [
      for (final r
          in (json['combo'] as List? ?? []).cast<Map<String, dynamic>>())
        ComboPromoRule(
          id: id(),
          name: r['name'] as String? ?? 'combo',
          itemAId: r['itemAId'] as String,
          itemBId: r['itemBId'] as String,
          comboPrice: (r['comboPrice'] as num).toDouble(),
          startsAt: _date(r['startsAt']),
          endsAt: _date(r['endsAt']),
          isActive: r['isActive'] as bool? ?? true,
        ),
    ],
    itemDiscount: [
      for (final r
          in (json['itemDiscount'] as List? ?? []).cast<Map<String, dynamic>>())
        ItemDiscountPromoRule(
          id: id(),
          name: 'rule',
          itemId: r['itemId'] as String,
          discountType: _type(r['discountType'] as String),
          discountValue: (r['discountValue'] as num).toDouble(),
          startsAt: _date(r['startsAt']),
          endsAt: _date(r['endsAt']),
          isActive: r['isActive'] as bool? ?? true,
        ),
    ],
    promoCodes: [
      for (final r
          in (json['promoCodes'] as List? ?? []).cast<Map<String, dynamic>>())
        PromoCode(
          id: id(),
          code: r['code'] as String,
          discountType: _type(r['discountType'] as String),
          discountValue: (r['discountValue'] as num).toDouble(),
          isActive: r['isActive'] as bool? ?? true,
          expiresAt: _date(r['expiresAt']),
        ),
    ],
  );
}

void main() {
  // `flutter test` runs from the client/ directory; the shared file lives one level up.
  final file = File('../shared/pricing-scenarios.json');
  final root = jsonDecode(file.readAsStringSync()) as Map<String, dynamic>;
  final defaultNow = root['now'] as String;

  for (final scenario
      in (root['scenarios'] as List).cast<Map<String, dynamic>>()) {
    test(scenario['name'] as String, () {
      final result = PricingEngine.price(
        lines: [
          for (final l
              in (scenario['lines'] as List).cast<Map<String, dynamic>>())
            PricingLineInput(
              lineId: l['lineId'] as String,
              itemId: l['itemId'] as String,
              quantity: (l['quantity'] as num).toDouble(),
              unitPrice: (l['unitPrice'] as num).toDouble(),
            ),
        ],
        rules: _rules(scenario['rules'] as Map<String, dynamic>?),
        seniorPwdApplied: scenario['seniorPwdApplied'] as bool? ?? false,
        promoCode: scenario['promoCode'] as String?,
        now: DateTime.parse((scenario['now'] as String?) ?? defaultNow),
      );

      final expected = scenario['expect'] as Map<String, dynamic>;
      final actual = <String, Object?>{
        'grossSubtotal': result.grossSubtotal,
        'itemPromoDiscountAmount': result.itemPromoDiscountAmount,
        'vatExemptAmount': result.vatExemptAmount,
        'seniorPwdDiscountAmount': result.seniorPwdDiscountAmount,
        'promoDiscountAmount': result.promoDiscountAmount,
        'discountAmount': result.discountAmount,
        'totalAmount': result.totalAmount,
        'appliedPromoCode': result.appliedPromoCode,
        'retainedPromoCode': result.retainedPromoCode,
        'promoCodeNotApplied': result.promoCodeNotApplied.name,
        'appliedPromoSide': result.appliedPromoSide.name,
        'seniorPwdSavings': result.seniorPwdSavings,
        'promoSavings': result.promoSavings,
      };

      expected.forEach((key, want) {
        if (key == 'lineDiscounts') {
          (want as Map<String, dynamic>).forEach((lineId, lineWant) {
            final line = result.lineDiscounts[lineId]!;
            final lw = lineWant as Map<String, dynamic>;
            if (lw.containsKey('discount')) {
              expect(
                line.discount,
                closeTo((lw['discount'] as num).toDouble(), _tolerance),
                reason: 'line $lineId discount',
              );
            }
            if (lw.containsKey('label')) {
              expect(line.label, lw['label'], reason: 'line $lineId label');
            }
          });
        } else if (want is num) {
          expect(
            actual[key] as double,
            closeTo(want.toDouble(), _tolerance),
            reason: key,
          );
        } else {
          expect(actual[key], want, reason: key);
        }
      });
    });
  }
}
