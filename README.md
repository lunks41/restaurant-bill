# Restaurant Billing & Stock Management System

Production-oriented multi-tenant restaurant billing platform for India built with .NET 8, SQL Server, and hybrid MVC/Razor UI.

## Prerequisites

- .NET 8 SDK
- SQL Server 2019+
- Visual Studio 2022 or JetBrains Rider

## Configuration

Update `src/RestaurantBilling/appsettings.json`:

- `ConnectionStrings:DefaultConnection`
- `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Key`
- Serilog sinks and file path

## Database Setup

```bash
dotnet ef database update --project src/RestaurantBilling.Infrastructure/RestaurantBilling.Infrastructure.csproj --startup-project src/RestaurantBilling/RestaurantBilling.csproj
```

Seed data is applied automatically on startup (roles, tax configuration, number series, default outlet/settings).

## First Run Checklist

1. Configure outlet GSTIN and 14-digit FSSAI number.
2. Configure number series for Bill/Quote/KOT/Receipt.
3. Verify GST scenario (Standalone/Specified Premises/Composition).
4. Configure printer profile and run test print.
5. Configure payment gateway settings for non-cash modes.

## Thermal Printer Notes

- Supported approach: TCP network printer (port `9100`) and extensible provider model.
- Default flow supports ESC/POS rendering fallback patterns.

## Payment Gateway Notes

- Razorpay dynamic QR and PhonePe dynamic QR are designed via provider abstraction.
- UPI Collect is intentionally excluded.
- Card data must be limited to masked values (last 4 digits only).

## Roles and Permissions

- Roles: Admin, Manager, Cashier, Captain, Kitchen, StockManager, Accountant
- Policies: `CanVoidBill`, `CanApplyDiscount`, `CanCloseDay`, `CanDeleteMaster`

## Day-End Closing

1. Open `/dayclose`
2. Review X-report preview
3. Validate unsettled bills and payment splits
4. Perform final Z-close with manager role

## Backup Recommendation

- Nightly full backup
- 15-minute transaction log backups
- 30-day retention minimum

## Legal Disclaimer

Verify all GST rates, SAC/HSN mappings, and e-invoicing thresholds with a qualified CA before production rollout. Service charge behavior must comply with CCPA guidelines and applicable court rulings.
