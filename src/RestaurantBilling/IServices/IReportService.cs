using IServices.Dtos;

namespace IServices;

public interface IReportService
{
    Task<IReadOnlyList<DailySalesReportDto>> GetDailySalesAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentSummaryDto>> GetPaymentSummaryAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
