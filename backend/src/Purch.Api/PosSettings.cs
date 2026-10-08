using Purch.Application.Pos;

namespace Purch.Api;

/// <summary>Reads the POS switches from configuration. <c>POS_REQUIRE_EXPECTED_TOTAL</c> defaults to true; set it to false
/// only as a temporary grace period for app versions that do not send the total the customer was shown.</summary>
public sealed class PosSettings(IConfiguration configuration) : IPosSettings
{
    public bool RequireExpectedTotal { get; } = configuration.GetValue("POS_REQUIRE_EXPECTED_TOTAL", true);

    public bool AcceptUnattestedOfflineDiscounts { get; } = configuration.GetValue("POS_ACCEPT_UNATTESTED_OFFLINE_DISCOUNTS", false);
}
