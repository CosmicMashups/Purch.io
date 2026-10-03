using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Devices;

/// <summary>What a customer display receives. State is null until the Register has published anything (the display shows its
/// welcome screen). NotModified means the display already has this version.</summary>
public sealed record CustomerDisplayFeed(long Version, DateTimeOffset? UpdatedAt, JsonElement? State, bool NotModified);

public interface ICustomerDisplayRepository
{
    Task<CustomerDisplayState?> GetByRegisterAsync(Guid registerDeviceId, CancellationToken cancellationToken = default);

    /// <summary>Whether an active customer display is paired to this Register: nothing is stored for a Register nobody watches.</summary>
    Task<bool> HasActiveDisplayAsync(Guid registerDeviceId, CancellationToken cancellationToken = default);

    void Add(CustomerDisplayState state);
}

public interface ICustomerDisplayService
{
    /// <summary>A Register (a person has unlocked it) says what its customer display should show.</summary>
    Task PublishAsync(JsonElement state, CancellationToken cancellationToken = default);

    /// <summary>A customer display asks for what its Register is showing. <paramref name="knownVersion"/> is the one it has.</summary>
    Task<CustomerDisplayFeed> GetAsync(long? knownVersion, CancellationToken cancellationToken = default);
}

public sealed class CustomerDisplayService(
    ICustomerDisplayRepository repository,
    IDeviceRepository deviceRepository,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : ICustomerDisplayService
{
    /// <summary>A cart of a few dozen lines is a few kilobytes; this keeps a bug or a hostile client from filling the table.</summary>
    public const int MaxStateBytes = 64 * 1024;

    private static readonly HashSet<string> Modes = ["idle", "cart", "payment", "completed"];

    public async Task PublishAsync(JsonElement state, CancellationToken cancellationToken = default)
    {
        var registerId = currentActorProvider.DeviceId
            ?? throw new ForbiddenException("Only a Register that someone has unlocked can update its customer display.");
        var register = await deviceRepository.GetByIdAsync(registerId, cancellationToken);
        if (register is not { DeviceType: DeviceType.Register, Status: DeviceStatus.Active })
        {
            throw new ForbiddenException("Only a Register that someone has unlocked can update its customer display.");
        }

        var json = state.GetRawText();
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxStateBytes)
        {
            throw new ValidationException(nameof(state), "The order is too large for the customer display.");
        }

        if (state.ValueKind != JsonValueKind.Object || !state.TryGetProperty("mode", out var mode) || mode.ValueKind != JsonValueKind.String || !Modes.Contains(mode.GetString()!))
        {
            throw new ValidationException(nameof(state), "The customer display needs a mode of idle, cart, payment or completed.");
        }

        // Nothing is stored for a Register nobody is watching, so a till without a customer display costs nothing.
        if (!await repository.HasActiveDisplayAsync(registerId, cancellationToken))
        {
            return;
        }

        var existing = await repository.GetByRegisterAsync(registerId, cancellationToken);
        if (existing is null)
        {
            repository.Add(new CustomerDisplayState { TenantId = register.TenantId, RegisterDeviceId = registerId, StateJson = json, Version = 1 });
        }
        else
        {
            existing.StateJson = json;
            existing.Version++;
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<CustomerDisplayFeed> GetAsync(long? knownVersion, CancellationToken cancellationToken = default)
    {
        var displayId = currentActorProvider.DeviceId
            ?? throw new ForbiddenException("Only a paired customer display can read this.");
        var display = await deviceRepository.GetByIdAsync(displayId, cancellationToken);
        if (display is not { DeviceType: DeviceType.CustomerDisplay, Status: DeviceStatus.Active, LinkedRegisterDeviceId: { } registerId })
        {
            throw new ForbiddenException("Only a paired customer display can read this.");
        }

        var stored = await repository.GetByRegisterAsync(registerId, cancellationToken);
        if (stored is null)
        {
            return new CustomerDisplayFeed(0, null, null, knownVersion == 0);
        }

        if (knownVersion == stored.Version)
        {
            return new CustomerDisplayFeed(stored.Version, stored.UpdatedAt, null, true);
        }

        using var document = JsonDocument.Parse(stored.StateJson);
        return new CustomerDisplayFeed(stored.Version, stored.UpdatedAt, document.RootElement.Clone(), false);
    }
}
