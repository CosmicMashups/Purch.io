import 'promo_code_models.dart';

/// Admin/Manager-facing promo code management — separate from PosRepository,
/// mirroring the backend's separate Purch.Application.Promotions namespace.
abstract class PromoCodeRepository {
  Future<List<PromoCode>> listPromoCodes();

  Future<PromoCode> createPromoCode(CreatePromoCodeRequest request);
}
