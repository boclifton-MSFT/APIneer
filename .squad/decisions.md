# Squad Decisions

## Active Decisions

### Stack Decision (2026-03-30T21:41:04Z)
**By:** boclifton-MSFT (via Copilot)  
**Decision:** APIneer will use .NET 10 for the backend API and Nuxt UI v4 for the frontend. SQLite for local data persistence. No desktop shell — runs as localhost web app.  
**Rationale:** User request — explicit stack choice.

### TDD Directive (2026-03-30T21:41:04Z)
**By:** boclifton-MSFT (via Copilot)  
**Decision:** All development must follow Test-Driven Design (TDD). Freeman writes failing tests FIRST, then Marcus/Kratos implement to make them pass. Red → Green → Refactor cycle on every feature. No feature ships until all tests pass.  
**Rationale:** User request — captured for team memory. User wants bulletproof, resilient software with comprehensive test coverage.

### Backend Scaffold Architecture (2026-03-30)
**By:** Marcus  
**Decision:** Backend uses .NET 10 Minimal API pattern with EF Core + SQLite. Solution file is `APIneer.slnx` (new .NET 10 default format). API listens on `localhost:5000`. Test project uses `WebApplicationFactory<Program>` with in-memory SQLite, swapping the real DB. Initial migration `InitialCreate` covers all 7 entity types. Swagger is always on (no environment gate). Auto-migrate on startup.  
**Rationale:** Minimal API keeps things lean for a local-first desktop tool. In-memory SQLite in tests avoids file I/O and gives full isolation per fixture. Auto-migrate on startup ensures the DB is always current without user intervention.

### Proxy Engine Contract and Test Infrastructure (2026-03-30)
**By:** Freeman  
**Decision:** Defined the HTTP proxy engine contract via 35 TDD Red tests. Created `IProxyEngine` interface and DTOs (`ProxyRequest`, `ProxyResponse`, `ProxyError`, `RedirectEntry`) in `src/api/APIneer.Api/Proxy/`. Tests in `tests/APIneer.Api.Tests/Proxy/` use a `TestHttpServer` helper (raw `HostBuilder` + Kestrel on port 0) for integration testing. Key design decision: the proxy engine returns structured `ProxyError` objects instead of throwing exceptions, so the frontend always gets a renderable result. Error codes: `TIMEOUT`, `CONNECTION_REFUSED`, `DNS_FAILURE`, `INVALID_URL`, `REQUEST_ERROR`.  
**Rationale:** TDD Red phase — tests define the proxy contract before implementation. Structured errors over exceptions ensure the UI never crashes on network failures. The `TestHttpServer` approach gives real HTTP integration testing without external dependencies. Security constraints (10MB body limit, 30s default timeout, no SSRF protection) are baked into the test expectations per `docs/security-architecture.md`.

### Request API Contract Defined by Tests (2025-07-16)
**By:** Freeman (Tester)  
**Decision:** Defined the full Request CRUD API contract via failing tests. Endpoints: `POST /api/requests` (201 Created), `GET /api/requests` (list), `GET /api/requests/{id}` (detail), `PUT /api/requests/{id}` (update), `DELETE /api/requests/{id}` (204), `POST /api/requests/{id}/send` (execute), `GET /api/requests/{id}/history` (history list). Validation: empty URL→400, invalid method→400, missing name→400, missing collectionId→400, body>10MB→413. Valid methods: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS.  
**Rationale:** TDD Red phase — tests define the contract BEFORE implementation. Marcus builds to make these tests pass.

### Clipboard Mock in Test Setup (2025-07-17)
**By:** Kratos (Frontend)  
**Decision:** Added a clipboard stub to `tests/setup.ts` that redefines `navigator.clipboard` as a writable/configurable property. This allows test-level `Object.assign` mocking to work without modifying individual test files.  
**Rationale:** Freeman's ResponseBody tests use `Object.assign(navigator, { clipboard: { writeText } })` to mock clipboard. This fails in happy-dom because `navigator.clipboard` is getter-only. The stub solves this at setup time, unblocking all clipboard-dependent tests without per-test mocking.

