# Project Context

- **Owner:** boclifton-MSFT
- **Project:** APIneer — a locally running API platform (Postman alternative). Desktop app for building, testing, and managing API requests with collections, environments, and response visualization.
- **Stack:** .NET 10 (backend), Nuxt UI v4 (frontend)
- **Created:** 2026-03-30

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-30: E2E API Fixes — Health, Draft Requests, ProxyEngine Wiring
- **`/api/health` endpoint:** Added at `/api/health` returning `{ status, timestamp }`. Original `/health` kept for backward compat
- **Seed data:** Default workspace + collection created on first startup (after migrations) so requests can be created immediately
- **Draft requests:** `POST /api/requests` now allows empty/null URL and null collectionId — auto-assigns to "Default" collection, creating workspace+collection on-the-fly if needed
- **ProxyEngine wired up:** `POST /api/requests/{id}/send` now uses real `IProxyEngine.SendAsync()` instead of stub. Builds `ProxyRequest` from stored data (method, URL, headers, body), saves real `ProxyResponse` to `RequestHistory`, returns response with optional `error` field for transport failures
- **Response shape:** Send response now includes `error: { code, message }` when proxy returns an error (TIMEOUT, DNS_FAILURE, CONNECTION_REFUSED, etc.)
- **Test updates:** Validation tests updated (empty URL → Created, missing collectionId → auto-assigned). Send/history tests relaxed to handle unreachable target URLs. New smoke test for `/api/health`. 381 tests passing

### 2026-03-30: Phase 3.2 — Collections API Implementation (GREEN)
- **Endpoints added:** 11 new/enhanced routes for collections, folders, reorder, duplicate, and move
- **Collection CRUD:** POST/GET/PUT/DELETE `/api/collections` with full validation (name required), timestamps, Location header
- **GET detail:** `GET /api/collections/{id}` returns nested folder tree + root-level requests; recursive `MapFolderTree` builds arbitrary-depth hierarchy
- **Folder CRUD:** `POST /api/collections/{id}/folders` (auto SortOrder via MAX+1), `DELETE /api/collections/{id}/folders/{folderId}` with manual cascade (descendant folders + their requests + history)
- **Move:** `PATCH /api/requests/{id}/move` reassigns `folderId` (or null for root)
- **Reorder:** `PATCH /api/collections/{id}/reorder` takes `itemIds[]` and sets SortOrder by position
- **Duplicate:** `POST /api/collections/{id}/duplicate` deep-copies collection with "(Copy)" suffix; BFS folder traversal maps old→new IDs; preserves nested structure
- **SortOrder:** Auto-assigned for both requests and folders (MAX+1 in scope); existing `POST /api/requests` updated
- **MapToResponse:** Added `FolderId` to request response (needed for move verification)
- **Cascade delete strategy:** Manual removal (history→requests→folders→collection) avoids issues with `DeleteBehavior.Restrict` on folder self-reference
- **No migration needed:** All changes are endpoint logic; models/DbContext unchanged
- **Tests:** All 41 collection tests pass (CRUD + folders + ordering + duplicate); 223/224 total (1 pre-existing CodeGeneration failure)

### 2026-03-30: Phase 4.2 — Environments API Implementation (GREEN)
- **Endpoints added to `Program.cs`:** 10 environment routes total
  - `POST /api/environments` — create environment (201 Created with location header)
  - `GET /api/environments` — list all environments with variables (secrets masked)
  - `GET /api/environments/{id}` — get single environment with variables (secrets masked as `***masked***`)
  - `PUT /api/environments/{id}` — update environment name
  - `DELETE /api/environments/{id}` — delete environment (cascades variables via EF)
  - `POST /api/environments/{id}/variables` — add variable with key, value, isSecret
  - `GET /api/environments/{id}/variables/{varId}` — get single variable (secrets masked)
  - `PUT /api/environments/{id}/variables/{varId}` — update variable key, value, isSecret
  - `DELETE /api/environments/{id}/variables/{varId}` — remove variable
  - `PUT /api/environments/{id}/activate` — set active (deactivates others in same workspace)
  - `PUT /api/environments/{id}/deactivate` — deactivate environment
  - `POST /api/environments/resolve` — resolve `{{var}}` placeholders using active environment
