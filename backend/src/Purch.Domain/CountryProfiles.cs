namespace Purch.Domain;

/// <summary>Country to currency and VAT rate. Only the Philippines ships today; add a row to support another country.</summary>
public static class CountryProfiles
{
    public sealed record Profile(string Name, string Currency, decimal VatRate);

    private static readonly Dictionary<string, Profile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PH"] = new("Philippines", "PHP", 0.12m),
    };

    public static IReadOnlyDictionary<string, Profile> All => Profiles;

    public static Profile Get(string? country) =>
        country is not null && Profiles.TryGetValue(country, out var profile) ? profile : Profiles["PH"];
}
