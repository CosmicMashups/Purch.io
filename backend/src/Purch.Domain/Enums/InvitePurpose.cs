namespace Purch.Domain.Enums;

public enum InvitePurpose
{
    /// <summary>A new staff member opens the link to create their account and set a password and PIN.</summary>
    Enrolment,

    /// <summary>An existing staff member opens the link to set a new password (no email is sent).</summary>
    PasswordReset,
}
