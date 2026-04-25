using Microsoft.EntityFrameworkCore;
using Entities.Configuration;
using Entities.Integration;
using Entities.Inventory;
using Entities.Organisation;
using Entities.Reports;
using Entities.Sales;
using Entities.Audit;

namespace IServices;

public interface IAppDbContext
{
    DbSet<Bill> Bills { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Quotation> Quotations { get; }
    DbSet<DayCloseReport> DayCloseReports { get; }
    DbSet<RestaurantSetting> RestaurantSettings { get; }
    DbSet<StockLot> StockLots { get; }
    DbSet<StockLedgerEntry> StockLedger { get; }
    DbSet<OutboxEvent> OutboxEvents { get; }
    DbSet<EInvoiceQueueItem> EInvoiceQueue { get; }
    DbSet<PaymentCallbackEvent> PaymentCallbackEvents { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Outlet> Outlets { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

