import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/pos/domain/item_promo_models.dart';
import 'package:purch_client/features/pos/domain/pricing_engine.dart';
import 'package:purch_client/features/pos/domain/promo_code_models.dart';

// Mirrors backend/tests/Purch.UnitTests/Promotions/ItemPromoPricingCalculatorTests
// plus the cart-level Senior/PWD + promo-code layering of RecalculateTotalAsync,
// so the Dart and C# pricing can't drift apart unnoticed.
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

  test('Senior/PWD takes 20% of the post-item-promo subtotal', () {
    final result = _price([_line('1', _a, 2, 50)], senior: true);
    expect(result.seniorPwdDiscountAmount, 20);
    expect(result.discountAmount, 20);
    expect(result.totalAmount, 80);
  });

  test('promo code stacks after Senior/PWD and is capped by the remainder', () {
    final rules = PricingRules(
      promoCodes: [
        PromoCode(
          id: 'p',
          code: 'SAVE500',
          discountType: PromoDiscountType.fixedAmount,
          discountValue: 500,
          isActive: true,
          expiresAt: null,
        ),
        PromoCode(
          id: 'q',
          code: 'TEN',
          discountType: PromoDiscountType.percentage,
          discountValue: 10,
          isActive: true,
          expiresAt: null,
        ),
      ],
    );

    final capped = _price(
      [_line('1', _a, 1, 100)],
      rules: rules,
      senior: true,
      code: 'save500',
    );
    // 100 - senior 20 = 80 left; fixed 500 is capped to 80 -> total 0.
    expect(capped.promoDiscountAmount, 80);
    expect(capped.totalAmount, 0);
    expect(capped.appliedPromoCode, 'SAVE500');

    final percent = _price([_line('1', _a, 1, 200)], rules: rules, code: 'TEN');
    expect(percent.promoDiscountAmount, 20);
    expect(percent.totalAmount, 180);
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
