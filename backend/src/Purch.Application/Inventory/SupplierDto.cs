namespace Purch.Application.Inventory;

public sealed record SupplierContactDto(
    string ContactPerson,
    IReadOnlyList<string> Modes,
    IReadOnlyList<string> Numbers,
    IReadOnlyList<string> Emails);

/// <summary>ContactInfo is a one-line summary of Contacts (or the old free text), kept so older clients still read it.</summary>
public sealed record SupplierDto(
    Guid Id,
    string Name,
    string? ContactInfo,
    bool IsActive,
    string? Specialization,
    string? Address,
    string? Tin,
    string? Remarks,
    IReadOnlyList<SupplierContactDto> Contacts,
    string? PaymentAccountName = null,
    string? PaymentAccountNumber = null);

public sealed record CreateSupplierRequest(
    string Name,
    string? ContactInfo = null,
    string? Specialization = null,
    string? Address = null,
    string? Tin = null,
    string? Remarks = null,
    IReadOnlyList<SupplierContactDto>? Contacts = null,
    string? PaymentAccountName = null,
    string? PaymentAccountNumber = null);

public sealed record UpdateSupplierRequest(
    string Name,
    string? Specialization,
    string? Address,
    string? Tin,
    string? Remarks,
    IReadOnlyList<SupplierContactDto> Contacts,
    bool IsActive = true,
    string? PaymentAccountName = null,
    string? PaymentAccountNumber = null);
