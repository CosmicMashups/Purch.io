import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/pos/domain/item_promo_models.dart';
import 'package:purch_client/features/pos/domain/pricing_engine.dart';
import 'package:purch_client/features/pos/domain/promo_code_models.dart';

// Mirrors backend/tests/Purch.UnitTests/Promotions/ItemPromoPricingCalculatorTests
// plus the cart-level rule in RecalculateTotalAsync — Senior/PWD, a promo code and
// item promos never stack — so the Dart and C# pricing can't drift apart unnoticed.
const _a = 'item-a';
const _b = 'item-b';

PricingLineInput _line(String id, String item, double qty, double price) =>
    PricingLineInput(lineId: id, itemId: item, quantity: qty, unitPrice: price);

BogoPromoRule _bogo(String name, String trigger, int tq, String free, int fq) =>
    BogoPromoRule(
      id: 'r-$name',
      name: name,
      triggerItemId: trigger,
      triggerQuantity: tq,
      freeItemId: free,
      freeQuantity: fq,
      startsAt: null,
      endsAt: null,
      isActive: true,
    );

ItemDiscountPromoRule _discount(PromoDiscountType type, double value) =>
    ItemDiscountPromoRule(
      id: 'd',
      name: 'd',
      itemId: _a,
      discountType: type,
      discountValue: value,
      startsAt: null,
      endsAt: null,
      isActive: true,
    );

PricingResult _price(
  List<PricingLineInput> lines, {
  PricingRules rules = PricingRules.empty,
  bool senior = false,
  String? code,
}) => PricingEngine.price(
  lines: lines,
  rules: rules,
  seniorPwdApplied: senior,
  promoCode: code,
);

