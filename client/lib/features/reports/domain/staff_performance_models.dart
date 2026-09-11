/// Mirrors Purch.Application.Reporting.StaffPerformanceReportDto (F4).
class StaffPerformanceReport {
  const StaffPerformanceReport({
    required this.sales,
    required this.shiftAttendance,
  });

  factory StaffPerformanceReport.fromJson(Map<String, dynamic> json) {
    return StaffPerformanceReport(
      sales:
          (json['sales'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(StaffSalesSummary.fromJson)
              .toList(),
      shiftAttendance:
          (json['shiftAttendance'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(StaffShiftAttendance.fromJson)
              .toList(),
    );
  }

  final List<StaffSalesSummary> sales;
  final List<StaffShiftAttendance> shiftAttendance;
}

class StaffSalesSummary {
  const StaffSalesSummary({
    required this.staffUserId,
    required this.staffName,
    required this.transactionCount,
    required this.totalSales,
  });

  factory StaffSalesSummary.fromJson(Map<String, dynamic> json) {
    return StaffSalesSummary(
      staffUserId: json['staffUserId'] as String,
      staffName: json['staffName'] as String,
      transactionCount: json['transactionCount'] as int,
      totalSales: (json['totalSales'] as num).toDouble(),
    );
  }

  final String staffUserId;
  final String staffName;
  final int transactionCount;
  final double totalSales;
}

class StaffShiftAttendance {
  const StaffShiftAttendance({
    required this.staffUserId,
    required this.staffName,
    required this.shiftsOpened,
    required this.shiftsWithVariance,
  });

  factory StaffShiftAttendance.fromJson(Map<String, dynamic> json) {
    return StaffShiftAttendance(
      staffUserId: json['staffUserId'] as String,
      staffName: json['staffName'] as String,
      shiftsOpened: json['shiftsOpened'] as int,
      shiftsWithVariance: json['shiftsWithVariance'] as int,
    );
  }

  final String staffUserId;
  final String staffName;
  final int shiftsOpened;
  final int shiftsWithVariance;
}
