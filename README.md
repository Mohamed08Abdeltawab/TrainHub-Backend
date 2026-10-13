# 🎓 TrainHub — Multi-Tenant Training Center SaaS MVP (Backend API)

[![Backend .NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Tests Passing](https://img.shields.io/badge/Backend%20Tests-626%20Passed-brightgreen)](https://github.com)

**TrainHub Backend** is a modular monolith ASP.NET Core 10 Web API built using Clean Architecture principles, EF Core with SQL Server, multi-tenant isolation, and JWT authentication.

---

## 🏛️ System Architecture

TrainHub follows Clean Architecture:
- **`TrainHub.Domain`**: Core domain entities, value objects, invariants, and constants.
- **`TrainHub.Application`**: Use cases, business logic services, DTOs, interfaces, and validators.
- **`TrainHub.Infrastructure`**: EF Core `AppDbContext`, tenant resolution middleware, JWT token provider, and `DemoDataSeeder`.
- **`TrainHub.Api`**: RESTful Controllers, authorization policies, global exception handler, OpenAPI spec.
- **`TrainHub.UnitTests`**: Comprehensive xUnit test suite (626 tests).

---

## 🚀 Getting Started

### Prerequisites
- .NET 10 SDK
- SQL Server (LocalDB or SQL Server 2022)

### Running Migrations & API
```bash
# 1. Update database schema
dotnet ef database update --project .\TrainHub.Infrastructure --startup-project .\TrainHub.Api

# 2. Run the Web API
dotnet run --project .\TrainHub.Api
```

The API will listen at `http://localhost:5087` (OpenAPI docs at `/openapi/v1.json`).

On initial startup, `Database.MigrateAsync()` applies pending migrations and `DemoDataSeeder` automatically creates 2 multi-tenant organizations and 4 test accounts.

---

## 🔑 Demo Credentials

All seeded accounts use password: `demo1234`

| Role | Name | Email |
| :--- | :--- | :--- |
| **Super Admin** | Jordan Lee | `jordan.lee@trainhub.io` |
| **Org Admin** | Alex Morgan | `alex.morgan@trainhub.io` |
| **Instructor** | Sarah Jenkins | `sarah.jenkins@trainhub.io` |
| **Learner** | David Kim | `david.kim@trainhub.io` |

---

## 🧪 Testing

```bash
dotnet test
```
Result: **626 tests passing (100% pass rate)**.
