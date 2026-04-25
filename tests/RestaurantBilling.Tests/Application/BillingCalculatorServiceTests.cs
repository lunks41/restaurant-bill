using Models.Billing;
using Services.Billing;
using Entities.Enums;

namespace RestaurantBilling.Application.Tests;

public class BillingCalculatorServiceTests
{
    [Fact]
    public void Calculate_IntraStateGst_SplitsIntoCgstAndSgst()
    {
        var service = new BillingCalculatorService();
        var input = new[]
        {
            new BillItemInput(1, "Paneer Tikka", 1, 100, 0, 5, false, TaxType.GST)
        };

        var result = service.Calculate(input, 0, 0, false, false);

        Assert.Equal(100m, result.SubTotal);
        Assert.Equal(5m, result.TaxAmount);
        Assert.Equal(105m, result.GrandTotal);
        Assert.Equal(2.5m, result.Lines.First().CgstAmount);
        Assert.Equal(2.5m, result.Lines.First().SgstAmount);
        Assert.Equal(0m, result.Lines.First().IgstAmount);
    }

    [Fact]
    public void Calculate_ServiceCharge_ExcludedFromTax()
    {
        var service = new BillingCalculatorService();
        var input = new[]
        {
            new BillItemInput(2, "Soup", 2, 100, 0, 5, false, TaxType.GST)
        };

        var result = service.Calculate(input, 0, 20, true, false);

        Assert.Equal(200m, result.SubTotal);
        Assert.Equal(10m, result.TaxAmount);
        Assert.Equal(20m, result.ServiceCharge);
        Assert.Equal(230m, result.GrandTotal);
    }
}
