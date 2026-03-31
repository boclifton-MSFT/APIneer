# Project Context

- **Owner:** boclifton-MSFT
- **Project:** APIneer — a locally running API platform (Postman alternative). Desktop app for building, testing, and managing API requests with collections, environments, and response visualization.
- **Stack:** .NET 10 (backend), Nuxt UI v4 (frontend)
- **Created:** 2026-03-30

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Cross-Agent Context (Phase 2)

#### Marcus — Request API (GREEN)
- **7 endpoints implemented:** POST/GET/PUT/DELETE `/api/requests`, POST `/api/requests/{id}/send`, GET `/api/requests/{id}/history`
- **Validation:** Empty URL→400, invalid method→400, missing name→400, missing collectionId→400, body>10MB→413
- **RequestHistory:** Each send logs method, URL, timing, response data
- **DTOs:** CreateRequestDto, UpdateRequestDto (records at bottom of Program.cs)
- **Test isolation:** Added CreateClient() override to ApiTestFixture clearing DB between tests
- **Result:** 53/53 Request tests pass, 90/90 total (zero regressions)

#### Marcus — ProxyEngine (GREEN)
- **File:** `src/api/APIneer.Api/Proxy/ProxyEngine.cs` — implements IProxyEngine
- **HTTP methods:** GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS all supported
- **Redirect handling:** Manual loop: 301/302/303→GET, 307/308→preserve method, capture chain when flag set, raw 3xx when disabled
- **Error contract:** Structured errors (TIMEOUT, CONNECTION_REFUSED, DNS_FAILURE, INVALID_URL, REQUEST_ERROR) — never throws
- **Constraints:** 10MB body limit, 30s default timeout (1–300s range), no SSRF protection, Stopwatch timing
- **DI:** Singleton in Program.cs (stateless, safe to share)
- **Result:** 35/35 proxy tests pass

#### Kratos — Builder UI (GREEN)
- **5 components:** MethodSelector, UrlInput, HeadersEditor, BodyEditor, RequestBuilder
- **41 tests pass** — all RED contracts implemented
- **Test setup:** Vitest 4 + `@nuxt/test-utils` (environment: 'nuxt'), MSW 2 mocks, `data-testid` selectors

#### Ralph — Response UI (GREEN)
- **5 components:** StatusBadge, ResponseBody, ResponseHeaders, ResponseTiming, ResponseViewer
- **82 tests pass** — all RED contracts implemented
- **Status colors:** 2xx green, 3xx blue, 4xx orange, 5xx red, status-0 special case
- **ResponseBody features:** JSON pretty-print, Pretty/Raw tabs, copy-to-clipboard, large body handling
- **ResponseHeaders:** Alphabetical sort, count display, empty state

#### Dev Environment (Marcus)
- **Root package.json:** concurrently for backend + frontend
- **README:** Setup instructions
- **.gitignore:** Standard exclusions
- **Both services run:** `npm start` or individual `npm run dev:{backend|frontend}`

---

- **2026-03-30 — Proxy Engine test contracts defined (Phase 2.3):**Created 35 failing tests across 6 test files defining the HTTP proxy engine contract. Tests cover: success paths (all HTTP methods), headers (custom/response/Content-Type/User-Agent), body types (JSON/form/text/empty/large), timing (response time & size metrics), error handling (timeout/invalid URL/connection refused/DNS failure — all as structured errors, never exceptions), and redirects (follow by default, capture chain, disable following). Proxy DTOs defined: `ProxyRequest`, `ProxyResponse`, `ProxyError`, `RedirectEntry`, `IProxyEngine`. Key constraints from security doc: 10MB max body, 30s default timeout (1s–5min range), no SSRF protection by design, structured errors instead of exceptions. Test infrastructure uses a raw `HostBuilder` + Kestrel with port 0 for random port allocation — `WebApplication.CreateSlimBuilder()` fought us on default port binding. FluentAssertions v8 renames: `BeGreaterThanOrEqualTo` (not `BeGreaterOrEqualTo`), `HaveCountGreaterThanOrEqualTo`.

- **2025-07-16 — Phase 2.5 Request Builder UI tests (RED):** Created 5 failing test files in `src/ui/tests/components/` covering MethodSelector (7 tests), UrlInput (6 tests), HeadersEditor (7 tests), BodyEditor (9 tests), RequestBuilder (7 tests) — 36 tests total. All fail because components at `~/components/request-builder/*.vue` don't exist yet. Tests use `mount` from `@vue/test-utils` with `data-testid` attributes for reliable selectors. Test infrastructure uses Vitest 4 + `@nuxt/test-utils` (environment: 'nuxt') with MSW 2 for API mocking. Existing test at `tests/setup.ts` configures MSW server globally.

- **2025-07-16 — Phase 2.7 Response Viewer UI tests (RED):** Created 5 failing test files for Response Viewer components: StatusBadge (8 tests — status display, color-coding for 2xx/3xx/4xx/5xx, status-0 edge case), ResponseBody (9 tests — render, empty state, JSON pretty-print, Pretty/Raw tabs, copy-to-clipboard, large body handling), ResponseHeaders (6 tests — table rendering, alphabetical sort, header count, empty state), ResponseTiming (5 tests — ms display, human-readable size in B/KB/MB, compact layout), ResponseViewer integration (7 tests — tabs, default tab, StatusBadge presence, empty state, child data passing). All 35 tests fail because components at `~/components/response/*.vue` don't exist yet. Used `mountSuspended` from `@nuxt/test-utils/runtime` consistent with Nuxt test conventions and `data-testid` attributes for selectors.

- **2025-07-16 — Phase 2.1 Request CRUD + validation tests (RED):** Created 4 files in `tests/APIneer.Api.Tests/Requests/`: TestData.cs (shared helper with sample payloads, JSON serialization, response DTOs), RequestCrudTests.cs (20 tests — POST create, GET list, GET by ID, PUT update, DELETE, POST send/execute, GET history), RequestValidationTests.cs (11 tests — empty URL→400, invalid method→400, missing name→400, missing collectionId→400, non-existent ID→404 for all endpoints, body>10MB→413 per security doc), RequestModelTests.cs (12 tests — default values, timestamps, headers JSON serialization round-trip, RequestHistory model). Total 53 tests: 31 fail (no endpoints exist — correct RED phase), 22 model tests pass. Also fixed pre-existing build errors in Proxy test files (FluentAssertions v8 API changes: `BeGreaterOrEqualTo`→`BeGreaterThanOrEqualTo`, `HaveCountGreaterOrEqualTo`→`HaveCountGreaterThanOrEqualTo`, `.Subject` removal on `AndConstraint`).
