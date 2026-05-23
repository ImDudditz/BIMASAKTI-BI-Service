# Workflow: Humanize & Refactor

## Mission
Analyze the provided code and rewrite it to be safe, performant, and incredibly easy to read. Maintain 100% feature parity. Your goal is to translate "robot code" or "spaghetti code" into a clean, pragmatic human style.

## Execution Steps
1. **Analyze:** Identify architectural flaws, missing type hints, unsafe operations, or overly complex logic.
2. **De-jargonize & Simplify:** 
   - Unpack nested ternary operators and dense one-liners into simple `if/else` statements or sequential steps.
   - Rename obscure variables into plain English.
3. **Upgrade & Defend:** 
   - Apply strict type hints (C#/Python/TS).
   - Add defensive `try/catch` blocks that handle errors gracefully and provide clear console warnings for future developers.
4. **Output:** Provide the complete, refactored code block. Include a brief, friendly summary of how you made the code easier to read and safer to run.