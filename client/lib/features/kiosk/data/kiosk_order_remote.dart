import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/failure_mapper.dart';
import '../../pos/domain/item_promo_models.dart';
import '../../pos/domain/pricing_engine.dart';
import '../../pos/domain/transaction_models.dart';
import '../domain/place_kiosk_order_request.dart';

/// The server half of the kiosk's local-first flow (see
/// LocalFirstKioskCartRepository): the cart is built and priced on-device,
/// and this is the one call that ever reaches the server — it builds, prices
/// and submits the whole order at once.
class KioskOrderRemote {
  KioskOrderRemote({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  /// The automatic item promotions (BOGO, combo, item discount) the kiosk
  /// totals its on-screen cart with. No promo codes: the kiosk never takes one.
  Future<PricingRules> loadPromoRules() async {
    try {
      final response = await _apiClient.dio.get<Map<String, dynamic>>(
        '/kiosk/promo-rules',
      );
      final data = response.data!;
      List<T> parse<T>(
        String key,
        T Function(Map<String, dynamic>) fromJson,
      ) => [
        for (final row in (data[key] as List<dynamic>? ?? const []))
          fromJson(row as Map<String, dynamic>),
      ];
      return PricingRules(
        bogo: parse('bogo', BogoPromoRule.fromJson),
        combo: parse('combos', ComboPromoRule.fromJson),
        itemDiscount: parse('itemDiscounts', ItemDiscountPromoRule.fromJson),
      );
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }

  Future<Transaction> placeOrder(PlaceKioskOrderRequest request) async {
    try {
      final response = await _apiClient.dio.post<Map<String, dynamic>>(
        '/kiosk/cart/place-order',
        data: request.toJson(),
      );
      return Transaction.fromJson(response.data!);
    } on DioException catch (exception) {
      throw mapDioExceptionToFailure(exception);
    }
  }
}
