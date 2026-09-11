/// Mirrors Purch.Application.CreditLedger.CustomerCreditLedgerDto (B7).
class CustomerCreditLedger {
  const CustomerCreditLedger({
    required this.id,
    required this.customerFullName,
    required this.customerPhoneNumber,
    required this.customerAddress,
    required this.balance,
    required this.creditLimit,
    required this.dueDate,
    required this.isActive,
  });

  factory CustomerCreditLedger.fromJson(Map<String, dynamic> json) {
    return CustomerCreditLedger(
      id: json['id'] as String,
      customerFullName: json['customerFullName'] as String,
      customerPhoneNumber: json['customerPhoneNumber'] as String,
      customerAddress: json['customerAddress'] as String?,
      balance: (json['balance'] as num).toDouble(),
      creditLimit: (json['creditLimit'] as num).toDouble(),
      dueDate:
          json['dueDate'] == null
              ? null
              : DateTime.parse(json['dueDate'] as String),
      isActive: json['isActive'] as bool,
    );
  }

  final String id;
  final String customerFullName;
  final String customerPhoneNumber;
  final String? customerAddress;
  final double balance;
  final double creditLimit;
  final DateTime? dueDate;
  final bool isActive;

  double get availableCredit => creditLimit - balance;
}

/// Mirrors Purch.Application.CreditLedger.CreateCustomerCreditLedgerRequest.
class CreateCustomerCreditLedgerRequest {
  const CreateCustomerCreditLedgerRequest({
    required this.customerFullName,
    required this.customerPhoneNumber,
    this.customerAddress,
    required this.creditLimit,
    this.dueDate,
  });

  final String customerFullName;
  final String customerPhoneNumber;
  final String? customerAddress;
  final double creditLimit;
  final DateTime? dueDate;

  Map<String, dynamic> toJson() => {
    'customerFullName': customerFullName,
    'customerPhoneNumber': customerPhoneNumber,
    'customerAddress': customerAddress,
    'creditLimit': creditLimit,
    'dueDate': dueDate?.toIso8601String().split('T').first,
  };
}

/// Mirrors Purch.Application.CreditLedger.RecordCreditPaymentRequest — a
/// repayment against the balance. Charges only ever happen at POS checkout
/// via the Utang/Credit payment method.
class RecordCreditPaymentRequest {
  const RecordCreditPaymentRequest({required this.amount, this.note});

  final double amount;
  final String? note;

  Map<String, dynamic> toJson() => {'amount': amount, 'note': note};
}

/// Mirrors Purch.Application.CreditLedger.CreditReminderDto (B7's due-date
/// reminders) — no SMS/email vendor is chosen yet, so this is a queryable
/// list the app displays rather than a push notification.
class CreditReminder {
  const CreditReminder({
    required this.id,
    required this.customerFullName,
    required this.customerPhoneNumber,
    required this.balance,
    required this.dueDate,
    required this.isOverdue,
  });

  factory CreditReminder.fromJson(Map<String, dynamic> json) {
    return CreditReminder(
      id: json['id'] as String,
      customerFullName: json['customerFullName'] as String,
      customerPhoneNumber: json['customerPhoneNumber'] as String,
      balance: (json['balance'] as num).toDouble(),
      dueDate: DateTime.parse(json['dueDate'] as String),
      isOverdue: json['isOverdue'] as bool,
    );
  }

  final String id;
  final String customerFullName;
  final String customerPhoneNumber;
  final double balance;
  final DateTime dueDate;
  final bool isOverdue;
}
