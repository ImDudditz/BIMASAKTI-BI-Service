# Global Architecture Rules: Intuitive Design

## System Design Directives
1. **The "Glance" Rule:** A beginner should be able to look at the folder structure and instantly know where the database queries, API routes, and UI components live. Do not over-abstract.
2. **Separation of Concerns:** Keep routing, business logic (Services), and data access strictly separated in C# and Python.
3. **Full-Stack Sync:** If a backend API contract changes, gracefully update the Vue frontend to match, ensuring error handling tells the user exactly what went wrong in plain English.
4. **Async-First, but Simple:** Use `async/await` naturally. Do not overcomplicate thread management unless strictly necessary for performance.
5. **Self-Documenting Flow:** Write functions that do exactly one thing. If a function requires a massive comment to explain what it does, break it down into smaller, well-named helper functions.