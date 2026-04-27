using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using IServices;
using Entities.Audit;
using Entities.Configuration;
using Entities.Inventory;
using Entities.Integration;
using Entities.Kitchen;
using Entities.Masters;
using Entities.Organisation;
using Entities.Reports;
using Entities.Sales;

namespace Data.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<Microsoft.AspNetCore.Identity.IdentityUser<int>, Microsoft.AspNetCore.Identity.IdentityRole<int>, int>(options), IAppDbContext
{
    public DbSet<Outlet> Outlets => Set<Outlet>();
    public DbSet<TaxConfiguration> TaxConfigurations => Set<TaxConfiguration>();
    public DbSet<NumberSeries> NumberSeries => Set<NumberSeries>();
    public DbSet<RestaurantSetting> RestaurantSettings => Set<RestaurantSetting>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<DiningTables> DiningTables => Set<DiningTables>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillItem> BillItems => Set<BillItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<KotHeader> KotHeaders => Set<KotHeader>();
    public DbSet<KotItem> KotItems => Set<KotItem>();
    public DbSet<GroceryStockItem> GroceryStockItems => Set<GroceryStockItem>();
    public DbSet<Grocery> Groceries => Set<Grocery>();
    public DbSet<KitchenStation> KitchenStations => Set<KitchenStation>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<PaymentCallbackEvent> PaymentCallbackEvents => Set<PaymentCallbackEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ReprintLog> ReprintLogs => Set<ReprintLog>();
    public DbSet<DayCloseReport> DayCloseReports => Set<DayCloseReport>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Outlet>(entity =>
        {
            entity.HasKey(x => x.OutletId);
            entity.Property(x => x.OutletName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.StateCode).HasMaxLength(2).IsRequired();
            entity.Property(x => x.FssaiNumber).HasMaxLength(14).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<TaxConfiguration>(entity =>
        {
            entity.HasKey(x => x.TaxConfigurationId);
            entity.Property(x => x.ScenarioType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.TotalGstPercent).HasPrecision(5, 2);
            entity.Property(x => x.CgstPercent).HasPrecision(5, 2);
            entity.Property(x => x.SgstPercent).HasPrecision(5, 2);
            entity.Property(x => x.IgstPercent).HasPrecision(5, 2);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<NumberSeries>(entity =>
        {
            entity.HasKey(x => x.NumberSeriesId);
            entity.Property(x => x.Prefix).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Suffix).HasMaxLength(10);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<RestaurantSetting>(entity =>
        {
            entity.HasKey(x => x.RestaurantSettingId);
            entity.Property(x => x.SettingKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SettingValue).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Category>(entity =>
        {
            entity.HasKey(x => x.CategoryId);
            entity.Property(x => x.CategoryName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Unit>(entity =>
        {
            entity.HasKey(x => x.UnitId);
            entity.Property(x => x.UnitName).HasMaxLength(60).IsRequired();
            entity.Property(x => x.UnitCode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<DiningTables>(entity =>
        {
            entity.HasKey(x => x.TableMasterId);
            entity.ToTable("DiningTables");
            entity.Property(x => x.TableName).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Area).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Item>(entity =>
        {
            entity.HasKey(x => x.ItemId);
            entity.Property(x => x.ItemCode).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SalePrice).HasPrecision(18, 4);
            entity.Property(x => x.GstPercent).HasPrecision(5, 2);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Bill>(entity =>
        {
            entity.HasKey(x => x.BillId);
            entity.Property(x => x.BillNo).HasMaxLength(16).IsRequired();
            entity.Property(x => x.CustomerName).HasMaxLength(160);
            entity.Property(x => x.Phone).HasMaxLength(20);
            entity.Property(x => x.TableName).HasMaxLength(40);
            entity.Property(x => x.SubTotal).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.ServiceCharge).HasPrecision(18, 2);
            entity.Property(x => x.RoundOff).HasPrecision(18, 2);
            entity.Property(x => x.GrandTotal).HasPrecision(18, 2);
            entity.Property(x => x.PaidAmount).HasPrecision(18, 2);
            entity.Property(x => x.BalanceAmount).HasPrecision(18, 2);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.BillId);
            entity.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.BillId);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<BillItem>(entity =>
        {
            entity.HasKey(x => x.BillItemId);
            entity.Property(x => x.ItemNameSnapshot).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Qty).HasPrecision(18, 4);
            entity.Property(x => x.RateSnapshot).HasPrecision(18, 4);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxableAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.Property(x => x.RowVersion).IsRowVersion();
        });

        builder.Entity<Payment>(entity =>
        {
            entity.HasKey(x => x.PaymentId);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.ReferenceNo).HasMaxLength(80);
            entity.Property(x => x.CardLast4).HasMaxLength(4);
            entity.Property(x => x.UpiTxnId).HasMaxLength(80);
            entity.Property(x => x.RowVersion).IsRowVersion();
        });


        builder.Entity<KotHeader>(entity =>
        {
            entity.HasKey(x => x.KotHeaderId);
            entity.Property(x => x.KotNo).HasMaxLength(20).IsRequired();
            entity.Property(x => x.KotEventType).HasMaxLength(20);
            entity.Property(x => x.Status).HasMaxLength(20);
            entity.Property(x => x.ServedByUserId).HasMaxLength(100);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<KotItem>(entity =>
        {
            entity.HasKey(x => x.KotItemId);
            entity.Property(x => x.ItemNameSnapshot).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Qty).HasPrecision(18, 4);
            entity.Property(x => x.RowVersion).IsRowVersion();
        });

        builder.Entity<GroceryStockItem>(entity =>
        {
            entity.HasKey(x => x.GroceryStockItemId);
            entity.Property(x => x.GroceryId).IsRequired();
            entity.Property(x => x.CurrentQty).HasPrecision(18, 4);
            entity.Property(x => x.ReorderLevel).HasPrecision(18, 4);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Grocery>(entity =>
        {
            entity.HasKey(x => x.GroceryId);
            entity.Property(x => x.GroceryName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<KitchenStation>(entity =>
        {
            entity.HasKey(x => x.KitchenStationId);
            entity.Property(x => x.StationName).HasMaxLength(60).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<OutboxEvent>(entity =>
        {
            entity.HasKey(x => x.OutboxEventId);
            entity.Property(x => x.EventType).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Error).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<PaymentCallbackEvent>(entity =>
        {
            entity.HasKey(x => x.PaymentCallbackEventId);
            entity.Property(x => x.Provider).HasMaxLength(30).IsRequired();
            entity.Property(x => x.EventId).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ProcessingStatus).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(500);
            entity.Property(x => x.MatchedPaymentId);
            entity.Property(x => x.MatchedBillId);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.Provider, x.EventId }).IsUnique();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.AuditLogId);
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PreviousHash).HasMaxLength(128);
            entity.Property(x => x.EntryHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
        });

        builder.Entity<ReprintLog>(entity =>
        {
            entity.HasKey(x => x.ReprintLogId);
            entity.Property(x => x.DocumentType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(300);
            entity.Property(x => x.RowVersion).IsRowVersion();
        });

        builder.Entity<DayCloseReport>(entity =>
        {
            entity.HasKey(x => x.DayCloseReportId);
            entity.Property(x => x.OpeningCash).HasPrecision(18, 2);
            entity.Property(x => x.ClosingCash).HasPrecision(18, 2);
            entity.Property(x => x.TotalSales).HasPrecision(18, 2);
            entity.Property(x => x.TotalTax).HasPrecision(18, 2);
            entity.Property(x => x.CashOverShort).HasPrecision(18, 2);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

    }
}

