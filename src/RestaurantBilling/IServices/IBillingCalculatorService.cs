using Models.Billing;

namespace IServices;

public interface IBillingCalculatorService
{
    BillCalculationResult Calculate(
        IReadOnlyCollection<BillItemInput> items,
        decimal billLevelDiscount,
        decimal serviceCharge,
        bool serviceChargeOptIn,
        bool isInterState);
}

