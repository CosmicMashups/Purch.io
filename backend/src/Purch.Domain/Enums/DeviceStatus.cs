namespace Purch.Domain.Enums;

/// <summary>Active is first so that devices created before this field existed read as Active.</summary>
public enum DeviceStatus
{
    /// <summary>Paired and allowed to sign in.</summary>
    Active,

    /// <summary>Created by an Admin; waiting for its one-time pairing code to be entered on the device.</summary>
    Pending,

    /// <summary>Taken out of service. Its credential and sessions no longer work.</summary>
    Revoked,
}
