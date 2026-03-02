# Supermarket Management System (SMS)

Current stack:

- `SMS.Api` - ASP.NET Core API (.NET 10)
- `SMS.Core` - DTOs, interfaces, services
- `SMS.Data` - entities, enums, DbContext, EF migrations
- `sms-frontend` - Angular frontend

## Backend

```bash
dotnet build SMS.slnx
dotnet run --project SMS.Api
```

API default URL:

- `http://localhost:5032`

## Frontend

```bash
npm install --prefix sms-frontend
npm run start --prefix sms-frontend
```

Frontend URL:

- `http://localhost:4200`

## Database

SQL Server LocalDB connection is configured in:

- `SMS.Api/appsettings.json`

Default DB:

- `SMSDb`
