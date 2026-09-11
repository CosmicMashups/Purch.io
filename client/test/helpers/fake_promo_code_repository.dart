import 'package:purch_client/features/pos/domain/promo_code_models.dart';
import 'package:purch_client/features/pos/domain/promo_code_repository.dart';

class FakePromoCodeRepository implements PromoCodeRepository {
  FakePromoCodeRepository({this.createFailure, List<PromoCode>? initialCodes})
    : promoCodes = initialCodes ?? [];

  final Object? createFailure;
  final List<PromoCode> promoCodes;

  CreatePromoCodeRequest? lastCreateRequest;

  @override
  Future<List<PromoCode>> listPromoCodes() async => promoCodes;

  @override
  Future<PromoCode> createPromoCode(CreatePromoCodeRequest request) async {
    lastCreateRequest = request;
    if (createFailure != null) {
      throw createFailure!;
    }
    final created = PromoCode(
      id: 'promo-${promoCodes.length + 1}',
      code: request.code,
      discountType: request.discountType,
      discountValue: request.discountValue,
      isActive: true,
      expiresAt: request.expiresAt,
    );
    promoCodes.add(created);
    return created;
  }
}
