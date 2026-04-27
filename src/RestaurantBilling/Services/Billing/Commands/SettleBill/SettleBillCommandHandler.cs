using MediatR;
using Microsoft.EntityFrameworkCore;
using Models.Billing;
using Helper;
using IServices;
using Entities.Configuration;
using Entities.Enums;
using Entities.Integration;
using Entities.Sales;

namespace Services.Billing.Commands.SettleBill;

public class SettleBillCommandHandler(
    IAppDbContext db,
    IBillingCalculatorService calculatorService,
    INumberGeneratorService numberGeneratorService,
    IStockService stockService) : IRequestHandler<SettleBillCommand, Result<long>>
{
    public async Task<Result<long>> Handle(SettleBillCommand request, CancellationToken cancellationToken)
    {
        var dayLocked = await db.DayCloseReports.AnyAsync(
            x => x.OutletId == request.OutletId && x.BusinessDate == request.BusinessDate && x.IsLocked,
            cancellationToken);
        if (dayLocked)
        {
            return Result<long>.Failure("Business date is locked. Cannot settle bill.", ResultStatus.Conflict);
        }

        var billNo = await numberGeneratorService.GenerateAsync(request.OutletId, NumberSeriesKey.Bill, cancellationToken);

        var calcInput = request.Items.Select(x => new BillItemInput(
            x.ItemId,
            x.ItemName,
            x.Qty,
            x.Rate,
            x.DiscountAmount,
            x.TaxPercent,
            x.IsTaxInclusive,
            x.TaxType)).ToList();

        var computed = calculatorService.Calculate(
            calcInput,
            request.BillLevelDiscount,
            request.ServiceChargeAmount,
            request.ServiceChargeOptIn,
            request.IsInterState);

        var bill = new Bill(request.OutletId, billNo, request.BusinessDate, request.BillType);
        bill.SetTableName(request.TableName);
        bill.SetCustomerInfo(request.CustomerName, request.Phone);
        foreach (var line in computed.Lines)
        {
            bill.AddItem(new BillItem(
                line.ItemId,
                line.ItemName,
                line.Qty,
                line.Rate,
                line.DiscountAmount,
                line.TaxAmount));
        }

        bill.SetServiceCharge(request.ServiceChargeAmount, request.ServiceChargeOptIn);

        var paymentEntities = request.Payments
            .Select(x => new Payment(x.Mode, x.Amount, x.ReferenceNo, x.CardLast4, x.UpiTxnId))
            .ToList();
        bill.Settle(paymentEntities);

        db.Bills.Add(bill);

        await stockService.DeductSaleStockAsync(
            request.OutletId,
            request.BusinessDate,
            bill.Items.ToList(),
            cancellationToken);

        db.OutboxEvents.Add(new OutboxEvent
        {
            EventType = "BillSettled",
            Payload = $"{{\"billNo\":\"{billNo}\"}}",
            Status = "Pending"
        });

        await db.SaveChangesAsync(cancellationToken);
        return Result<long>.Success(bill.BillId);
    }
}

