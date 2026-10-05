namespace Purch.Application.Onboarding;

public sealed record UpdateInventoryTrackingSettingRequest(bool UseSeparateInventoryTracking);

public sealed record UpdateDepartmentTrackingSettingRequest(bool UseDepartmentTracking);
