using Models.Billing;
using IServices;
using Entities.Enums;

namespace Services.Billing;

public class BillingCalculatorService : IBillingCalculatorService
{
    public BillCalculationResult Calculate(
        IReadOnlyCollection<BillItemInput> items,
        decimal billLevelDiscount,
        decimal serviceCharge,
        bool serviceChargeOptIn,
        bool isInterState)
    {
        var lines = new List<BillItemComputed>();

        foreach (var item in items)
        {
            var gross = item.Rate * item.Qty;
            var discount = item.DiscountAmount;
            var net = gross - discount;

            decimal taxableAmount;
            decimal taxAmount;

            if (item.TaxType == TaxType.StateVAT || item.TaxType == TaxType.Exempt)
            {
                taxableAmount = net;
                taxAmount = item.TaxType == TaxType.StateVAT ? Math.Round(net * item.TaxPercent / 100m, 2) : 0m;
            }
            else if (item.IsTaxInclusive)
            {
                taxableAmount = Math.Round(net / (1 + (item.TaxPercent / 100m)), 2);
                taxAmount = Math.Round(net - taxableAmount, 2);
            }
            else
            {
                taxableAmount = net;
                taxAmount = Math.Round(taxableAmount * item.TaxPercent / 100m, 2);
            }

            var cgst = 0m;
            var sgst = 0m;
            var igst = 0m;
            var stateVat = 0m;

            if (item.TaxType == TaxType.GST)
            {
                if (isInterState)
                {
                    igst = taxAmount;
                }
                else
                {
                    cgst = Math.Round(taxAmount / 2m, 2);
                    sgst = Math.Round(taxAmount - cgst, 2);
                }
            }
            else if (item.TaxType == TaxType.StateVAT)
            {
                stateVat = taxAmount;
            }

            lines.Add(new BillItemComputed(
                item.ItemId,
                item.ItemName,
                item.Qty,
                item.Rate,
                discount,
                taxableAmount,
                taxAmount,
                Math.Round(taxableAmount + taxAmount, 2),
                cgst,
                sgst,
                igst,
                stateVat));
        }

        var subTotal = Math.Round(lines.Sum(x => x.Rate * x.Qty), 2);
        var totalDiscount = Math.Round(lines.Sum(x => x.DiscountAmount) + billLevelDiscount, 2);
        var totalTax = Math.Round(lines.Sum(x => x.TaxAmount), 2);
        var serviceChargeValue = serviceChargeOptIn ? serviceCharge : 0m;

        var computedGrand = subTotal - totalDiscount + totalTax + serviceChargeValue;
        var roundOff = Math.Round(computedGrand, MidpointRounding.AwayFromZero) - computedGrand;
        var grandTotal = computedGrand + roundOff;

        return new BillCalculationResult(lines, subTotal, totalDiscount, totalTax, serviceChargeValue, roundOff, grandTotal);
    }
}

