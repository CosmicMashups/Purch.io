namespace Purch.Domain.Enums;

/// <summary>What an inventory CSV import carries: catalog fields, or stock counts.</summary>
public enum ImportKind
{
    Catalog = 0,
    Stock = 1,
}