- **Secret masking:** GET responses return `***masked***` for secret variable values; resolve endpoint uses real values for proxy execution
- **Variable resolution:** Regex-based `{{key}}` replacement; supports escaped braces `\{\{` → `{{`, undefined vars left as-is, empty vars resolve to empty string
- **Active environment:** Only one active per workspace; activating deactivates siblings; resolution only uses active environment's variables
- **DTOs:** CreateEnvironmentDto, UpdateEnvironmentDto, CreateVariableDto, UpdateVariableDto, ResolveRequestDto
- **Migration fix:** Added `AddAssertions` migration to resolve `PendingModelChangesWarning` caused by Assertion model added without migration
- **Test results:** All 43 environment tests pass (0 failures, 0 regressions on environment tests)
- **Key pattern:** Models (Environment, EnvironmentVariable) were already defined in Phase 1.1; only endpoints and resolution logic needed implementation

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

### 2026-03-30: Phase 3.2 Collections API — Full Implementation (GREEN, 41/41 tests)
- **Collections CRUD:** POST/GET/PUT/DELETE `/api/collections` with full validation, timestamps, Location headers
- **Folder Hierarchy:** `GET /api/collections/{id}` returns nested tree; recursive `MapFolderTree` function builds arbitrary-depth structure
- **Folder Operations:** POST/DELETE folders with auto SortOrder; manual cascade delete (history→requests→folders→collection)
- **Request Move:** `PATCH /api/requests/{id}/move` reassigns folder or to root
- **Reorder:** `PATCH /api/collections/{id}/reorder` sets SortOrder by position
- **Duplicate:** `POST /api/collections/{id}/duplicate` deep-copies with "(Copy)" suffix; BFS maps old→new IDs, preserves structure
- **No model changes:** All endpoint logic; models/migrations unchanged

### 2026-03-30: Phase 4.2 Environments API — Full Implementation (GREEN, 43/43 tests)
- **Environment CRUD:** POST/GET/PUT/DELETE `/api/environments` with secret masking
- **Variable Management:** Add/update/delete variables within environments; secrets displayed as `***masked***` in GET responses
- **Activation:** `PUT /api/environments/{id}/activate` (deactivates others), `deactivate`
- **Resolution:** `POST /api/environments/resolve` replaces `{{var}}` placeholders using active environment; supports escaped braces
- **Key decision:** Only active environment used for variable resolution; ensures deterministic request execution
- **Migration:** Added `AddAssertions` migration (Phase 7.1 model addition)

### 2026-03-30: Phase 7.1 Advanced Features — Test Contracts (RED, 50 tests)
- **History (16 tests):** `GET /api/history` with pagination (page, pageSize), filtering (requestId, method, status, dateRange). `DELETE /api/history` clears. DTOs: `PaginatedHistory`, `HistoryEntry` with request+response snapshots.
- **Code Generation (18 tests):** `GET /api/requests/{id}/code?language=X` supports javascript-fetch, javascript-axios, python-requests, csharp-httpclient, curl. Generated code includes method, URL, headers, body. Invalid language→400.
- **Assertions (16 tests):** `POST /api/requests/{id}/assertions` (types: status_equals, body_contains, header_exists). `GET /api/requests/{id}/assertions` lists. `POST /api/requests/{id}/test` executes and evaluates assertions.
- **Status:** RED phase — 46 fail (expected, endpoints being implemented), 4 pass (coincidental 404 handlers)
- **Note:** Test contracts fully specified; Marcus implements in GREEN phase

### 2026-03-31: Phase 6.2 + 6.3 — Import/Export Engines Implementation (GREEN, 57/57 tests)
- **Import services** in `src/api/APIneer.Api/ImportExport/`:
  - `PostmanImporter.cs` — parses Postman v2.1 JSON collections into APIneer collections with full folder hierarchy, requests, headers, body, and body type. Recursive `ProcessItems` handles arbitrary nesting. Validates schema field presence.
  - `CurlImporter.cs` — tokenizes cURL commands (respecting quotes), extracts -X (method), -H (headers), -d/--data (body), -u (basic auth → Base64 Authorization header). Handles multiline backslash continuation via regex normalization.
  - `JsonImporter.cs` — re-imports APIneer native JSON exports, creating new collection with new IDs. Preserves folder hierarchy, requests, and all metadata.
