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

### 2026-03-30: Phase 2.4 — ProxyEngine Implementation (GREEN)
- **File:** `src/api/APIneer.Api/Proxy/ProxyEngine.cs` — implements `IProxyEngine`
- **Design:** Creates `HttpClientHandler` per-request with `AllowAutoRedirect = false` so redirects are handled manually — this lets us capture redirect chains and disable following per-request
- **HTTP methods:** GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS all supported
- **Redirect handling:** Manual loop: follows 301/302/303 as GET, 307/308 preserves method; captures chain when `CaptureRedirectChain = true`; returns raw 3xx when `FollowRedirects = false`
- **Error contract:** Never throws — catches `OperationCanceledException` (TIMEOUT), `HttpRequestException` with `SocketException.ConnectionRefused` (CONNECTION_REFUSED), DNS failures via `HttpRequestError.NameResolutionError` or `SocketError.HostNotFound` (DNS_FAILURE), invalid URLs (INVALID_URL), and catch-all (REQUEST_ERROR)
- **Timing:** `Stopwatch` wraps the entire request lifecycle, reported even on errors
- **Size:** Calculated as UTF-8 body bytes + serialised header bytes
- **Security:** 10MB request body limit enforced, configurable timeout (1–300s, default 30s)
- **DI:** Registered as `Singleton<IProxyEngine, ProxyEngine>` in Program.cs (stateless, safe to share)
- **Tests:** All 35 proxy tests pass (Success, Header, Body, Timing, Error, Redirect suites)

### 2026-03-30: Phase 2.2 — Request API Implementation (GREEN)
- **Endpoints:** 7 routes — POST/GET/PUT/DELETE `/api/requests`, POST `/api/requests/{id}/send`, GET `/api/requests/{id}/history`
- **Seed endpoints:** POST `/api/workspaces` and POST `/api/collections` for test FK data
- **Validation:** Empty/missing URL→400, invalid HTTP method→400, missing name→400, missing collectionId→400, body>10MB→413
- **Valid HTTP methods:** GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS (case-insensitive in request, stored uppercase)
- **Send endpoint:** Records `RequestHistory` entry with method, URL, timing, response data per execution
- **DTOs:** CreateRequestDto, UpdateRequestDto defined as records at bottom of Program.cs
- **Test isolation fix:** ApiTestFixture.CreateClient() override clears all DB tables between tests (xUnit shares fixture state within class)
- **Tests:** All 53 Request tests pass, 90/90 total (zero regressions)

## Cross-Agent Context (Phase 2)

### Freeman — Test Contracts (RED)
- **Request API:** 53 tests (31 fail, 22 pass) — CRUD endpoints, validation rules, RequestHistory
- **Proxy Engine:** 35 tests — HTTP methods, headers, body types, timing, error handling, redirects
- **Builder UI:** 36 tests (all RED) — MethodSelector, UrlInput, HeadersEditor, BodyEditor, RequestBuilder
- **Response UI:** 35 tests (all RED) — StatusBadge, ResponseBody, ResponseHeaders, ResponseTiming, ResponseViewer
- **Test infrastructure:** Fixed FluentAssertions v8 API changes in proxy tests (BeGreaterOrEqualTo→BeGreaterThanOrEqualTo)

### Kratos — Builder UI (GREEN)
- **5 Vue components:** MethodSelector (7 tests), UrlInput (6 tests), HeadersEditor (7 tests), BodyEditor (9 tests), RequestBuilder (7 tests)
- **41 tests pass** — all RED contracts satisfied
- **Locations:** `~/components/request-builder/*.vue`
- **Test infrastructure:** @vue/test-utils with `data-testid`, Vitest 4, @nuxt/test-utils (environment: 'nuxt'), MSW 2

### Ralph — Response UI (GREEN)
- **5 Vue components:** StatusBadge (8 tests), ResponseBody (9 tests), ResponseHeaders (6 tests), ResponseTiming (5 tests), ResponseViewer (7 tests)
- **82 tests pass** — all RED contracts satisfied
- **Key features:** Status color-coding (2xx/3xx/4xx/5xx), JSON pretty-print, Pretty/Raw tabs, copy-to-clipboard, alphabetical header sort, human-readable sizing (B/KB/MB)
- **Locations:** `~/components/response/*.vue`
- **Test infrastructure:** `mountSuspended` from @nuxt/test-utils/runtime with `data-testid`

### Dev Environment (Marcus)
- **Root package.json:** concurrently, README, .gitignore
- **Both services:** `npm start` or individual dev commands

## Cross-Agent Context (Phase 1)

### Frontend (Kratos)
- **Location:** `src/ui/` — Nuxt 4.4.2 + Nuxt UI v4
- **API proxy:** Routes `/api/**` → backend on `localhost:5000`
- **Port:** 3000
- **Critical:** Vitest config MUST use `defineVitestConfig` from `@nuxt/test-utils/config` (not plain vitest defineConfig)
- **State:** Pinia for global state management
- **Validation:** Zod for schema validation

### 2026-03-30: Phase 2.2 — Request API Endpoints (GREEN)
- **Endpoints added to `Program.cs`:** POST/GET/PUT/DELETE `/api/requests`, plus `/api/requests/{id}/send` and `/api/requests/{id}/history`
- **Seed endpoints:** POST `/api/workspaces` and POST `/api/collections` — minimal endpoints so tests can seed FK data
- **Validation:** Empty/missing URL → 400, invalid HTTP method → 400, missing name → 400, missing collectionId → 400, body > 10MB → 413
- **Valid methods:** GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS (case-insensitive comparison, stored uppercase)
- **Send endpoint:** Stubbed with mock 200 response — proxy engine (`IProxyEngine`) exists but isn't wired to the endpoint yet (tests hit unreachable URLs)
- **History:** Each `/send` call records a `RequestHistory` entry with method, URL, timing, and response data
- **DTOs:** `CreateRequestDto`, `UpdateRequestDto`, `CreateWorkspaceDto`, `CreateCollectionDto` — all defined as records at bottom of Program.cs
- **Test isolation fix:** Added `CreateClient()` override to `ApiTestFixture` that clears all DB tables between tests — xUnit shares fixture state within a class, so the "empty list" test failed when other tests created data first
- **Test results:** 53 Request tests pass, 90 total tests pass (zero regressions)

### Security Architecture (Payne)
- **Pattern:** Auth header injection (credentials resolved → headers injected → request sent → headers stripped from response)
- **Encryption:** DPAPI for secrets at rest in SQLite
- **Rule:** Secrets ONLY decrypted backend-side at request execution; frontend receives masked representation
- **7 testable invariants:** Raw secrets never in responses, masked in UI, encrypted storage, header sanitization, collection sanitization, no plaintext in logs, scoping respected
- **Location:** `docs/security-architecture.md`