### Frontend Scaffolding Architecture (2025-07-16)
**By:** Kratos  
**Decision:** Frontend project at `src/ui/` uses Nuxt 4.4 + Nuxt UI v4.6 + Pinia + Zod. Testing uses Vitest 4 + @nuxt/test-utils (environment: 'nuxt') + MSW 2. API proxy configured: `/api/**` → `http://localhost:5000/api/**`. Dashboard layout with collapsible sidebar is the primary layout.  
**Rationale:** Phase 1.2 scaffolding. Dashboard layout chosen because this is an API dev tool — sidebar nav + panel content is the right pattern. Nuxt environment required for `mountSuspended` to work in tests (happy-dom alone won't suffice).

### ApiTestFixture Resets DB Between Tests (2026-03-30)
**By:** Marcus (Backend Dev)  
**Decision:** Added a `CreateClient()` override to `ApiTestFixture` that clears all entity tables before returning the client. Since each xUnit test creates a new test class instance (calling `CreateClient()` in the field initializer), every test now starts with a clean DB. This ensures full isolation — no ordering dependencies or cross-test pollution.  
**Rationale:** xUnit `IClassFixture<ApiTestFixture>` shares a single in-memory SQLite DB across all tests in a class. Without reset, tests checking for empty state failed when other tests had already inserted records. DB clear on `CreateClient()` gives clean state per test without ordering guarantees.

### Security Architecture Foundation (2026-03-30)
**By:** Payne, Security Specialist  
**Status:** Active  
**Decision:** APIneer's security architecture is built on **encryption at rest + backend-only decryption + log sanitization**. No SSRF protection against localhost by design (tool IS for localhost testing).  
**Rationale:**
1. **Encryption at Rest (DPAPI)** — Protects against local file access attacks. Keys stored in OS-protected directories per-user, machine-bound.
2. **Backend-Only Decryption** — Frontend never holds raw secrets. Requests reference auth configs; backend resolves and injects headers. Response logs stripped before return to UI.
3. **No SSRF Protection** — APIneer's primary use case is testing localhost APIs and internal endpoints. Users explicitly construct requests. This is not a bug; it's a feature.
4. **Log Sanitization** — All logs use `[REDACTED]` for credentials. No raw tokens, API keys, or passwords appear anywhere (logs, responses, UI).

**Implementation Checklist (Phase 1.5):**
- [ ] Implement DPAPI wrapper for secret storage
- [ ] Add auth header injection logic (resolve → inject → strip)
- [ ] Implement log redaction across all request/response logging
- [ ] Write security tests for all 7 invariants
- [ ] Sanitize collection imports (no inline scripts)

## Security Review Findings (Phase 2.9)

### P2-001: Request/Response Secrets Stored Plaintext in History Logs — HIGH
**Location:** Program.cs `/api/requests/{id}/send` endpoint  
**Issue:** RequestHistory stores raw request/response without sanitization. Authorization headers, API keys, and request bodies may contain secrets, stored as plaintext in SQLite.  
**Violates:** Invariant 6: "No plaintext secrets in request logs"  
**Fix Required:** Implement header/body sanitization before storage. Redact Authorization, API-Key, X-API-Key, etc. with `[REDACTED]`.  
**Status:** 🔴 UNRESOLVED

### P2-002: EnvironmentVariable.Value Stored Plaintext — MEDIUM
**Location:** Models/EnvironmentVariable.cs  
**Issue:** Value property stored as string without DPAPI encryption. No encryption at rest.  
**Violates:** Invariant 3: "Credentials encrypted at rest"  
**Fix Required:** Implement DPAPI encryption layer. Store encrypted bytes, decrypt only on backend at request execution time.  
**Status:** 🔴 UNRESOLVED (Planned for Phase 1.5)

### P2-003: Verbose Error Messages Expose Request Context — MEDIUM
**Location:** ProxyEngine.cs lines 30, 59–65  
**Issue:** Error messages echo user input (URLs) and expose implementation details from exception messages.  
**Risk:** Stack trace information leak, unexpected echoing of user input  
**Fix Required:** Provide generic error messages to frontend; log detailed errors server-side.  
**Status:** 🟡 PARTIAL

### P2-004: No Audit Trail for Request Execution — LOW
**Location:** ProxyEngine.cs, Program.cs (entire /send endpoint)  
**Issue:** No logging or audit trail when requests execute. Cannot detect unauthorized or suspicious activity.  
**Fix Required:** Add server-side audit logging for request execution (method, URL, status, duration).  
**Status:** ⚪ OBSERVATION

### P2-005: ProxyEngine Integration Pending — INFO
**Location:** Program.cs `/send` endpoint  
**Status:** Stub currently returns hardcoded response. Real ProxyEngine.SendAsync() integration deferred.  
**Future Work:** When integrated, response must strip injected auth headers per Invariant 4.  
**Status:** ⚪ PLACEHOLDER

## Positive Security Findings
✅ ProxyEngine correctly enforces 10MB request size limit  
✅ Timeout configuration properly implemented (1-300s range)  
✅ SSRF design intentionally permissive — aligns with architecture  
✅ HTTP method validation present  
✅ URL parsing and validation robust  
✅ Max redirect limit (20) prevents DoS  
✅ Error handling never throws — returns structured ProxyError  
✅ Resource disposal correct (using statements, finally block)

## Auth Security Review Findings (Phase 5.4)

### P5-001: AuthConfig Serialization Exposes All Secrets (HIGH)
**Location:** `Auth/AuthConfig.cs`, `Models/ApiRequest.cs:17`  
**Issue:** AuthConfig stores plaintext secrets (Token, Username, Password, ClientSecret, AccessToken, KeyValue) that are serialized to JSON and stored in SQLite.  
**Violates:** Invariant 3 (Encryption at Rest)  
**Fix Required:** Store secrets via `ICredentialProtector.Encrypt()`, decrypt only at request execution time.  
**Status:** ✅ RESOLVED (Phase 8)

### P5-002: OAuth2 Token Caching Stores Plaintext AccessToken (HIGH)
**Location:** `Auth/AuthHandler.cs:137-144, 176, 178-181`  
**Issue:** Cached OAuth2 AccessToken stored plaintext in AuthConfig, which is serialized to SQLite.  
**Violates:** Invariant 3 (Encryption at Rest), Invariant 6 (No plaintext in logs)  
**Fix Required:** Implement stateful in-memory token cache outside of AuthConfig using ConcurrentDictionary + SemaphoreSlim.  
**Status:** ✅ RESOLVED (Phase 8)

### P5-003: Client Secret Sent in Plaintext HTTP Body (HIGH)
**Location:** `Auth/AuthHandler.cs:147-160`  
**Issue:** OAuth2 client credentials sent in plaintext form body to token endpoint without HTTPS validation.  
**Violates:** Secure-by-default principle  
**Fix Required:** Validate TokenEndpoint is HTTPS-only, add certificate validation.  
**Status:** ✅ RESOLVED (Phase 8)

### P5-004: API Key in Query String Not Redacted (MEDIUM)
**Location:** `Auth/AuthHandler.cs:86-91`  
**Issue:** API keys appended to URL as query parameters, logged in RequestHistory plaintext.  
**Violates:** Invariant 6 (No plaintext in logs)  
**Fix Required:** Implement response log sanitization to redact query parameters containing sensitive keys.  
**Status:** ✅ RESOLVED (Phase 8)

### P5-005: OAuth2 Error Response Echoed in Exception (MEDIUM)
**Location:** `Auth/AuthHandler.cs:164-168`  
**Issue:** OAuth2 error details echoed in exception message, exposing token endpoint structure.  
**Fix Required:** Generic error messages; log detailed errors server-side only.  
**Status:** ✅ RESOLVED (Phase 8)

### P5-006: AuthConfig Type Validation Too Permissive (MEDIUM)
**Location:** `Auth/AuthHandler.cs:28-51`  
**Issue:** Null auth type throws NullReferenceException; unknown types throw without logging.  
**Fix Required:** Validate type before calling ToLowerInvariant(); add whitelist validation.  
**Status:** ✅ RESOLVED (Phase 8)

### P5-007: No HTTPS Enforcement for OAuth2 Endpoint (MEDIUM)
**Location:** `Auth/AuthHandler.cs:160`  
**Issue:** TokenEndpoint not validated to be HTTPS; HttpClient has no certificate pinning.  
**Fix Required:** Validate TokenEndpoint starts with https://; add certificate validation.  
**Status:** ✅ RESOLVED (Phase 8)

### P5-008: AccessToken Mutability (LOW)
**Location:** `Auth/AuthHandler.cs:176`  
**Issue:** Public setter on AccessToken allows modification and cache poisoning.  
**Fix Required:** Use immutable token cache.  
**Status:** ✅ RESOLVED (Phase 8)

### P5-009: No Audit Logging for Token Refresh (INFO)
**Location:** `Auth/AuthHandler.cs` (entire OAuth2 flow)  
**Issue:** No logging for token lifecycle events (fetch, refresh, expiry).  
**Recommendation:** Add ILogger calls for token lifecycle.  
**Status:** ✅ RESOLVED (Phase 8)

## Regression Tests Written (Phase 5.4)

9 comprehensive tests written to validate auth security fixes:
- `AuthConfigEncryptionTests`: Verify secrets encrypted at rest in SQLite
- `OAuth2TokenCacheTests`: Verify token cache is in-memory only, not persisted
- `AuthHttpsEnforcementTests`: Verify TokenEndpoint validation for HTTPS
- `ResponseSanitizationTests`: Verify API keys redacted in request history
- `AuthErrorHandlingTests`: Verify generic error messages without exposure
- `AuthTypeValidationTests`: Verify whitelist validation for auth types
- `TokenRefreshRaceTests`: Verify SemaphoreSlim prevents concurrent token refreshes
- `HeaderStrippingTests`: Verify Authorization headers removed from stored history
- `AuditLoggingTests`: Verify token lifecycle events logged

All tests passing. Phase 1-8 complete.

### QueryParamsEditor URL Parsing (2026-03-31)
**By:** Kratos (Frontend)  
**Decision:** QueryParamsEditor uses manual query string parsing (`split('&')` + `decodeURIComponent`) instead of `URLSearchParams` for URL ↔ params sync. Handles `+` as space per form-urlencoded convention via `decodePart()` helper.  
**Rationale:** Manual parsing gives full control over edge cases (key-only params, duplicate keys, original ordering). `URLSearchParams` normalizes values in ways that can cause unwanted URL rewrites. Bidirectional sync uses `v-model:url` + `suppressUrlSync` flag to prevent recursive watch loops.  
**Impact:** Future URL/fragment/path param editors should follow this pattern. Reusable bidirectional sync pattern.

### Playwright E2E Testing Directive (2026-03-31)
**By:** boclifton-MSFT (via Copilot)  
**Decision:** Always use Playwright MCP to E2E test UI changes — open the site, click buttons, verify everything works visually. Don't rely solely on unit tests.  
**Rationale:** User request — captured for team memory. Ensures visual correctness beyond snapshot/unit coverage.

### Auth Config Frontend Wiring Pattern (2026-03-31)
**By:** Kratos (Frontend Dev)  
**Decision:** Auth config follows the same serialization pattern as headers: stored as a JSON string in the backend `ApiRequest.AuthConfig` field, parsed defensively on the frontend when loading a request, and serialized back to JSON string on save/send.  
**Details:**
- `RequestBuilder.vue` owns `authConfig` as reactive state (`{ type: 'none' }` default)
- On load: parse `request.authConfig` from JSON string (defensive — handles string, object, null, invalid JSON)
- On send/save: serialize to JSON string in the emit payload
- `index.vue` passes the serialized `authConfig` string through to `updateRequest()` before sending
- Backend `AuthHandler` handles deserialization and applies auth to outgoing proxy requests  
**Rationale:** Consistent with the existing `headers` pattern — JSON string transport between frontend and backend. Defensive parsing prevents crashes from malformed data. The backend owns auth application logic; frontend only manages the config editing and persistence.  
**Impact:** Future auth-related UI features (inherit from collection, per-folder auth) should follow this same pattern. Backend `AuthConfig` schema changes will require updating the `AuthEditor.vue` fields.

### Auth-Proxy Integration (2026-03-31)
**By:** Marcus (Backend Dev)  
**Decision:** The `/api/requests/{id}/send` endpoint now resolves and applies auth configuration before proxying. Auth flows through three layers:
1. **Request-level auth** — stored as JSON string in `ApiRequest.AuthConfig`
2. **Collection-level auth** — stored as JSON string in `Collection.AuthConfig` (new column + migration)
3. **Resolution** — `IAuthHandler.ResolveAuth()` picks request auth if present, falls back to collection auth, returns null if request explicitly sets type "none"  
**Error Contract:**
- Missing required auth fields (e.g. bearer with no token) → **400 Bad Request**
- Unsupported auth type → **400 Bad Request**
- OAuth2 token endpoint failure (network or HTTP error) → **502 Bad Gateway**
- Invalid auth config JSON → **400 Bad Request**  
**Impact:** Frontend (Kratos) can now send `authConfig` as JSON string in request create/update payloads. The send button will automatically apply auth. Collection-level auth is inherited by requests that don't override it. Tests (Freeman): 10 new integration tests cover the auth-proxy flow. All 391 tests pass. Security (Payne): Auth secrets still stored as JSON strings in SQLite — encryption at rest (P5-001) is a separate concern tracked in Phase 1.5.

### Form Data Serialization Format (2026-03-31)
**By:** Kratos (Frontend Dev)  
**Decision:** Form data in BodyEditor uses `application/x-www-form-urlencoded` serialization (`key1=value1&key2=value2`) as the `modelValue` string transport format. Internal state is an array of `{ key: string, value: string }` objects with bidirectional sync using the same `suppressSync` flag pattern established by QueryParamsEditor.  
**Rationale:** Matches the HTTP standard for `application/x-www-form-urlencoded` content type. Backend can use the serialized string directly as request body. Consistent with QueryParamsEditor's manual encoding approach. Handles special characters, `+` as space, and edge cases.  
**Impact:** Future multipart/form-data support (file uploads) will need a different approach. The `modelValue` string contract remains unchanged — all body modes emit a plain string.

### CollectionSidebar: Collapse/Expand + Inline Rename (2025-07-18)
**By:** Kratos (Frontend Dev)  
**Decision:** 
1. **Collapse/Expand** uses a reactive `Set<string>` of collapsed collection IDs. All collections start expanded. `v-show` (not `v-if`) hides content to preserve drag-drop DOM state. Chevron rotation uses CSS `transform: rotate(90deg)` with a 200ms transition.
2. **Inline Rename** reuses the existing `InlineRename.vue` component (double-click → input → Enter/Escape). The `rename-request` event bubbles from `CollectionSidebar` to `index.vue`, which calls `api.updateRequest()` to persist.

**Rationale:**
- `v-show` over `v-if`: drag-drop relies on DOM elements being present. `v-if` would destroy and recreate them, breaking mid-drag state.
- `InlineRename` reuse: avoids duplicating double-click-to-edit logic. Component already handles focus, select-all, blur-save, escape-cancel.
- Double-click for rename: single-click selects the request, drag initiates move. Double-click is the only safe trigger that doesn't conflict.

**Impact:** `CollectionSidebar` now emits `rename-request` — any parent consuming this component must handle it. `CollectionTreeFolder` does NOT yet support inline rename for folder-level requests. Future work if needed.

### MCP Client Proxy Architecture (2026-04-01)

**By:** Marcus (Backend Dev)  
**Status:** Active  
**Decision:** MCP client proxy uses a **singleton `McpConnectionManager`** that holds active `McpConnection` instances keyed by GUID. Connections are ephemeral (in-memory only), while server configs are persisted in SQLite via `McpServerConfig`. Two transport implementations: `StdioMcpTransport` (spawn process, JSON-RPC over stdin/stdout) and `HttpMcpTransport` (POST JSON-RPC to URL, supports SSE).

**Key Design Choices:**
1. **Transport abstraction (`IMcpTransport`)** — stdio and HTTP share the same interface. Adding new transports (e.g., WebSocket) only requires implementing the interface.
2. **Connection ≠ Config** — Server configs are CRUD'd independently. A connection references a config ID but can also be created inline without saving. This lets the frontend test servers before persisting.
3. **Request ID tracking** — stdio transport uses `ConcurrentDictionary<int, TaskCompletionSource>` to match async responses to requests. 30-second timeout per request.
4. **Error contract** — JSON-RPC errors from MCP servers are returned as structured `{ error: { code, message } }` with HTTP 502. Validation errors return 400. This matches the existing proxy error pattern.
5. **No background keep-alive** — connections live as long as the manager holds them. Frontend must reconnect if the server drops. Future enhancement: ping-based health checks.

**Impact:**
- Frontend (Kratos): 13 new `/api/mcp/*` endpoints available for MCP UI integration
- Tests (Freeman): MCP server config CRUD can be tested with standard API test patterns. Transport/connection tests will need mock MCP servers.
- Security (Payne): Stdio transport spawns child processes — environment variables may contain secrets. Review needed when secrets management is integrated.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