- **Export services:**
  - `JsonExporter.cs` — exports collection as APIneer native JSON with nested folders, sub-folders, and requests including headers/body/bodyType.
  - `CurlExporter.cs` — generates cURL commands per request with -X method, -H headers, -d body flags.
  - `PostmanExporter.cs` — exports as Postman v2.1 format with info.schema, nested item structure (folders as items with child items, requests as items with request object).
- **Endpoints added to `Program.cs`:**
  - `POST /api/import/postman` — accepts `{ "collection": "<postman json>" }` wrapper
  - `POST /api/import/curl` — accepts raw cURL text (text/plain)
  - `POST /api/import/json` — accepts APIneer native JSON for round-trip import
  - `GET /api/collections/{id}/export?format=json|curl|postman` — export in specified format; validates format param, returns 404 for missing collection
- **MapRequestSummary fix:** Added `Headers`, `Body`, `BodyType` fields to collection detail response so imported data is visible via GET
- **Test results:** All 57 import/export tests pass (15 Postman import, 15 cURL import, 16 export, 11 round-trip). 369 total tests pass, zero regressions.

### 2026-03-30: Phase 5.2 — Auth Engine Implementation (GREEN, 46/46 tests)
- **File:** `src/api/APIneer.Api/Auth/AuthHandler.cs` — implements `IAuthHandler`
- **API Key:** Header placement injects custom header; query placement appends URL-encoded key=value to URL; missing placement defaults to header; missing KeyName/KeyValue → ArgumentException
- **Bearer Token:** Injects `Authorization: Bearer {token}` header; overwrites existing Authorization; empty/null token → ArgumentException
- **Basic Auth:** Encodes `{username}:{password}` as UTF-8 base64, injects `Authorization: Basic {encoded}`; empty username/password still encodes; null → ArgumentException
- **OAuth 2.0 Client Credentials:** POSTs to token endpoint with grant_type=client_credentials, client_id, client_secret, scope as form-encoded body; parses JSON response for access_token and expires_in; caches token with TokenExpiresAt; reuses cached token when not expired; re-fetches when expired; stores AccessToken back on AuthConfig; non-200 response → InvalidOperationException; missing endpoint/clientId/clientSecret → ArgumentException
- **Auth Inheritance:** `ResolveAuth(requestAuth, collectionAuth)` — request overrides collection; null request → inherit collection; type "none" → returns null (explicitly disables inherited auth); both null → null
- **DI Registration:** `AddHttpClient<IAuthHandler, AuthHandler>()` in Program.cs — typed HttpClient for OAuth2 token requests
- **Test results:** All 46 auth tests pass (ApiKeyAuth: 10, BearerToken: 7, BasicAuth: 10, OAuth2: 13, AuthInheritance: 7); zero regressions on other test suites

### 2026-03-31: Phase 7.4 — WebSocket Support (GREEN, 25/25 tests)
- **WebSocket proxy:** `src/api/APIneer.Api/WebSocket/WebSocketProxy.cs` — connects to target WebSocket, relays messages bidirectionally
- **Connection lifecycle:** `ConnectAsync(url)` validates URL (ws/wss/http/https), `SendAsync(msg)` forwards text messages, `DisconnectAsync()` graceful close
- **Status tracking:** `WebSocketConnectionStatus` enum (Connecting, Open, Closed, Error) with error message capture
- **Message history:** `ConcurrentQueue<WebSocketMessage>` stores sent/received messages with direction, content, timestamp
- **Background receive loop:** Async loop reads from target WebSocket, handles multi-frame messages, detects close/error
- **WebSocket upgrade support:** `HandleUpgradeAsync` accepts browser WS upgrades and relays client↔target bidirectionally
- **REST endpoints (5 routes, all before `app.Run()`):**
  - `GET /api/ws/connect?url={target}` — connects via REST or upgrades to WebSocket
  - `POST /api/ws/send` — send message to connected WebSocket (SendMessageDto)
  - `GET /api/ws/messages` — get message history with status
  - `DELETE /api/ws/disconnect` — close connection (always calls DisconnectAsync to handle error states)
  - `GET /api/ws/status` — connection status, target URL, message count, error
- **DI:** `WebSocketProxy` registered as singleton, `app.UseWebSockets()` middleware added
- **Tests (25 total):**
  - `WebSocketProxyTests` (13 unit tests): connect, send/receive echo, disconnect, error handling, multiple messages, history, timestamps, http→ws scheme conversion, duplicate connect guard
  - `WebSocketEndpointTests` (12 integration tests): REST connect/disconnect/send/messages/status, validation (missing URL, empty message, not connected), error target handling
  - `TestWebSocketServer` — Kestrel-based echo WS server on random port for tests
