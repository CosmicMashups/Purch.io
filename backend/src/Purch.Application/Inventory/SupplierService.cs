using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

public sealed class SupplierService(
    ISupplierRepository supplierRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : ISupplierService
{
    private static readonly string[] AllowedModes = ["Call", "Viber", "Email", "Facebook", "Messenger", "Landline", "Others"];

    public async Task<IReadOnlyList<SupplierDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var suppliers = await supplierRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return [.. suppliers.OrderBy(supplier => supplier.Name).Select(ToDto)];
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Supplier name is required.");
        }

        var supplier = new Supplier
        {
            TenantId = CurrentTenantId,
            Name = request.Name.Trim(),
            ContactInfo = Clean(request.ContactInfo),
            Specialization = Clean(request.Specialization),
            Address = Clean(request.Address),
            Tin = Clean(request.Tin),
            Remarks = Clean(request.Remarks),
            Contacts = CleanContacts(request.Contacts),
            IsActive = true,
        };

        supplierRepository.Add(supplier);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(supplier);
    }

    public async Task<SupplierDto> UpdateAsync(Guid supplierId, UpdateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Supplier name is required.");
        }

        var supplier = await supplierRepository.GetByIdAsync(supplierId, cancellationToken)
            ?? throw new NotFoundException("Supplier", supplierId);

        supplier.Name = request.Name.Trim();
        supplier.Specialization = Clean(request.Specialization);
        supplier.Address = Clean(request.Address);
        supplier.Tin = Clean(request.Tin);
        supplier.Remarks = Clean(request.Remarks);
        supplier.Contacts = CleanContacts(request.Contacts);
        supplier.IsActive = request.IsActive;
        // The old free-text line is replaced by the structured contacts.
        supplier.ContactInfo = null;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(supplier);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Supplier management requires an authenticated tenant context.");

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static List<string> CleanList(IEnumerable<string>? values)
    {
        return [.. (values ?? []).Select(value => value.Trim()).Where(value => value.Length > 0).Distinct()];
    }

    private static List<SupplierContact> CleanContacts(IReadOnlyList<SupplierContactDto>? contacts)
    {
        var result = new List<SupplierContact>();
        foreach (var contact in contacts ?? [])
        {
            var modes = CleanList(contact.Modes);
            if (modes.Any(mode => !AllowedModes.Contains(mode)))
            {
                throw new ValidationException(nameof(contact.Modes), "Unknown mode of contact.");
            }

            var cleaned = new SupplierContact
            {
                ContactPerson = contact.ContactPerson?.Trim() ?? string.Empty,
                Modes = modes,
                Numbers = CleanList(contact.Numbers),
                Emails = CleanList(contact.Emails),
            };

            if (cleaned.ContactPerson.Length == 0 && cleaned.Numbers.Count == 0 && cleaned.Emails.Count == 0)
            {
                continue;
            }

            result.Add(cleaned);
        }
        return result;
    }

    private static SupplierDto ToDto(Supplier supplier)
    {
        var contacts = supplier.Contacts
            .Select(contact => new SupplierContactDto(contact.ContactPerson, contact.Modes, contact.Numbers, contact.Emails))
            .ToList();

        var summary = supplier.Contacts.Count == 0
            ? supplier.ContactInfo
            : string.Join(" · ", supplier.Contacts.Select(contact =>
                string.Join(" ", new[] { contact.ContactPerson }.Concat(contact.Numbers.Take(1)).Concat(contact.Emails.Take(1)).Where(part => part.Length > 0))));

        return new(supplier.Id, supplier.Name, summary, supplier.IsActive, supplier.Specialization, supplier.Address, supplier.Tin, supplier.Remarks, contacts);
    }
}
