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
    public DbSet<TableMaster> TableMasters => Set<TableMaster>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillItem> BillItems => Set<BillItem>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<KotHeader> KotHeaders => Set<KotHeader>();
    public DbSet<KotItem> KotItems => Set<KotItem>();
    public DbSet<StockLedgerEntry> StockLedger => Set<StockLedgerEntry>();
    public DbSet<StockLot> StockLots => Set<StockLot>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockLedger> StockLedgers => Set<StockLedger>();
    public DbSet<StockLoss> StockLosses => Set<StockLoss>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<KitchenStation> KitchenStations => Set<KitchenStation>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<EInvoiceQueueItem> EInvoiceQueue => Set<EInvoiceQueueItem>();
    public DbSet<PaymentCallbackEvent> PaymentCallbackEvents => Set<PaymentCallbackEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ReprintLog> ReprintLogs => Set<ReprintLog>();
    public DbSet<DayCloseReport> DayCloseReports => Set<DayCloseReport>();
    public DbSet<TaxMaster> TaxMasters => Set<TaxMaster>();
    public DbSet<PrinterProfile> PrinterProfiles => Set<PrinterProfile>();

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

        builder.Entity<TableMaster>(entity =>
        {
            entity.HasKey(x => x.TableMasterId);
            entity.Property(x => x.TableName).HasMaxLength(40).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Customer>(entity =>
        {
            entity.HasKey(x => x.CustomerId);
            entity.Property(x => x.CustomerName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(20);
            entity.Property(x => x.Gstin).HasMaxLength(15);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Supplier>(entity =>
        {
            entity.HasKey(x => x.SupplierId);
            entity.Property(x => x.SupplierName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.ContactNo).HasMaxLength(20);
            entity.Property(x => x.Gstin).HasMaxLength(15);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Item>(entity =>
        {
            entity.HasKey(x => x.ItemId);
            entity.Property(x => x.ItemCode).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SalePrice).HasPrecision(18, 4);
            entity.Property(x => x.PurchasePrice).HasPrecision(18, 4);
            entity.Property(x => x.GstPercent).HasPrecision(5, 2);
            entity.Property(x => x.ReorderLevel).HasPrecision(18, 4);
            entity.Property(x => x.SacCode).HasMaxLength(10);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Bill>(entity =>
        {
            entity.HasKey(x => x.BillId);
            entity.Property(x => x.BillNo).HasMaxLength(16).IsRequired();
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

        builder.Entity<Quotation>(entity =>
        {
            entity.HasKey(x => x.QuotationId);
            entity.Property(x => x.QuoteNo).HasMaxLength(16).IsRequired();
            entity.Property(x => x.SubTotal).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.GrandTotal).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.QuotationId);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<QuotationItem>(entity =>
        {
            entity.HasKey(x => x.QuotationItemId);
            entity.Property(x => x.ItemNameSnapshot).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Qty).HasPrecision(18, 4);
            entity.Property(x => x.Rate).HasPrecision(18, 4);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
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

        builder.Entity<StockLedgerEntry>(entity =>
        {
            entity.HasKey(x => x.StockLedgerEntryId);
            entity.Property(x => x.InQty).HasPrecision(18, 4);
            entity.Property(x => x.OutQty).HasPrecision(18, 4);
            entity.Property(x => x.Rate).HasPrecision(18, 4);
            entity.Property(x => x.RunningBalance).HasPrecision(18, 4);
            entity.Property(x => x.Remarks).HasMaxLength(250);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<KotHeader>(entity =>
        {
            entity.HasKey(x => x.KotHeaderId);
            entity.Property(x => x.KotNo).HasMaxLength(20).IsRequired();
            entity.Property(x => x.KotEventType).HasMaxLength(20);
            entity.Property(x => x.Status).HasMaxLength(20);
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

        builder.Entity<StockLot>(entity =>
        {
            entity.HasKey(x => x.StockLotId);
            entity.Property(x => x.QtyReceived).HasPrecision(18, 4);
            entity.Property(x => x.QtyRemaining).HasPrecision(18, 4);
            entity.Property(x => x.CostPerUnit).HasPrecision(18, 4);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<StockItem>(entity =>
        {
            entity.HasKey(x => x.StockItemId);
            entity.Property(x => x.CurrentQty).HasPrecision(18, 4);
            entity.Property(x => x.ReorderLevel).HasPrecision(18, 4);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<StockLedger>(entity =>
        {
            entity.HasKey(x => x.StockLedgerId);
            entity.Property(x => x.InQty).HasPrecision(18, 4);
            entity.Property(x => x.OutQty).HasPrecision(18, 4);
            entity.Property(x => x.Rate).HasPrecision(18, 4);
            entity.Property(x => x.RunningBalance).HasPrecision(18, 4);
            entity.Property(x => x.Remarks).HasMaxLength(250);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<StockLoss>(entity =>
        {
            entity.HasKey(x => x.StockLossId);
            entity.Property(x => x.Qty).HasPrecision(18, 4);
            entity.Property(x => x.Rate).HasPrecision(18, 4);
            entity.Property(x => x.Reason).HasMaxLength(250);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<StockAdjustment>(entity =>
        {
            entity.HasKey(x => x.StockAdjustmentId);
            entity.Property(x => x.Qty).HasPrecision(18, 4);
            entity.Property(x => x.Rate).HasPrecision(18, 4);
            entity.Property(x => x.AdjustmentType).HasMaxLength(20);
            entity.Property(x => x.Reason).HasMaxLength(250);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Purchase>(entity =>
        {
            entity.HasKey(x => x.PurchaseId);
            entity.Property(x => x.PurchaseNo).HasMaxLength(24).IsRequired();
            entity.Property(x => x.SubTotal).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.GrandTotal).HasPrecision(18, 2);
            entity.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.PurchaseId);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<PurchaseItem>(entity =>
        {
            entity.HasKey(x => x.PurchaseItemId);
            entity.Property(x => x.Qty).HasPrecision(18, 4);
            entity.Property(x => x.Rate).HasPrecision(18, 4);
            entity.Property(x => x.TaxPercent).HasPrecision(5, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
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

        builder.Entity<EInvoiceQueueItem>(entity =>
        {
            entity.HasKey(x => x.EInvoiceQueueItemId);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Irn).HasMaxLength(80);
            entity.Property(x => x.LastError).HasMaxLength(1000);
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

        builder.Entity<TaxMaster>(entity =>
        {
            entity.HasKey(x => x.TaxMasterId);
            entity.Property(x => x.TaxName).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TaxPercent).HasPrecision(5, 2);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<PrinterProfile>(entity =>
        {
            entity.HasKey(x => x.PrinterProfileId);
            entity.Property(x => x.PrinterName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PrinterType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.DevicePath).HasMaxLength(200);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });
    }
}

