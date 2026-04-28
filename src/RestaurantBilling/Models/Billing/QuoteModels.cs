using Entities.Enums;

namespace RestaurantBilling.Models.Billing;

public sealed record QuoteItemInput(
    int ItemId,
    string ItemName,
    decimal Qty,
    decimal Rate,
    decimal DiscountAmount,
    decimal TaxPercent,
    bool IsTaxInclusive,
    TaxType TaxType);

public sealed record CreateQuoteRequest(
    DateOnly BusinessDate,
    IReadOnlyCollection<QuoteItemInput> Items,
    decimal BillLevelDiscount);

public sealed record HoldBillRequest(
    BillType BillType,
    DateOnly BusinessDate,
    IReadOnlyCollection<QuoteItemInput> Items,
    decimal BillLevelDiscount,
    bool ServiceChargeOptIn,
    decimal ServiceChargeAmount,
    string? TableName = null,
    string? CustomerName = null,
    string? Phone = null);

public sealed record SettleExistingRequest(
    IReadOnlyCollection<SettleExistingPayment> Payments,
    string? CustomerName = null,
    string? Phone = null);

public sealed record UpdateDraftBillRequest(
    IReadOnlyCollection<QuoteItemInput> Items,
    decimal BillLevelDiscount,
    bool ServiceChargeOptIn,
    decimal ServiceChargeAmount,
    string? TableName = null,
    string? CustomerName = null,
    string? Phone = null);

public sealed record CancelDraftRequest();

public sealed record SettleExistingPayment(
    PaymentMode Mode,
    decimal Amount,
    string? ReferenceNo = null,
    string? CardLast4 = null,
    string? UpiTxnId = null);

public sealed record ReprintRequest(
    string DocumentType,
    long DocumentId,
    int UserId,
    string Reason,
    string ManagerPin);

