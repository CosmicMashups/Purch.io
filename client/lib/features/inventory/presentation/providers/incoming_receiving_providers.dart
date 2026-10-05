import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/data/data_refresh.dart';
import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/incoming_receiving_repository_impl.dart';
import '../../domain/incoming_receiving_models.dart';
import '../../domain/incoming_receiving_repository.dart';
import 'purchase_order_providers.dart';

part 'incoming_receiving_providers.g.dart';

@Riverpod(keepAlive: true)
IncomingReceivingRepository incomingReceivingRepository(Ref ref) {
  return IncomingReceivingRepositoryImpl(
    apiClient: ref.watch(apiClientProvider),
  );
}

@riverpod
class IncomingReceivingList extends _$IncomingReceivingList {
  @override
  Future<List<IncomingReceiving>> build() {
    return ref.watch(incomingReceivingRepositoryProvider).listReports();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class IncomingReceivingController extends _$IncomingReceivingController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreateIncomingReceivingRequest request) => _act(
    (repository) => repository.createReport(request),
  );

  Future<bool> link(String reportId, String purchaseOrderId) => _act(
    (repository) => repository.linkPurchaseOrder(reportId, purchaseOrderId),
  );

  Future<bool> _act(
    Future<IncomingReceiving> Function(IncomingReceivingRepository) action,
  ) async {
    state = const AsyncLoading();
    final repository = ref.read(incomingReceivingRepositoryProvider);

    state = await AsyncValue.guard(() => action(repository));
    final succeeded = !state.hasError;
    if (succeeded) {
      // Accepted lines add stock and move the linked purchase order's status.
      refreshStockAndSalesData(ref);
      await ref.read(incomingReceivingListProvider.notifier).refresh();
      await ref.read(purchaseOrderListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