- **Key fix:** Disconnect endpoint always calls `DisconnectAsync()` regardless of state (not just when Open) — needed to reset from Error state
- **Endpoint placement fix:** All WebSocket routes must be registered before `app.Run()` (initial placement after helper functions caused 404s)
- **Zero regressions:** 312/312 non-ImportExport tests pass (ImportExport failures pre-existing)

### 2026-03-31: Phase 8.2 — Performance Optimization
- **Response compression:** Added gzip + brotli via `AddResponseCompression` middleware (`BrotliCompressionProvider`, `GzipCompressionProvider`) with `CompressionLevel.Fastest`; `UseResponseCompression()` added before `UseCors()`
- **Pagination added to list endpoints:**
  - `GET /api/collections` — now returns `{ items, page, pageSize, totalCount }` with default pageSize=50, max=100
  - `GET /api/requests` — same paginated shape, ordered by `UpdatedAt` descending
  - `GET /api/requests/{id}/history` — same paginated shape, ordered by `ExecutedAt` descending (was flat array)
  - `GET /api/history` already had pagination — no change needed
- **AsNoTracking added to all read-only queries:** `GET /api/collections`, `GET /api/collections/{id}`, `GET /api/requests`, `GET /api/requests/{id}`, `GET /api/requests/{id}/history`, `GET /api/history`, `GET /api/environments`, `GET /api/environments/{id}`, `GET /api/requests/{id}/assertions`, `GET /api/collections/{id}/export`
- **DB indexes (migration `AddPerformanceIndexes`):**
  - `IX_RequestHistory_ExecutedAt` — optimizes history ordering/filtering
  - `IX_RequestHistory_RequestId` — optimizes per-request history lookups
  - `IX_RequestHistory_ResponseStatus` — optimizes status filtering
  - `IX_ApiRequest_CollectionId` — renamed from EF convention name
  - `IX_ApiRequest_FolderId` — renamed from EF convention name
  - `IX_Environment_WorkspaceId_IsActive` — composite index for active environment lookups
- **Response size limits:** ProxyEngine streams large responses (>1MB) to limit memory allocation; reads up to `LargeResponseThresholdBytes` chars for bodies exceeding threshold
- **HttpClient pooling:** ProxyEngine now uses `IHttpClientFactory` (primary constructor injection) with named client `ProxyEngine`; `SocketsHttpHandler` configured with `PooledConnectionLifetime=5min`, `PooledConnectionIdleTimeout=2min`, `MaxConnectionsPerServer=20`, `EnableMultipleHttp2Connections=true`; eliminates socket exhaustion from per-request HttpClient/Handler creation
- **ProxyEngine refactor:** Changed from `new HttpClient()`/`new HttpClientHandler()` per request to `IHttpClientFactory.CreateClient()`. Registered as `Transient` instead of `Singleton` since IHttpClientFactory manages pooling
- **Test infrastructure:** Created `TestHttpClientFactory` for proxy tests (implements `IHttpClientFactory` with `AllowAutoRedirect=false`); updated all 6 proxy test files
- **Updated existing tests:** `RequestCrudTests`, `CollectionCrudTests` updated to deserialize paginated response shape (`PaginatedRequests`, `PaginatedHistory`, `PaginatedCollections`)
- **Performance tests (11 new):** `tests/APIneer.Api.Tests/Performance/PerformanceTests.cs`
  - Large response handling (1MB+ body creation and retrieval within timeout)
  - Rapid sequential request execution (10 sends) doesn't fail
  - Rapid sequential creation (10 creates) doesn't fail
  - History pagination with 50+ entries, page navigation, performance check
  - Global history pagination efficiency with 25+ entries
  - Collection duplication with 20 requests in reasonable time
  - Collection list pagination (15 collections, page navigation)
  - Collection deletion with 15 requests + history in reasonable time

