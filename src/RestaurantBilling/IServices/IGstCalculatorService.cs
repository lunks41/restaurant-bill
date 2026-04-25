using Models.Billing;

namespace IServices;

public interface IGstCalculatorService
{
    BillCalculationResult Compute(
        IReadOnlyCollection<BillItemInput> items,
        decimal billLevelDiscount,
        decimal serviceCharge,
        bool serviceChargeOptIn,
        bool isInterState);
}

