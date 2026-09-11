namespace Purch.Application.Inventory;

public sealed record SupplierDto(Guid Id, string Name, string? ContactInfo, bool IsActive);

public sealed record CreateSupplierRequest(string Name, string? ContactInfo);
