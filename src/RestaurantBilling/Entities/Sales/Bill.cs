using Entities.Common;
using Entities.Enums;

namespace Entities.Sales;

public class Bill : BaseEntity
{
    public long BillId { get; private set; }
    public string BillNo { get; private set; } = string.Empty;
    public DateTime BillDate { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public BillType BillType { get; private set; }
    public decimal SubTotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal ServiceCharge { get; private set; }
    public bool ServiceChargeOptIn { get; private set; }
    public decimal RoundOff { get; private set; }
    public decimal GrandTotal { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal BalanceAmount { get; private set; }
    public BillStatus Status { get; private set; } = BillStatus.Draft;
    public string? TableName { get; private set; }
    public string? CustomerName { get; private set; }
    public string? Phone { get; private set; }

    private readonly List<BillItem> _items = [];
    public IReadOnlyCollection<BillItem> Items => _items.AsReadOnly();

    private readonly List<Payment> _payments = [];
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    private Bill() { }

    public Bill(string billNo, DateOnly businessDate, BillType billType)
    {
        BillNo = billNo;
        BillDate = DateTime.UtcNow;
        BusinessDate = businessDate;
        BillType = billType;
    }

    public void AddItem(BillItem item)
    {
        _items.Add(item);
        Recalculate();
    }

    public void SetServiceCharge(decimal serviceCharge, bool optedIn)
    {
        ServiceChargeOptIn = optedIn;
        ServiceCharge = optedIn ? serviceCharge : 0m;
        Recalculate();
    }

    public void Settle(IEnumerable<Payment> payments)
    {
        foreach (var payment in payments)
        {
            _payments.Add(payment);
        }

        PaidAmount = _payments.Sum(x => x.Amount);
        BalanceAmount = GrandTotal - PaidAmount;
        Status = BalanceAmount <= 0 ? BillStatus.Paid : BillStatus.Partial;
        AddDomainEvent(new BillSettledEvent(BillId, BusinessDate));
    }

    public void SetTableName(string? tableName) => TableName = tableName;
    public void SetCustomerInfo(string? customerName, string? phone)
    {
        CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
    }

    public void Cancel()
    {
        Status = BillStatus.Cancelled;
    }

    private void Recalculate()
    {
        SubTotal = _items.Sum(x => x.RateSnapshot * x.Qty);
        DiscountAmount = _items.Sum(x => x.DiscountAmount);
        TaxAmount = _items.Sum(x => x.TaxAmount);
        var computedTotal = SubTotal - DiscountAmount + TaxAmount + ServiceCharge;
        RoundOff = Math.Round(computedTotal, MidpointRounding.AwayFromZero) - computedTotal;
        GrandTotal = computedTotal + RoundOff;
    }
}

