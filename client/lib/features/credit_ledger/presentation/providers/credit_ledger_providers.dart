import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/credit_ledger_repository_impl.dart';
import '../../domain/credit_ledger_models.dart';
import '../../domain/credit_ledger_repository.dart';

part 'credit_ledger_providers.g.dart';

@Riverpod(keepAlive: true)
CreditLedgerRepository creditLedgerRepository(Ref ref) {
  return CreditLedgerRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

@riverpod
class CreditLedgerList extends _$CreditLedgerList {
  @override
  Future<List<CustomerCreditLedger>> build() {
    return ref.watch(creditLedgerRepositoryProvider).listLedgers();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
Future<List<CreditReminder>> creditReminders(Ref ref, {int withinDays = 7}) {
  return ref
      .watch(creditLedgerRepositoryProvider)
      .listReminders(withinDays: withinDays);
}

@riverpod
class CreateCreditLedgerController extends _$CreateCreditLedgerController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreateCustomerCreditLedgerRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(creditLedgerRepositoryProvider);

    state = await AsyncValue.guard(() => repository.createLedger(request));
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(creditLedgerListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

@riverpod
class RecordCreditPaymentController extends _$RecordCreditPaymentController {
  @override
  FutureOr<void> build() {}

  Future<bool> recordPayment(
    String ledgerId,
    RecordCreditPaymentRequest request,
  ) async {
    state = const AsyncLoading();
    final repository = ref.read(creditLedgerRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.recordPayment(ledgerId, request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(creditLedgerListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
