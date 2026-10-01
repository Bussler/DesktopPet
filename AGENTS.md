##  General Workflow
Store relevant findings or thought processes in dedicated files to the session in a local .codex/ directory.

Always keep changes to the codebase lean and as short as possible without impacting correctness. Do not introduce unnecessary complexity unless there is a clear benefit.
A smaller and easier-to-understand solution is preferred over a more sophisticated one.

## Software Engineering and Architecture Principles

When making implementation or architecture decisions, prioritize:

1. Correctness
2. Simplicity
3. Maintainability
4. Testability
5. Reproducibility
6. Transparency
7. Extensibility

Prefer the smallest cohesive solution that meets the requirements. Avoid speculative abstractions, premature optimization, and complexity without a clear benefit.
Reuse existing abstractions before creating new ones.

## Clarification of Requirements

- Do not silently guess or invent requirements.
- If the user’s request is unclear, incomplete, contradictory, or open to materially different interpretations, explicitly ask what is meant or desired before implementing the affected work.
- Ask focused questions and briefly explain the relevant options or consequences when helpful.
- For minor ambiguity that does not materially affect the result, make a reasonable assumption and state it clearly.

## Design Guidelines

* Understand and follow the existing architecture and project conventions.
* Keep responsibilities clear, dependencies explicit, and components loosely coupled.
* Introduce abstractions only for a concrete boundary, repeated logic, or known variation.
* Prefer straightforward control flow and descriptive, domain-focused names.
* Avoid hidden global state, implicit behavior, and unnecessary side effects.
* Keep public interfaces small and treat APIs, schemas, configuration, and persisted data as contracts.
* Preserve backward compatibility unless a breaking change is explicitly required.
* Validate inputs at system boundaries and handle errors explicitly.
* Add dependencies only when their benefit outweighs their maintenance and operational cost.
* Optimize performance only for a stated requirement or measured problem.
* Consider security, data integrity, failure handling, and observability where relevant.

## Testing and Verification

* Add or update tests for changed behavior, edge cases, and relevant failure paths.
* Keep tests deterministic and add regression tests for bug fixes.
* Never weaken or bypass tests merely to make a change pass.

## Architecture Decisions

For significant or difficult-to-reverse decisions, document:

* Context and constraints
* Considered alternatives
* Selected approach
* Trade-offs and consequences
* Compatibility or migration requirements