import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/supplier_repository_impl.dart';
import '../../domain/supplier_models.dart';
import '../../domain/supplier_repository.dart';

part 'supplier_providers.g.dart';

@Riverpod(keepAlive: true)
SupplierRepository supplierRepository(Ref ref) {
  return SupplierRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

@riverpod
class SupplierList extends _$SupplierList {
  @override
  Future<List<Supplier>> build() {
    return ref.watch(supplierRepositoryProvider).listSuppliers();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class CreateSupplierController extends _$CreateSupplierController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreateSupplierRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(supplierRepositoryProvider);

    state = await AsyncValue.guard(() => repository.createSupplier(request));
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(supplierListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
