using Models.Billing;
using IServices;

namespace Services.Billing;

public class GstCalculatorService(IBillingCalculatorService billingCalculatorService) : IGstCalculatorService
{
    public BillCalculationResult Compute(
        IReadOnlyCollection<BillItemInput> items,
        decimal billLevelDiscount,
        decimal serviceCharge,
        bool serviceChargeOptIn,
        bool isInterState)
    {
        return billingCalculatorService.Calculate(items, billLevelDiscount, serviceCharge, serviceChargeOptIn, isInterState);
    }
}

