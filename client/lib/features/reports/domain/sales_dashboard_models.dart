/// Mirrors Purch.Application.Reporting.SalesDashboardDto (F1) — rolling
/// windows ("today"/"last 7 days"/"last 30 days"), not calendar week/month
/// boundaries, same pragmatic simplification the backend documents.
class SalesDashboard {
  const SalesDashboard({
    required this.revenueToday,
    required this.revenueLast7Days,
    required this.revenueLast30Days,
    required this.trend,
    required this.topSellingItems,
    required this.branchComparison,
  });

  factory SalesDashboard.fromJson(Map<String, dynamic> json) {
    return SalesDashboard(
      revenueToday: (json['revenueToday'] as num).toDouble(),
      revenueLast7Days: (json['revenueLast7Days'] as num).toDouble(),
      revenueLast30Days: (json['revenueLast30Days'] as num).toDouble(),
      trend:
          (json['trend'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(DailyRevenuePoint.fromJson)
              .toList(),
      topSellingItems:
          (json['topSellingItems'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(TopSellingItem.fromJson)
              .toList(),
      branchComparison:
          (json['branchComparison'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(BranchRevenue.fromJson)
              .toList(),
    );
  }

  final double revenueToday;
  final double revenueLast7Days;
  final double revenueLast30Days;
  final List<DailyRevenuePoint> trend;
  final List<TopSellingItem> topSellingItems;
  final List<BranchRevenue> branchComparison;
}

class DailyRevenuePoint {
  const DailyRevenuePoint({required this.date, required this.revenue});

  factory DailyRevenuePoint.fromJson(Map<String, dynamic> json) {
    return DailyRevenuePoint(
      date: DateTime.parse(json['date'] as String),
      revenue: (json['revenue'] as num).toDouble(),
    );
  }

  final DateTime date;
  final double revenue;
}

class TopSellingItem {
  const TopSellingItem({
    required this.itemId,
    required this.itemName,
    required this.quantitySold,
    required this.revenue,
  });

  factory TopSellingItem.fromJson(Map<String, dynamic> json) {
    return TopSellingItem(
      itemId: json['itemId'] as String,
      itemName: json['itemName'] as String,
      quantitySold: (json['quantitySold'] as num).toDouble(),
      revenue: (json['revenue'] as num).toDouble(),
    );
  }

  final String itemId;
  final String itemName;
  final double quantitySold;
  final double revenue;
}

class BranchRevenue {
  const BranchRevenue({
    required this.branchId,
    required this.branchName,
    required this.revenue,
  });

  factory BranchRevenue.fromJson(Map<String, dynamic> json) {
    return BranchRevenue(
      branchId: json['branchId'] as String,
      branchName: json['branchName'] as String,
      revenue: (json['revenue'] as num).toDouble(),
    );
  }

  final String branchId;
  final String branchName;
  final double revenue;
}
