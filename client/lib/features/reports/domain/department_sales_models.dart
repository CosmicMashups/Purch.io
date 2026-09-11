/// Mirrors Purch.Application.Reporting.DepartmentSalesSummaryDto (B6's split
/// sales-attribution report). A null departmentId means "General" — items
/// with no department assigned.
class DepartmentSalesSummary {
  const DepartmentSalesSummary({
    required this.departmentId,
    required this.departmentName,
    required this.revenue,
  });

  factory DepartmentSalesSummary.fromJson(Map<String, dynamic> json) {
    return DepartmentSalesSummary(
      departmentId: json['departmentId'] as String?,
      departmentName: json['departmentName'] as String,
      revenue: (json['revenue'] as num).toDouble(),
    );
  }

  final String? departmentId;
  final String departmentName;
  final double revenue;
}
