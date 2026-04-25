using Entities.Enums;
using Entities.Sales;

namespace RestaurantBilling.Domain.Tests;

public class BillTests
{
    [Fact]
    public void Settle_SetsPaidStatus_WhenPaymentsCoverGrandTotal()
    {
        var bill = new Bill(1, "TI-2026-000001", new DateOnly(2026, 4, 24), BillType.Takeaway);
        bill.AddItem(new BillItem(1, "Naan", 1, 100, 0, 5));

        bill.Settle(new[] { new Payment(PaymentMode.Cash, 105) });

        Assert.Equal(BillStatus.Paid, bill.Status);
        Assert.True(bill.BalanceAmount <= 0);
    }
}
