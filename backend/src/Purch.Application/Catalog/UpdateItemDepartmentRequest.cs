namespace Purch.Application.Catalog;

/// <summary>Null clears the assignment — an item doesn't have to belong to a department.</summary>
public sealed record UpdateItemDepartmentRequest(Guid? DepartmentId);
