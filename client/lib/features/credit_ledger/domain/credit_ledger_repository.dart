import 'credit_ledger_models.dart';

/// B7 — customer credit account (utang) management. Charging a sale to an
/// account happens through the POS's own payment flow, not here.
abstract class CreditLedgerRepository {
  Future<List<CustomerCreditLedger>> listLedgers();

  Future<CustomerCreditLedger> createLedger(
    CreateCustomerCreditLedgerRequest request,
  );

  Future<CustomerCreditLedger> recordPayment(
    String ledgerId,
    RecordCreditPaymentRequest request,
  );

  Future<List<CreditReminder>> listReminders({int withinDays = 7});
}
