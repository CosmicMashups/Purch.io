namespace Purch.Application.Reporting;

/// <summary>B6's split sales-attribution report — one row per department that
/// had any sales in range, plus a "General" row for items with no department
/// assigned (Item.DepartmentId is optional; not every item belongs to a
/// concessionaire).</summary>
public sealed record DepartmentSalesSummaryDto(Guid? DepartmentId, string DepartmentName, decimal Revenue);
