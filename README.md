# AcxiomCRM

AcxiomCRM is an ASP.NET Core MVC customer relationship management application with a companion JSON API. It provides tools for managing customers, leads, sales opportunities, follow-ups, activities, users, and audit history, along with dashboards and reports.

## Features

- Customer, lead, and opportunity management
- Follow-up and activity tracking
- Dashboard and sales reporting
- User accounts and role-based access using ASP.NET Core Identity
- Audit logging and duplicate detection
- JSON API for authentication and CRM records
- SQLite by default, with SQL Server also supported

## Requirements

- .NET 8 SDK
- Visual Studio 2022 or another .NET-compatible editor (optional)

## Run locally

From the repository root:

```powershell
dotnet restore AcxiomCRM.sln
dotnet run --project AcxiomCRM\AcxiomCRM.csproj
```

Open the local URL printed by `dotnet run`. The application applies Entity Framework Core migrations at startup and creates a local `AcxiomCRM.db` SQLite database by default. It also seeds the application roles, demo users, and sample CRM data when the database is empty.

The seeded accounts and sample data are for local development only. Replace or remove default credentials and sample data before deploying the application.

## Database configuration

SQLite is the default provider. Its connection string is configured in `AcxiomCRM\appsettings.json`:

```json
{
  "DatabaseProvider": "Sqlite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=AcxiomCRM.db"
  }
}
```

To use SQL Server, set `DatabaseProvider` to `SqlServer` and configure `ConnectionStrings:SqlServerConnection`. Configuration can be overridden with standard ASP.NET Core configuration providers, including environment variables. For example:

```powershell
$env:DatabaseProvider = "SqlServer"
$env:ConnectionStrings__SqlServerConnection = "Server=localhost;Database=AcxiomCRM;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run --project AcxiomCRM\AcxiomCRM.csproj
```

## Run tests

```powershell
dotnet test AcxiomCRM.sln
```

## API

The API is rooted at `/api`. Authentication is provided by `POST /api/auth/login` and `POST /api/auth/logout`. CRM API endpoints require authentication unless explicitly configured otherwise:

- `/api/customers`
- `/api/leads`
- `/api/opportunities`
- `/api/followups`

List endpoints support paging and relevant filtering options. Enum values are serialized as strings. Use the API controller DTOs as the source of truth for request and response fields.

## Solution layout

- `AcxiomCRM\` — ASP.NET Core MVC application, API controllers, data models, services, views, and EF Core migrations
- `AcxiomCRM.Tests\` — xUnit tests
- `AcxiomCRM.sln` — Visual Studio solution
