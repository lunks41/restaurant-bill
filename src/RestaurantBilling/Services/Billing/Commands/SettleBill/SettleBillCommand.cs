using MediatR;
using Helper;
using Entities.Enums;

namespace Services.Billing.Commands.SettleBill;

public sealed record SettleBillCommand(
    BillType BillType,
    DateOnly BusinessDate,
    bool IsInterState,
    decimal BillLevelDiscount,
    bool ServiceChargeOptIn,
    decimal ServiceChargeAmount,
    string? TableName,
    string? CustomerName,
    string? Phone,
    IReadOnlyCollection<SettleBillItemInput> Items,
    IReadOnlyCollection<SettleBillPaymentInput> Payments) : IRequest<Result<long>>;

public sealed record SettleBillItemInput(
    int ItemId,
    string ItemName,
    decimal Qty,
    decimal Rate,
    decimal DiscountAmount,
    decimal TaxPercent,
    bool IsTaxInclusive,
    TaxType TaxType,
    bool IsStockTracked);

public sealed record SettleBillPaymentInput(
    PaymentMode Mode,
    decimal Amount,
    string? ReferenceNo,
    string? CardLast4,
    string? UpiTxnId);

