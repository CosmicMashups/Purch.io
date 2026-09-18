namespace Purch.Domain.Enums;

public enum MovementType
{
    StockIn,
    StockOut,
    Consumption,
    Spoiled,
    Damaged,
    ForReturn,
    Transfer,
    Adjustment,

    /// <summary>System-generated only, from a completed Cashier sale of a directly-tracked
    /// (non-recipe) item — never manually selectable when recording a movement by hand.</summary>
    Sale,
}
