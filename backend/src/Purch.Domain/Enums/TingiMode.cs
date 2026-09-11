namespace Purch.Domain.Enums;

/// <summary>Governs sub-unit ("tingi") selling for weight/volume items only.</summary>
public enum TingiMode
{
    /// <summary>Sold only as a continuous freeform quantity — today's default weight/volume behavior.</summary>
    None,

    /// <summary>Sold as the whole pack plus an explicit, owner-configured list of allowed sizes.</summary>
    FixedSizes,

    /// <summary>Sold as the whole pack plus any whole multiple of a configured step, up to the pack size.</summary>
    Increment,
}