void main() {
  test('no active rules yields no discounts', () {
    final result = _price([_line('1', _a, 4, 25)]);
    expect(result.itemPromoDiscountAmount, 0);
    expect(result.lineDiscounts['1']!.label, isNull);
    expect(result.totalAmount, 100);
  });

  test('same-item BOGO spans multiple lines, earliest first', () {
    final result = _price(
      [_line('1', _a, 3, 10), _line('2', _a, 2, 10)],
      rules: PricingRules(bogo: [_bogo('B1T1', _a, 1, _a, 1)]),
    );
    expect(result.itemPromoDiscountAmount, 20);
    expect(result.lineDiscounts['1']!.discount, 20);
    expect(result.lineDiscounts['1']!.label, 'B1T1');
    expect(result.lineDiscounts['2']!.discount, 0);
  });

  test('different-item BOGO is capped by the free item quantity', () {
    final result = _price(
      [_line('1', _a, 10, 5), _line('2', _b, 3, 8)],
      rules: PricingRules(bogo: [_bogo('A2 get B1', _a, 2, _b, 1)]),
    );
    expect(result.lineDiscounts['2']!.discount, 24);
    expect(result.lineDiscounts['1']!.discount, 0);
  });

  test('combo pairs only matched units, splitting the discount proportionally', () {
    final result = _price(
      [_line('1', _a, 3, 50), _line('2', _b, 5, 40)],
      rules: PricingRules(
        combo: [
          ComboPromoRule(
            id: 'c',
            name: 'Combo',
            itemAId: _a,
            itemBId: _b,
            comboPrice: 85,
            startsAt: null,
            endsAt: null,
            isActive: true,
          ),
        ],
      ),
    );
    expect(result.itemPromoDiscountAmount, closeTo(15, 0.02));
    expect(result.lineDiscounts['1']!.discount, closeTo(8.33, 0.01));
    expect(result.lineDiscounts['2']!.discount, closeTo(6.67, 0.01));
    expect(result.lineDiscounts['1']!.label, contains('COMBO'));
  });

  test('a unit claimed by BOGO is not discounted again by an item discount', () {
    final result = _price(
      [_line('t', 'trigger', 1, 20), _line('1', _a, 1, 30)],
      rules: PricingRules(
        bogo: [_bogo('B1T1', 'trigger', 1, _a, 1)],
        itemDiscount: [_discount(PromoDiscountType.percentage, 20)],
      ),
    );
    expect(result.lineDiscounts['1']!.discount, 30);
    expect(result.lineDiscounts['1']!.label, 'B1T1');
  });

  test('item discount types: percentage, fixed amount, fixed price', () {
    double discountFor(PromoDiscountType type, double value) =>
        _price(
          [_line('1', _a, 1, 100)],
          rules: PricingRules(itemDiscount: [_discount(type, value)]),
        ).itemPromoDiscountAmount;

    expect(discountFor(PromoDiscountType.percentage, 20), 20);
    expect(discountFor(PromoDiscountType.fixedAmount, 15), 15);
    expect(discountFor(PromoDiscountType.fixedPrice, 70), 30);
  });

  test('inactive or out-of-window rules are ignored', () {
    final past = DateTime.now().subtract(const Duration(days: 2));
    final expired = BogoPromoRule(
      id: 'x',
      name: 'old',
      triggerItemId: _a,
      triggerQuantity: 1,
      freeItemId: _a,
      freeQuantity: 1,
      startsAt: past,
      endsAt: past.add(const Duration(days: 1)),
      isActive: true,
    );
    final result = _price(
      [_line('1', _a, 2, 10)],
      rules: PricingRules(bogo: [expired]),
    );
    expect(result.itemPromoDiscountAmount, 0);
  });

  PromoCode codeOf(String code, PromoDiscountType type, double value) => PromoCode(
    id: code,
    code: code,
    discountType: type,
    discountValue: value,
    isActive: true,
    expiresAt: null,
  );

  ItemDiscountPromoRule itemDiscountOn(String item, PromoDiscountType type, double value) =>
      ItemDiscountPromoRule(
        id: 'd-$item',
        name: 'promo',
        itemId: item,
        discountType: type,
        discountValue: value,
        startsAt: null,
        endsAt: null,
        isActive: true,
      );

  group('discounts do not stack (RA 9994)', () {
    test('Senior/PWD takes 20% of the regular price', () {
      final result = _price([_line('1', _a, 2, 50)], senior: true);
      expect(result.seniorPwdDiscountAmount, 20);
      expect(result.discountAmount, 20);
      expect(result.totalAmount, 80);
    });

    test('Senior/PWD is taken on the REGULAR price and suppresses item promos', () {
      final rules = PricingRules(
        itemDiscount: [itemDiscountOn(_a, PromoDiscountType.percentage, 50)],
      );

      final withPromo = _price([_line('1', _a, 1, 100)], rules: rules);
      expect(withPromo.itemPromoDiscountAmount, 50);
      expect(withPromo.totalAmount, 50);

      final senior = _price([_line('1', _a, 1, 100)], rules: rules, senior: true);
      // 20% of 100, not of the promo price of 50; the promo is gone entirely.
      expect(senior.seniorPwdDiscountAmount, 20);
      expect(senior.itemPromoDiscountAmount, 0);
      expect(senior.lineDiscounts['1']!.discount, 0);
      expect(senior.lineDiscounts['1']!.label, isNull);
      expect(senior.totalAmount, 80);
    });

    test('a promo code does not stack with Senior/PWD, but stays on the cart', () {
      final rules = PricingRules(
        promoCodes: [codeOf('SAVE10', PromoDiscountType.percentage, 10)],
      );

      final senior = _price([_line('1', _a, 1, 100)], rules: rules, senior: true, code: 'save10');
      expect(senior.seniorPwdDiscountAmount, 20);
      expect(senior.promoDiscountAmount, 0);
      expect(senior.appliedPromoCode, isNull);
      expect(senior.retainedPromoCode, 'SAVE10');
      expect(senior.promoCodeNotApplied, PromoCodeNotApplied.suppressedBySeniorPwd);
      expect(senior.totalAmount, 80);

      // Switching Senior/PWD back off restores the code.
      final off = _price([_line('1', _a, 1, 100)], rules: rules, code: 'save10');
      expect(off.promoDiscountAmount, 10);
      expect(off.appliedPromoCode, 'SAVE10');
      expect(off.promoCodeNotApplied, PromoCodeNotApplied.none);
      expect(off.totalAmount, 90);
    });

    test('a promo code is computed on the regular subtotal and capped by it', () {
      final rules = PricingRules(
        promoCodes: [
          codeOf('SAVE500', PromoDiscountType.fixedAmount, 500),
          codeOf('TEN', PromoDiscountType.percentage, 10),
        ],
      );

      final capped = _price([_line('1', _a, 1, 100)], rules: rules, code: 'SAVE500');
      expect(capped.promoDiscountAmount, 100);
      expect(capped.totalAmount, 0);

      final percent = _price([_line('1', _a, 1, 200)], rules: rules, code: 'TEN');
      expect(percent.promoDiscountAmount, 20);
      expect(percent.totalAmount, 180);
    });

    test('a promo code and item promos do not stack: the larger one applies', () {
      // Item promo: 10 off. A code worth 30 beats it.
      final rules = PricingRules(
        itemDiscount: [itemDiscountOn(_a, PromoDiscountType.fixedAmount, 10)],
        promoCodes: [
          codeOf('CODE30', PromoDiscountType.fixedAmount, 30),
          codeOf('CODE5', PromoDiscountType.fixedAmount, 5),
          codeOf('CODE10', PromoDiscountType.fixedAmount, 10),
        ],
      );
      final cart = [_line('1', _a, 1, 100)];

      final codeWins = _price(cart, rules: rules, code: 'CODE30');
      expect(codeWins.promoDiscountAmount, 30);
      expect(codeWins.itemPromoDiscountAmount, 0);
      expect(codeWins.lineDiscounts['1']!.discount, 0);
      expect(codeWins.appliedPromoSide, PromoSide.promoCode);
      expect(codeWins.totalAmount, 70);

      final itemWins = _price(cart, rules: rules, code: 'CODE5');
      expect(itemWins.itemPromoDiscountAmount, 10);
      expect(itemWins.promoDiscountAmount, 0);
      expect(itemWins.appliedPromoSide, PromoSide.itemPromos);
      expect(itemWins.retainedPromoCode, 'CODE5');
      expect(itemWins.promoCodeNotApplied, PromoCodeNotApplied.supersededByItemPromos);
      expect(itemWins.totalAmount, 90);

      // A tie goes to the automatic item promo.
      final tie = _price(cart, rules: rules, code: 'CODE10');
      expect(tie.itemPromoDiscountAmount, 10);
      expect(tie.promoDiscountAmount, 0);
      expect(tie.totalAmount, 90);
    });

    test('reports what each option would save, so the cashier can let the customer choose', () {
      final rules = PricingRules(
        itemDiscount: [itemDiscountOn(_a, PromoDiscountType.percentage, 50)],
      );

      final promosApplied = _price([_line('1', _a, 1, 100)], rules: rules);
      expect(promosApplied.seniorPwdSavings, 20);
      expect(promosApplied.promoSavings, 50);

      // Both figures are still reported once Senior/PWD is chosen.
      final seniorApplied = _price([_line('1', _a, 1, 100)], rules: rules, senior: true);
      expect(seniorApplied.seniorPwdSavings, 20);
      expect(seniorApplied.promoSavings, 50);
    });

    test('a lone item promo behaves exactly as before when Senior/PWD is off', () {
      final rules = PricingRules(bogo: [_bogo('B1T1', _a, 1, _a, 1)]);
      final result = _price([_line('1', _a, 2, 50)], rules: rules);
      expect(result.itemPromoDiscountAmount, 50);
      expect(result.appliedPromoSide, PromoSide.itemPromos);
      expect(result.totalAmount, 50);
    });
  });

  test('unknown or expired promo codes are dropped, not errors', () {
    final rules = PricingRules(
      promoCodes: [
        PromoCode(
          id: 'p',
          code: 'OLD',
          discountType: PromoDiscountType.fixedAmount,
          discountValue: 10,
          isActive: true,
          expiresAt: DateTime.now().subtract(const Duration(hours: 1)),
        ),
      ],
    );
    final expired = _price([_line('1', _a, 1, 50)], rules: rules, code: 'OLD');
    expect(expired.promoDiscountAmount, 0);
    expect(expired.appliedPromoCode, isNull);
    expect(_price([_line('1', _a, 1, 50)], code: 'NOPE').totalAmount, 50);
  });

  test('rounding halves to even like the server', () {
    // 0.625 * 20% = 0.125, exactly on a half-cent boundary -> 0.12.
    final result = _price([_line('1', _a, 1, 0.625)], senior: true);
    expect(result.seniorPwdDiscountAmount, 0.12);
  });
}
