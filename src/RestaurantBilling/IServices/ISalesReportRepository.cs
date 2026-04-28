using IServices.Dtos;

namespace IServices;

public interface ISalesReportRepository
{
    Task<IReadOnlyList<DailySalesReportDto>> GetDailySalesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockVarianceDto>> GetStockVarianceAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentSummaryDto>> GetPaymentSummaryAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<VoidReportDto>> GetVoidReportAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockMovementDto>> GetStockMovementAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

