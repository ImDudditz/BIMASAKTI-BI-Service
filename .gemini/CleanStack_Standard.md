# CleanStack Code Standards: Human-First Readability

## Core Philosophy
Write defensively, but code for humans first. The code must be so clean, logical, and straightforward that a junior engineer can read it top-to-bottom like a book. Avoid "clever" one-liners or over-engineered abstractions.

## Language Execution Rules
- **General Human Formatting:**
  - Group related logic together with natural whitespace (empty lines between "thought blocks").
  - Use clear, conversational variable names (e.g., `userList` instead of `usrLst_arr`).
  - Add friendly, brief comments explaining *why* a specific approach was taken if the logic is tricky.
- **Vue/Frontend:** 
  - ALWAYS use the Composition API (`<script setup>`).
  - Keep the template clean; move complex ternary operators or logic into readable `computed` properties.
  - ALWAYS clear intervals and event listeners in `onUnmounted`.
- **Python:** 
  - Enforce PEP-8 formatting, but prioritize readability over forced strictness.
  - Use explicit type hinting (`typing` module) so beginners know exactly what data is passing through.
  - Use natural `try/except` blocks that return helpful, human-readable error messages.
- **C# / .NET:** 
  - Follow standard PascalCase for methods. 
  - Favor straightforward `foreach` loops or simple LINQ queries over deeply chained, complex LINQ statements that are hard to decipher.
- **JavaScript/TypeScript:**
  - Avoid global variables. 
  - Use modern, readable syntax (`async/await` instead of nested `.then()` promise chains).