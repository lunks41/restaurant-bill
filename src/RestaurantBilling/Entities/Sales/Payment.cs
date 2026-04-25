using Entities.Common;
using Entities.Enums;

namespace Entities.Sales;

public class Payment : BaseEntity
{
    public long PaymentId { get; private set; }
    public long BillId { get; private set; }
    public PaymentMode PaymentMode { get; private set; }
    public decimal Amount { get; private set; }
    public string? ReferenceNo { get; private set; }
    public string? CardLast4 { get; private set; }
    public string? UpiTxnId { get; private set; }

    private Payment() { }

    public Payment(PaymentMode paymentMode, decimal amount, string? referenceNo = null, string? cardLast4 = null, string? upiTxnId = null)
    {
        PaymentMode = paymentMode;
        Amount = amount;
        ReferenceNo = referenceNo;
        CardLast4 = cardLast4;
        UpiTxnId = upiTxnId;
    }
}

