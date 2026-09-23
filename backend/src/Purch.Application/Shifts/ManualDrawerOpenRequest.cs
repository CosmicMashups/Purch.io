namespace Purch.Application.Shifts;

public sealed record ManualDrawerOpenRequest(string Reason, string? SupervisorPin = null);
