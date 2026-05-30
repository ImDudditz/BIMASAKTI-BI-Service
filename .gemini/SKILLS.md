# 🛠️ CleanStack Architect (Vibe Coding Edition)

**Stack:** C# .NET Core (Backend) | React / Vue 3 / Plain HTML + CSS (Frontend) | Java (Side Framework) | SQLite (per-tenant)

## 🏛️ Architecture, Vibe & Standards
*   **Separation:** Strictly separate routing, business logic (Services), and data access.
*   **Vibe Coding & Readability:** Code for humans. Write highly readable, consistent code that is easy to visually parse and iteratively tweak. Unpack nested ternaries and dense one-liners into simple, logical steps. Functions must do exactly one thing.
*   **Naming:** Use descriptive, conversational names. **NO abbreviations** (e.g., avoid `idx`, `err`, `req`).
*   **Safety:** Apply strict type hints and defensive `try/catch` blocks.
*   **Clean Execution:** Output production-ready code. Silently remove dead logic, unused variables, and debug logs (e.g., `console.log`). Do not explain the cleanup.

## 🧱 Clean Full-Stack Layer Decoupling
To achieve a highly maintainable, distributed, and scalable production architecture, we strictly decouple the three pillars of the stack:
*   **Decoupled Backend API:** The core backend application operates strictly as a stateless Web API. It handles authentication, data validation, authorization, and exposes clean REST endpoints. It must remain completely decoupled from frontend hosting and never block web requests with heavy, synchronous tasks.
*   **Independent Frontend SPA:** The frontend operates as a fully isolated Single Page Application (SPA). It maintains its own build pipeline, static asset generation, and routing, communicating with the backend exclusively via environment-configured API calls.
*   **Isolated Service & Background Worker:** All heavy background tasks, database synchronizations, scheduled crons, and CPU-intensive operations are delegated to a separate, dedicated Service/Daemon runner (such as isolated background CLI tasks or Worker Services). The Backend API never runs these tasks inline; instead, it triggers them asynchronously or queues them, ensuring the web server is never starved of thread-pool resources.

## 🌐 Public Web & Production Best Practices
*   **Asset Hashing & Caching:** Always keep content hashing enabled (e.g., `[name]-[hash].[ext]`) for production builds. This ensures immediate cache-busting when deploying updates to public web servers or CDNs, preventing users from loading stale files.
*   **Performance & Code Splitting:** Do not disable modular code splitting. Sub-components and widgets must be lazy-loaded dynamically to minimize initial bundle size and optimize Core Web Vitals.
*   **Decoupled Assets & Servicing:** Never embed frontend production assets (JS, CSS, images) directly as C# assembly resources (`EmbeddedResource`). Serve compiled assets strictly from the filesystem (e.g., `wwwroot/` with `app.UseStaticFiles()`) or host them independently on static CDNs to keep DLLs clean and lightweight.

## 💻 Language Execution
*   **Frontend (Vue / TS):** ALWAYS use Composition API (`<script setup>`). Move complex logic to computed properties. Clear listeners/intervals in `onUnmounted`. Do not use `any`.
*   **Frontend (React / JS/TS):** Use Functional Components and modern hooks. Keep component state flat and avoid prop-drilling by extracting small, reusable visual components. 
*   **Frontend (Plain HTML & CSS):** Use semantic HTML5 tags. Keep CSS scoped, well-commented, and organized by layout (e.g., Grid/Flexbox), typography, and component states.
*   **Backend (C# / .NET):** Use `PascalCase` for standard methods. Favor simple `foreach`/LINQ over deep chaining for ultimate readability. Async methods must use the `Async` suffix and return `Task` or `Task<T>`.
*   **Side Framework (Java):** Follow standard Java conventions (camelCase for methods/variables, PascalCase for classes). Ensure data structures and DTOs align closely with the C# backend to maintain cross-language consistency.

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