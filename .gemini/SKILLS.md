# 🛠️ Unified Master Skill: CleanStack Architect

> **Core Philosophy:** Write it clean, keep it fast, test the edges, leave no trace.
>
> **Behavioral Rule:** Skill directives strictly override generic defaults. Never silently skip skills; report missing files explicitly.

---

## 🏛️ 1. System Architecture & Stack

* **Backend:** C# .NET (ASP.NET Core)
* **Frontend:** Vue 3 + TypeScript
* **Database:** SQLite (per-tenant)
* **Project Context:** BIMASAKTI Reports

### Architecture & Coding Principles

* **Structure:** Adhere to the **"Glance" rule**, ensuring folder structures allow developers to instantly locate queries, routes, and UI components.
* **Separation:** Maintain strict separation of concerns between routing, business logic (Services), and data access.
* **Synchronization:** Ensure graceful full-stack synchronization when API contracts change. Output plain English errors.

---

## 🛡️ 2. Universal Code Standards

* **Readability:** Write defensively, but code for humans first, prioritizing natural whitespace between logic blocks.
* **Naming:** Use clear, conversational variable names. **Do not use abbreviations** like `idx`, `err`, or `req`.
* **Complexity:** Unpack nested ternary operators and dense one-liners into simple sequential steps or `if/else` statements. Write functions that do exactly one thing.
* **Safety:** Apply strict type hints and defensive `try/catch` blocks.

---

## 💻 3. Language-Specific Execution

### 🟢 Vue / Frontend
* **ALWAYS** use the Composition API (`<script setup>`).
* Move complex logic to computed properties.
* **ALWAYS** clear intervals/listeners in `onUnmounted`.
* Ensure all Vue components use descriptive, multi-word names.

### 🔵 C# / .NET
* Use `PascalCase` for standard methods.
* Favor straightforward `foreach` loops or simple LINQ queries over deep chaining.
* Ensure all async methods use the `Async` suffix and return `Task` or `Task<T>`.

### 🟡 JavaScript / TypeScript
* Avoid global variables.
* Use `async/await` rather than nested promise chains.
* Explicitly define all TypeScript interfaces without using the `any` type.

### 🐍 Python
* Enforce PEP-8 formatting with explicit type hinting.
* Use natural `try/except` blocks returning readable errors.

---

## 🏷️ 4. BIMASAKTI C# Naming Conventions

To maintain structural integrity within the `backend/` directory, strict **lowercase-prefixed camelCase** naming is mandatory:

| Prefix | Target Category | Example Implementation |
| :--- | :--- | :--- |
| `ds` | Dashboard scripts/controllers | `dsAdminDashboard.cs` |
| `pr` | Print & Export scripts | `prFinancialPDF.cs` |
| `rp` | Reports and data collation | `rpGlrx0310.cs` |
| `svc` | Services and utilities | `svcDbUtils.cs` |

### Key Rules
* **Routing:** Never modify ASP.NET Routing attributes to maintain frontend stability.
* **Dependency Injection:** Interface mappings for DI must retain the prefix after the initial `I` (e.g., `IsvcAuthenticationService`). Ensure DI registrations in `Program.cs` follow this mapping.

---

## 🧹 5. Required Workflow: Search, Destroy & Refactor

Before completing any task, execute this internal janitorial checklist:

1. **Eradicate Junk:** Delete dead logic, unreachable returns, unused variables, uncalled functions, and obsolete imports.
2. **Remove Remnants:** Eradicate all `console.log`, `print()`, and temporary debug statements.
3. **Plug Leaks:** Identify missing cleanup logic and inject teardown code.
4. **Final Output Check:** Return the sterilized code alongside a bulleted **"Kill List"** of removed junk. Include a brief summary of how readability and safety were improved.
