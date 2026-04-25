using IServices.Dtos;

namespace IServices;

public interface ISalesReportRepository
{
    Task<IReadOnlyList<DailySalesReportDto>> GetDailySalesAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockVarianceDto>> GetStockVarianceAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentSummaryDto>> GetPaymentSummaryAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<VoidReportDto>> GetVoidReportAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockMovementDto>> GetStockMovementAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

