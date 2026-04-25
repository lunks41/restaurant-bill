using IServices;
using IServices.Dtos;

namespace Services;

public class ReportService(ISalesReportRepository repository) : IReportService
{
    public Task<IReadOnlyList<DailySalesReportDto>> GetDailySalesAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => repository.GetDailySalesAsync(outletId, from, to, cancellationToken);

    public Task<IReadOnlyList<PaymentSummaryDto>> GetPaymentSummaryAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => repository.GetPaymentSummaryAsync(outletId, from, to, cancellationToken);
}
