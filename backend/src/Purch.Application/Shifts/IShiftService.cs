namespace Purch.Application.Shifts;

public interface IShiftService
{
    /// <summary>Null when the current device has no shift open right now.</summary>
    Task<ShiftDto?> GetCurrentShiftAsync(CancellationToken cancellationToken = default);

    Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, CancellationToken cancellationToken = default);

    Task<ShiftDto> CloseShiftAsync(CloseShiftRequest request, CancellationToken cancellationToken = default);

    Task<ShiftDto> RecordManualDrawerOpenAsync(ManualDrawerOpenRequest request, CancellationToken cancellationToken = default);
}
