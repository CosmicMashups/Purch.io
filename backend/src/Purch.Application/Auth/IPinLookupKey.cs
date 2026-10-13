namespace Purch.Application.Auth;

/// <summary>A keyed, non-reversible fingerprint of a PIN within one business, stored beside the slow BCrypt hash so
/// that a typed approver PIN can be matched to the one person it may belong to with an indexed lookup instead of a
/// BCrypt check against every manager. It only narrows candidates: the BCrypt hash still decides.</summary>
public interface IPinLookupKey
{
    string Compute(Guid tenantId, string pin);
}
