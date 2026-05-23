# Master Global Agent Instructions

> This file lives in the SKILLS folder synced via Google Drive.
> It is the single source of truth for AI behavior across all workspaces and PCs.

---

## 🔴 MANDATORY: Apply All Skills Before Every Task

At the start of every conversation, you MUST read and fully apply every `.md` file
found in the same folder as this file (the SKILLS folder).

Currently defined skill files:
- `SKILL.md` — Verbose Full-Stack C# Backend & Vue 3 Frontend Workflow

Read each one now before proceeding with any task.

---

## Core Behavior Rules (Always Active)

### 1. Never Silently Skip Skills
If you cannot find or read a skill file, explicitly report it to the user before continuing.
Do not proceed with generic behavior when a skill file is expected.

### 2. Skills Override Defaults
If a skill file conflicts with your default coding style, the skill file always wins.
The user has deliberately crafted these rules for their codebase.

### 3. Verify Compliance Before Finishing
Before concluding any implementation task, run an internal checklist:
- [ ] All variable/method/class names comply with skill naming rules
- [ ] No abbreviations used (no `idx`, `err`, `res`, `req`, `db`, `ctrl`, `mgr`, etc.)
- [ ] All async C# methods use `Async` suffix and return `Task` / `Task<T>`
- [ ] All Vue components are multi-word and descriptive
- [ ] All TypeScript interfaces are explicitly defined — no `any`

---

## Stack Reference

| Layer       | Technology              |
|-------------|-------------------------|
| Backend     | C# .NET (ASP.NET Core)  |
| Frontend    | Vue 3 + TypeScript      |
| Database    | SQLite (per-tenant)     |
| Project     | BIMASAKTI Reports       |
