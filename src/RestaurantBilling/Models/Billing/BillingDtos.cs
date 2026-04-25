using Entities.Enums;

namespace Models.Billing;

public sealed record BillItemInput(int ItemId, string ItemName, decimal Qty, decimal Rate, decimal DiscountAmount, decimal TaxPercent, bool IsTaxInclusive, TaxType TaxType);

public sealed record BillItemComputed(
    int ItemId,
    string ItemName,
    decimal Qty,
    decimal Rate,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal LineTotal,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal StateVatAmount);

public sealed record BillCalculationResult(
    IReadOnlyCollection<BillItemComputed> Lines,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal ServiceCharge,
    decimal RoundOff,
    decimal GrandTotal);

