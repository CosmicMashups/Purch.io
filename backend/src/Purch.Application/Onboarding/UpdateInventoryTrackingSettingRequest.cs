namespace Purch.Application.Onboarding;

public sealed record UpdateInventoryTrackingSettingRequest(bool UseSeparateInventoryTracking, bool? UpdateCostOnReceiving = null);

public sealed record UpdateDepartmentTrackingSettingRequest(bool UseDepartmentTracking);
