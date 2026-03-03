# SMS (Supermarket Management System)

SMS is a retail and wallet-enabled management platform with a .NET API and an Angular frontend.

## System Identity

- System name: `Supermarket Management System (SMS)`
- Support email: `admin@sms.local`
- Support phone: `+263774700574`
- Address line 1: `3281 Hlalanikuhle Ext-Hwange`
- Address line 2: `Dete place`
- Logo asset: `sms-frontend/public/branding/sms-logo.svg`

## What The System Covers

- POS checkout with payment tracking (`Cash`, `Card`, `Digital`)
- Receipt and quotation generation with SMS branding
- Inventory management including manual stock entry and physical count updates
- Procurement draft purchase orders and vendor management
- Customer CRUD and member purchase history
- Wallet/account operations, transfers, QR/NFC access methods
- Staff management (create, role updates, status changes, delete)
- Reporting (EOD, shrinkage, sales trends) with PDF export
- Authentication and role-aware admin/staff experiences

## Project Structure

```text
SMS/
|-- SMS.Api/         # ASP.NET Core Web API
|-- SMS.Core/        # Business logic and DTOs
|-- SMS.Data/        # EF Core entities, DbContext, migrations
|-- sms-frontend/    # Angular frontend
`-- SMS.slnx         # Solution file
```

## Technology Stack

- Backend: `ASP.NET Core 10`, `Entity Framework Core 10`, `JWT`
- Frontend: `Angular 21`, `Angular Material`, `PrimeNG`
- Database: `SQL Server` (default local setup uses `MSSQLLocalDB`)
- PDF generation: `jsPDF` (frontend reports and exports)

## Prerequisites

- `.NET SDK 10.0.103` (see `global.json`)
- `Node.js 20+` and `npm`
- `SQL Server LocalDB` or another SQL Server instance

## Local Setup

1. Restore and build the solution:
```bash
dotnet restore SMS.slnx
dotnet build SMS.slnx
```

2. Run the API:
```bash
dotnet run --project SMS.Api
```

3. Run the frontend (new terminal):
```bash
cd sms-frontend
npm install
npm run start
```

4. Open:
- Frontend: `http://localhost:4200`
- API base URL: `http://localhost:5032`
- Health check: `http://localhost:5032/health`
- Swagger (Development): `http://localhost:5032/swagger`

## Configuration

Main backend config is in `SMS.Api/appsettings.json`:

- Connection string key: `ConnectionStrings:DefaultConnection`
- JWT section: `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SecretKey`, `Jwt:TokenLifetimeMinutes`
- Default local DB: `SMSDb` on `MSSQLLocalDB`

The API startup applies migrations and seed data automatically on launch via `DataSeeder.SeedAsync(...)`.

## Default Seeded Staff Accounts

- Admin: `admin` / `Admin@123`
- Admin Ops: `admin_ops` / `OpsAdmin@123!`
- Staff POS: `staff_pos` / `Staff@123`

Use these for local development only. Change credentials for non-local environments.

## Reporting And Document Output

- POS prints branded receipts and quotations with:
  - SMS logo
  - system name
  - email and phone
  - address
  - structured line items and totals
- PDF reports (EOD, shrinkage, sales trends) include:
  - top-left logo and address
  - report metadata
  - table-based structured data
  - footer with contact details and page numbering

## Notes

- CORS in API is configured for local frontend origins on port `4200`.
- HTTPS redirection is disabled in Development and enabled outside Development.
- Frontend API calls are currently hardcoded to `http://localhost:5032`.
