import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/credit_ledger/domain/credit_ledger_models.dart';
import 'package:purch_client/features/credit_ledger/domain/credit_ledger_repository.dart';

class FakeCreditLedgerRepository implements CreditLedgerRepository {
  FakeCreditLedgerRepository({
    List<CustomerCreditLedger>? ledgers,
    List<CreditReminder>? reminders,
    this.failureToThrow,
  }) : ledgers = ledgers ?? [],
       reminders = reminders ?? [];

  List<CustomerCreditLedger> ledgers;
  List<CreditReminder> reminders;
  final Failure? failureToThrow;
  CreateCustomerCreditLedgerRequest? lastCreateRequest;
  RecordCreditPaymentRequest? lastPaymentRequest;

  @override
  Future<List<CustomerCreditLedger>> listLedgers() async {
    if (failureToThrow != null) {
      throw failureToThrow!;
    }
    return ledgers;
  }

  @override
  Future<CustomerCreditLedger> createLedger(
    CreateCustomerCreditLedgerRequest request,
  ) async {
    lastCreateRequest = request;
    if (failureToThrow != null) {
      throw failureToThrow!;
    }
    final created = CustomerCreditLedger(
      id: 'ledger-${ledgers.length + 1}',
      customerFullName: request.customerFullName,
      customerPhoneNumber: request.customerPhoneNumber,
      customerAddress: request.customerAddress,
      balance: 0,
      creditLimit: request.creditLimit,
      dueDate: request.dueDate,
      isActive: true,
    );
    ledgers = [...ledgers, created];
    return created;
  }

  @override
  Future<CustomerCreditLedger> recordPayment(
    String ledgerId,
    RecordCreditPaymentRequest request,
  ) async {
    lastPaymentRequest = request;
    if (failureToThrow != null) {
      throw failureToThrow!;
    }
    final index = ledgers.indexWhere((l) => l.id == ledgerId);
    final updated = CustomerCreditLedger(
      id: ledgers[index].id,
      customerFullName: ledgers[index].customerFullName,
      customerPhoneNumber: ledgers[index].customerPhoneNumber,
      customerAddress: ledgers[index].customerAddress,
      balance: ledgers[index].balance - request.amount,
      creditLimit: ledgers[index].creditLimit,
      dueDate: ledgers[index].dueDate,
      isActive: ledgers[index].isActive,
    );
    ledgers = [
      for (final l in ledgers)
        if (l.id == ledgerId) updated else l,
    ];
    return updated;
  }

  @override
  Future<List<CreditReminder>> listReminders({int withinDays = 7}) async {
    if (failureToThrow != null) {
      throw failureToThrow!;
    }
    return reminders;
  }
}
