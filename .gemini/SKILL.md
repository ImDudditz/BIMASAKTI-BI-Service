# Bimasakti Reports C# Naming Conventions Guideline

To maintain architectural integrity, clean readability, and compliance with the frontend Vue integration layers, all C# files, classes, and services inside the `backend/` directory must follow a strict, lowercase-prefixed camelCase naming system:

## 1. Naming Categories

| Prefix | Category | Description / Examples |
| :--- | :--- | :--- |
| **`ds`** | **Dashboard** | Used for all dashboard scripts, controllers, or code. <br> *Examples*: `dsAdminDashboard.cs` (class `dsAdminDashboard`), `dsLm.cs` (class `dsLm`), `dsGlrx0310.cs` (class `dsGlrx0310`) |
| **`pr`** | **Print & Export** | Used for PDF, Excel generation, or exporting scripts. <br> *Examples*: `prFinancialExcel.cs`, `prFinancialPDF.cs` |
| **`rp`** | **Reports** | Used for report queries, data collation, or standard report endpoints. <br> *Examples*: `rpGlrx0310.cs` (class `rpGlrx0310`) |
| **`svc`** | **Services & Utilities** | Used for backend services, dependency injection business layers, and static utility classes. <br> *Examples*: `svcDbUtils.cs` (class `svcDbUtils`), `svcAuthenticationService.cs` (class `svcAuthenticationService`), `svcDatabaseSyncService.cs` (class `svcDatabaseSyncService`) |

## 2. Core Constraints

1. **Explicit Casing**:
   - The prefix must be **entirely lowercase** (e.g., `ds`, `pr`, `rp`, `svc`).
   - The following segment of the filename and class name should start with an uppercase letter (e.g., `dsLm` not `dslm`, `svcDbUtils` not `svcdbutils`).

2. **Endpoint Stability**:
   - Although C# class names and files are refactored to use the lowercase prefixes, **do not modify ASP.NET Routing attributes** (e.g., `[Route("api/auth")]` or `[HttpGet("filters")]`).
   - Standard route paths must remain completely identical to avoid breaking the frontend layers.

3. **Dependency Injection**:
   - Interfaces corresponding to `svc` services must also carry the prefix after the initial `I` (e.g., `IsvcAuthenticationService`, `IsvcDatabaseSyncService`, `IsvcGLRX0310`).
   - Ensure all new DI registrations in `backend/Program.cs` follow this interface-to-implementation mapping.
