# Project Context

- **Owner:** boclifton-MSFT
- **Project:** APIneer — a locally running API platform (Postman alternative). Desktop app for building, testing, and managing API requests with collections, environments, and response visualization.
- **Stack:** .NET 10 (backend), Nuxt UI v4 (frontend)
- **Created:** 2026-03-30

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Model Preferences (2026-04-02)

**Decision:** Use `claude-opus-4.6-1m` for planning/architecture work and `claude-sonnet-4.6` for code review. Planning and security are important enough to warrant the biggest model capacity.

**Rationale:** User directive — captured for team memory. Opus provides superior reasoning for complex architectural decisions and long-context planning. Sonnet is optimized for focused code review and targeted improvements.

### Phase 8.4 — Documentation Complete (2026-03-30)
**What:** Completed comprehensive project documentation covering README, API reference, and architecture design.

**Key Documentation Artifacts:**
- **README.md** — Enhanced with project description, feature list, architecture overview, prerequisites, quick start, TDD workflow explanation, project structure, API overview, security model, configuration, testing guide, and contributing guidelines.
- **docs/api-reference.md** — Complete REST API endpoint documentation covering requests, collections, environments, history, assertions, code generation, import/export, and WebSocket APIs with examples and error handling.
- **docs/architecture.md** — System architecture with ASCII diagram, component overview (frontend & backend), data flow, entity relationships, technology choices, deployment model, performance considerations, and extensibility roadmap.

**Why:** APIneer is production-ready. Comprehensive documentation ensures new contributors can onboard quickly, users understand the system design, and maintainers have a single source of truth for API contracts and security constraints.

**Impact:** Documentation serves as:
1. Onboarding guide for new developers
2. API contract for frontend/backend teams
3. Reference for security architecture and encryption strategy
4. Foundation for future feature planning and extensions

