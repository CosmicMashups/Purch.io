namespace Purch.Domain.Enums;

/// <summary>A person's standing inside one business. Staff are further limited by <see cref="StaffDuty"/>.</summary>
public enum MembershipRole
{
    Admin,
    Manager,
    Staff,
}
