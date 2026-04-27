using Microsoft.EntityFrameworkCore;
using Entities.Configuration;
using Entities.Integration;
using Entities.Organisation;
using Entities.Reports;
using Entities.Sales;
using Entities.Audit;

namespace IServices;

public interface IAppDbContext
{
    DbSet<Bill> Bills { get; }
    DbSet<Payment> Payments { get; }
    DbSet<DayCloseReport> DayCloseReports { get; }
    DbSet<RestaurantSetting> RestaurantSettings { get; }
    DbSet<OutboxEvent> OutboxEvents { get; }
    DbSet<PaymentCallbackEvent> PaymentCallbackEvents { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Outlet> Outlets { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