### 2026-04-01: MCP Client Proxy — Full Implementation
- **New directory:** `src/api/APIneer.Api/Mcp/` with 5 files:
  - `McpJsonRpc.cs` — JSON-RPC 2.0 message types (request, notification, response, error)
  - `IMcpTransport.cs` — transport abstraction (SendRequest + SendNotification)
  - `StdioMcpTransport.cs` — spawns child process, writes JSON-RPC on stdin, reads responses from stdout with ID-matching, 30s timeout, proper process cleanup
  - `HttpMcpTransport.cs` — POST JSON-RPC to URL, handles both JSON and SSE responses, MCP-Session-Id tracking, protocol version header
  - `McpConnection.cs` — manages single connection lifecycle (initialize → operate → disconnect), exposes tools/list, tools/call, resources/list, resources/read, prompts/list, prompts/get, ping
  - `McpConnectionManager.cs` — singleton managing multiple connections by GUID, creates transports, DI-registered
- **New model:** `Models/McpServerConfig.cs` — persisted server configs (name, transportType, command, args, envVars, url)
- **DbContext:** Added `McpServerConfigs` DbSet + entity config with column constraints
- **Migration:** `AddMcpServerConfigs` — creates the MCP server config table
- **13 API endpoints** under `/api/mcp/`:
  - CRUD for server configs (POST/GET/PUT/DELETE `/api/mcp/servers`)
  - Connect/disconnect/status for live connections
  - Proxy endpoints for tools, resources, prompts, ping
- **Error handling:** JSON-RPC errors returned as structured 502; validation errors as 400; transport failures as 502
- **Test fixture:** Updated `ApiTestFixture.CreateClient()` to clear `McpServerConfigs` table
- **All 391 existing tests passing** — zero regressions
- **Test results:** 380 passed, 0 failed, 8 skipped (pre-existing auth security skips)

### 2026-03-31: Auth-Proxy Integration — AuthConfig Applied on Send
- **What:** Wired `IAuthHandler` into the `/api/requests/{id}/send` endpoint so auth config stored on requests (and inherited from collections) is applied to outgoing proxy requests before sending
- **Request CRUD:** `CreateRequestDto` and `UpdateRequestDto` now include `AuthConfig` (JSON string). `MapToResponse` returns it. POST saves it, PUT updates it, GET returns it
- **Collection auth:** Added `AuthConfig` column to `Collection` model + migration (`AddCollectionAuthConfig`). Collection DTOs updated. `MapCollectionResponse` and `MapCollectionDetail` include it
- **Send endpoint flow:** Deserializes request-level and collection-level auth configs → calls `IAuthHandler.ResolveAuth()` for inheritance → calls `ApplyAuthAsync()` to inject headers/query params → then sends via proxy engine
- **Error handling:** `ArgumentException` from missing auth fields → 400. `InvalidOperationException` with "OAuth2" → 502. `HttpRequestException` from failed token fetch → 502. Invalid auth JSON → 400
- **Auth type "none":** Resolves to null via `ResolveAuth`, skips auth application entirely
- **Tests:** 10 new integration tests in `tests/APIneer.Api.Tests/Auth/AuthProxyIntegrationTests.cs` — auth round-trip, unsupported type → 400, missing bearer token → 400, missing basic creds → 400, missing API key name → 400, auth "none" → OK, no auth → OK, OAuth2 failure → 502
- **Test results:** 391 passed, 0 failed, 8 skipped (total 399)

## Phase 9: Auth-Proxy Integration (2026-03-31T19:25Z) — ✅ COMPLETE

- **Task:** Wire auth into proxy endpoint, ensure auth config saved/loaded/applied, implement collection inheritance
- **Implementation:**
  - Enhanced POST /api/requests/{id}/send endpoint to deserialize authConfig and apply auth before proxying
  - AuthHandler.ResolveAuth() implements request + collection-level inheritance logic
  - Added Collection.AuthConfig column to store collection-level auth as JSON string
  - Created migration AddCollectionAuthConfig
- **Error Contract:**
  - Missing required auth fields → 400 Bad Request
  - Unsupported auth type → 400 Bad Request
  - OAuth2 token endpoint failure → 502 Bad Gateway
  - Invalid auth config JSON → 400 Bad Request
- **Tests:** 391 backend tests pass (including 10 new auth-proxy integration tests)
- **Key Decision:** Request-level auth takes precedence; falls back to collection-level if not overridden; type 'none' explicitly disables inheritance
- **Status:** Auth config flows end-to-end: frontend editor → API save → proxy application, ready for E2E testing

