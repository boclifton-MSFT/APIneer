# Project Context

- **Owner:** boclifton-MSFT
- **Project:** APIneer — a locally running API platform (Postman alternative). Desktop app for building, testing, and managing API requests with collections, environments, and response visualization.
- **Stack:** .NET 10 (backend), Nuxt UI v4 (frontend)
- **Created:** 2026-03-30

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-30: Phase 1.1 — Backend Scaffolding Complete
- **Solution:** `APIneer.slnx` (root) — .NET 10 uses `.slnx` format by default
- **API project:** `src/api/APIneer.Api/` — Minimal API pattern, listens on `localhost:5000`
- **Test project:** `tests/APIneer.Api.Tests/` — xUnit + FluentAssertions + NSubstitute + WebApplicationFactory
- **Data layer:** EF Core with SQLite (`apineer.db`), AppDbContext at `Data/AppDbContext.cs`
- **Models:** Workspace, Collection, CollectionFolder, ApiRequest, Environment, EnvironmentVariable, RequestHistory — all in `Models/`
- **Relationships:** Workspace→Collections, Workspace→Environments, Collection→Folders, Collection→Requests, CollectionFolder→self-referencing hierarchy, ApiRequest→RequestHistory, Environment→EnvironmentVariables
- **CORS:** Configured for `http://localhost:3000` (Nuxt frontend)
- **Swagger:** Enabled at `/swagger`, JSON at `/swagger/v1/swagger.json`
- **Health check:** `GET /health` returns `{ status: "healthy" }`
- **Test fixture:** `ApiTestFixture.cs` uses in-memory SQLite via shared connection, swaps out the real DB for tests
- **`Program` class:** Partial class declared at bottom of Program.cs to enable WebApplicationFactory access
- **Initial migration:** `InitialCreate` in `Data/Migrations/`
- **Pattern:** Auto-apply migrations on startup (`db.Database.Migrate()` in Program.cs)

## Cross-Agent Context (Phase 1)

### Frontend (Kratos)
- **Location:** `src/ui/` — Nuxt 4.4.2 + Nuxt UI v4
- **API proxy:** Routes `/api/**` → backend on `localhost:5000`
- **Port:** 3000
- **Critical:** Vitest config MUST use `defineVitestConfig` from `@nuxt/test-utils/config` (not plain vitest defineConfig)
- **State:** Pinia for global state management
- **Validation:** Zod for schema validation

### Security Architecture (Payne)
- **Pattern:** Auth header injection (credentials resolved → headers injected → request sent → headers stripped from response)
- **Encryption:** DPAPI for secrets at rest in SQLite
- **Rule:** Secrets ONLY decrypted backend-side at request execution; frontend receives masked representation
- **7 testable invariants:** Raw secrets never in responses, masked in UI, encrypted storage, header sanitization, collection sanitization, no plaintext in logs, scoping respected
- **Location:** `docs/security-architecture.md`
