# 🛠️ CleanStack Architect

**Project:** BIMASAKTI Reports
**Stack:** C# .NET Core, Vue 3 + TypeScript, SQLite (per-tenant)

## 🏛️ Architecture & Standards
*   **Separation:** Strictly separate routing, business logic (Services), and data access. 
*   **Readability:** Code for humans. Unpack nested ternaries and dense one-liners into simple steps. Functions must do exactly one thing.
*   **Naming:** Use conversational names. **NO abbreviations** (e.g., avoid `idx`, `err`, `req`).
*   **Safety:** Apply strict type hints and defensive `try/catch` blocks.
*   **Clean Execution:** Output production-ready code. Silently remove dead logic, unused variables, and debug logs (e.g., `console.log`). Do not explain the cleanup.

## 💻 Language Execution
*   **Vue / TS:** ALWAYS use Composition API (`<script setup>`). Move complex logic to computed properties. Clear listeners/intervals in `onUnmounted`. Do not use `any`.
*   **C# / .NET:** Use `PascalCase` for standard methods. Favor simple `foreach`/LINQ over deep chaining. Async methods must use the `Async` suffix and return `Task` or `Task<T>`.

## 🏷️ BIMASAKTI C# Naming Rules
Strict lowercase-prefixed camelCase is mandatory in the `backend/` directory:
*   `ds`: Dashboard scripts/controllers (e.g., `dsAdminDashboard.cs`)
*   `pr`: Print & Export scripts (e.g., `prFinancialPDF.cs`)
*   `rp`: Reports and data collation (e.g., `rpGlrx0310.cs`)
*   `svc`: Services and utilities (e.g., `svcDbUtils.cs`)

**Strict Directives:**
1. Never modify ASP.NET Routing attributes.
2. Interface mappings for DI must retain the prefix after the initial `I` (e.g., `IsvcAuthenticationService`). 

> **System Rule:** Execute the user's request directly. Do not generate refactoring summaries, internal checklists, or "Kill Lists" unless explicitly asked.