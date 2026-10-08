namespace Purch.Application.Auth;

/// <summary>A signed note, made when a manager or admin signs in on a till, that this supervisor was working that till.</summary>
public sealed record SupervisorAttestation(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Lets the server trust "a manager rang this offline sale" without trusting the till. A sale made offline is paid before the
/// server hears of it and may sync later under a cashier's login, so the server cannot ask for the manager's PIN then. Instead,
/// when a supervisor signs in on a till (online), the server hands the till a short-lived signed attestation tied to that till and
/// business; the till attaches it to any offline discount sale it makes. A cashier cannot forge one, and one from another
/// till, another business or an earlier day does not verify.
///
/// What it proves: a supervisor signed in on this till within the last <see cref="Lifetime"/> before the sale. What it does not
/// prove: that the supervisor was standing at the till for that sale. It narrows who can authorise a discount to people holding a
/// supervisor's recent sign-in on the same device, which is as far as an offline device can be trusted.
/// </summary>
public interface ISupervisorAttestationService
{
    /// <summary>How long after the supervisor signs in an offline sale may rely on the attestation.</summary>
    static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    SupervisorAttestation Issue(Guid supervisorMembershipId, Guid tenantId, Guid deviceId, DateTimeOffset now);

    /// <summary>The supervisor's membership id if the token is genuine, is for this business and device, and the sale time falls
    /// inside its window; otherwise null.</summary>
    Guid? Validate(string token, Guid tenantId, Guid deviceId, DateTimeOffset saleTime);
}
