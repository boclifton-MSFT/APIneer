# Project Context

- **Owner:** boclifton-MSFT
- **Project:** APIneer — a locally running API platform (Postman alternative). Desktop app for building, testing, and managing API requests with collections, environments, and response visualization.
- **Stack:** .NET 10 (backend), Nuxt UI v4 (frontend)
- **Created:** 2026-03-30

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### BodyEditor Form Data Tests (GREEN — Kratos beat the RED phase)
- **9 new form-data tests** added to `src/ui/tests/components/BodyEditor.test.ts` in a `describe('BodyEditor — Form Data mode')` block. All 18 tests (9 original + 9 new) pass GREEN.
- **Kratos landed the full implementation simultaneously** — `formdata-table`, `formdata-key-input`, `formdata-value-input`, `remove-formdata`, `add-formdata` were all present by first test run. Vitest transform cache served the old component version on the first two runs, causing 2 false-RED results; `--no-cache` confirmed 18/18 GREEN.
- **`nextTick` must be imported from `vue`**, not from `vitest`. Use `import { nextTick } from 'vue'` in test files.
- **Form data serialization contract:** `encodeURIComponent(key)=encodeURIComponent(value)` pairs joined by `&`. Empty key+value rows are filtered out before serializing. Parsing uses `decodeURIComponent` + `.replace(/\+/g, ' ')` for `+`-as-space handling.
- **Empty modelValue behaviour:** `parseFormData('')` returns `[{ key: '', value: '' }]` — one empty row, not zero rows. Test 7 allows either `length === 0` or `length === 1` with `value === ''`.
- **Special-characters test pattern:** After typing a value with spaces/`&`/`=`, decode the emitted value with `decodeURIComponent()` to assert the round-trip, and verify raw emitted string has no literal spaces.
- **Test patterns used:**
  - `wrapper.find('[data-testid="formdata-table"]').exists()` — presence check for form-data container
  - `(input.element as HTMLInputElement).value` — read input value after prop-driven render
  - `wrapper.findAll('[data-testid="formdata-key-input"]')` + index — access specific row inputs
  - `emitted![emitted!.length - 1][0] as string` — get last emitted value after sequence of inputs
  - `await wrapper.find('[data-testid="add-formdata"]').trigger('click'); await nextTick()` — add row then wait for reactivity before finding new inputs

### 2026-03-31 — AuthEditor Integration Tests (GREEN)
- **9 new RequestBuilder integration tests** in `src/ui/tests/components/RequestBuilder.test.ts` (6 auth integration + 1 existing total 13)
- **4 new AuthEditor edge case tests** in `src/ui/tests/components/AuthEditor.test.ts` (total 17)
- **All 30 tests GREEN** — Kratos had already wired AuthEditor by the time tests ran
- **authConfig serialization contract:** `authConfig` in the `send` emit payload is a JSON string (not an object) — matches `RequestData.authConfig?: string`. Tests parse with `JSON.parse()` before asserting.
- **Test patterns used:**
  - `clickAuthTab` helper to DRY up tab navigation before auth assertions
  - `wrapper.findComponent({ name: 'AuthEditor' }).vm.$emit('update:modelValue', ...)` to simulate child emitting v-model update — parent's `authConfig` ref updates correctly via `v-model`
  - `await nextTick()` after `vm.$emit` to allow Vue reactivity to propagate before asserting props
  - `wrapper.setProps({ modelValue: emitted })` in AuthEditor tests to simulate parent updating controlled component after type switch
  - `expect(emitted).not.toHaveProperty('token')` to verify field clearing when type switches
- **Edge cases covered:**
  - bearer→basic: emitted config has no token, has username/password; DOM updates after setProps
  - any→none: emitted config equals `{ type: 'none' }` exactly; all type-specific DOM fields gone
  - OAuth2 all 4 fields (tokenEndpoint, clientId, clientSecret, scope) emit individually correct values
  - API Key placement header→query: emitted includes placement='query' and preserves keyName/keyValue


### 2026-03-31 — QueryParamsEditor Tests (GREEN)
- **18 comprehensive tests** in `src/ui/tests/components/QueryParamsEditor.test.ts`
- **Test patterns used:**
  - `defineComponent` wrapper with `ref` for `v-model:url` bidirectional testing (differs from HeadersEditor array prop)
  - `mountWithUrl(initialUrl)` helper + `getUrl()` closure to read emitted URL — avoids wrapper.emitted() verbosity
  - `wrapper.findComponent(QueryParamsEditor)` + `editor()` accessor for scoped queries inside wrapper
  - `wrapper.vm.$nextTick()` for testing external prop-driven re-renders (bidirectional sync validation)
- **Edge cases covered (all 18 tests GREEN):**
  - `?` with no params → empty table (URL parsing edge case)
  - `%20` and `+` encoding — URLSearchParams normalizes; test explicitly covers `+` as space (form-urlencoded)
  - Duplicate keys — all rows render with same key, different values
  - Disabled params excluded from URL; re-enabling adds them back
  - Base URL (origin + path) preserved when params change
  - Sequence: add → edit → remove (state consistency through mutations)
  - Bidirectional URL sync validation with `$nextTick` and wrapper component
- **Test patterns established for future URL-like components** (fragment editor, path param editor)
- **Result:** All 18 tests GREEN. Kratos implemented; all 190 total tests pass (16 new + 174 existing). Zero regressions.

### 2026-03-30: Phase 3 & 4 Test Contracts (RED) — 55 tests
- **Collections Tests (20 tests):** CRUD, folder hierarchy, nesting, move, reorder, duplicate operations defined
- **Environments Tests (21 tests):** CRUD, variable management, secret masking, activation, variable resolution patterns
- **Advanced Features (14 tests sampled):** History pagination/filtering, code generation languages, assertion types and evaluation
- **Results:** 46 fail (RED — expected, endpoints being implemented), 9 pass (coincidental 404 handlers)
- **Regressions:** 0 — Phase 1-2 tests unaffected
- **Test infrastructure:** All use `ApiTestFixture` DB reset pattern; DTOs will move to Program.cs during GREEN phase

### 2025-07-16: Phase 7.1 Advanced Features tests (RED)
- **History (16 tests):** global `/api/history` endpoint with pagination (page/pageSize), filtering (requestId/method/status/dateRange), request+response snapshots, DELETE to clear
- **Code Generation (18 tests):** `GET /api/requests/{id}/code?language=X` for javascript-fetch, javascript-axios, python-requests, csharp-httpclient, curl; validates generated code structure
- **Assertions (16 tests):** `POST /api/requests/{id}/assertions` (status_equals, body_contains, header_exists), `GET /api/requests/{id}/assertions` list, `POST /api/requests/{id}/test` execute and evaluate
- **Result:** 50 tests created (46 fail RED, 4 pass coincidentally on 404 handlers)
- **Regressions:** 0 (existing 224 tests from Phases 1-6 unaffected)
- **Pattern:** All tests use existing `ApiTestFixture`, `TestData`, xUnit patterns; DTOs defined inline (will consolidate during implementation)

#### Marcus — Request API (GREEN)
- **7 endpoints implemented:** POST/GET/PUT/DELETE `/api/requests`, POST `/api/requests/{id}/send`, GET `/api/requests/{id}/history`
- **Validation:** Empty URL→400, invalid method→400, missing name→400, missing collectionId→400, body>10MB→413
- **RequestHistory:** Each send logs method, URL, timing, response data
- **DTOs:** CreateRequestDto, UpdateRequestDto (records at bottom of Program.cs)

- **2025-07-16 — Phase 7.1 Advanced Features tests (RED):** Created 50 failing tests across 3 new test files defining advanced feature contracts. Tests cover: **History** (16 tests) — global `/api/history` endpoint with pagination (page/pageSize returning items/totalCount), filtering by requestId/method/status/dateRange, request+response snapshots in entries, DELETE to clear all history. **Code Generation** (18 tests) — `GET /api/requests/{id}/code?language=X` for javascript-fetch, javascript-axios, python-requests, csharp-httpclient, curl; validates generated code includes method, URL, headers, body; invalid/missing language→400. **Assertions** (16 tests) — `POST /api/requests/{id}/assertions` (status_equals, body_contains, header_exists types), `GET /api/requests/{id}/assertions` to list, `POST /api/requests/{id}/test` to run request and evaluate assertions returning pass/fail per assertion with actual values. Result: 46 fail (endpoints don't exist — correct RED), 4 pass coincidentally (404 tests for non-existent request IDs hit unregistered routes). All tests reuse existing `ApiTestFixture` and `TestData` patterns. DTOs defined inline in test files: PaginatedHistory, CodeGenerationResponse, AssertionResponse, TestRunResponse, AssertionResultDto.
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

- **2025-07-17 — Phase 5.1 Authentication tests (RED):** Created 6 test files (5 backend, 1 frontend) defining the full auth contract. **Backend (5 files in `tests/APIneer.Api.Tests/Auth/`):** ApiKeyAuthTests.cs (10 tests — header placement injects custom header, query placement appends to URL with encoding, preserves existing headers/params, validation for missing key name/value/placement), BearerTokenTests.cs (7 tests — adds Authorization: Bearer header, overwrites existing auth, preserves other headers, validates empty/null token), BasicAuthTests.cs (10 tests — Authorization: Basic {base64(user:pass)}, special chars in password, colon in password, empty username/password still encodes, validates null username/password), OAuth2Tests.cs (13 tests — POSTs to token endpoint with client_credentials grant, sends client_id/client_secret/scope, adds access token as Bearer, stores token+expiry in AuthConfig, reuses cached non-expired token, refreshes expired token, handles token endpoint errors, validates missing endpoint/clientId/clientSecret), AuthInheritanceTests.cs (7 tests — request null auth inherits from collection, request auth overrides collection, null/null→null, type "none" disables inherited auth, same-type uses request values, end-to-end resolve+apply). Also created auth stubs: `src/api/APIneer.Api/Auth/` with AuthConfig.cs (model with Type discriminator + fields for api_key/bearer/basic/oauth2), IAuthHandler.cs (ApplyAuthAsync + ResolveAuth interface), AuthHandler.cs (empty stub — does nothing). **Frontend (1 file):** `src/ui/tests/components/AuthEditor.test.ts` (14 tests — auth type selector with None/API Key/Bearer Token/Basic Auth/OAuth 2.0, type-specific form fields with data-testid selectors, inherit-from-collection toggle, emits update:modelValue on type/field change, placement options header/query for API Key). Total: 47 backend tests (39 fail, 7 pass coincidentally from stub no-ops), 14 frontend tests (all fail — component doesn't exist). Key design: AuthHandler takes HttpClient for OAuth2 token calls; ResolveAuth handles inheritance (request > collection > none); MockTokenEndpointHandler captures requests for OAuth2 assertions.

---

- **2026-03-30 — Proxy Engine test contracts defined (Phase 2.3):**Created 35 failing tests across 6 test files defining the HTTP proxy engine contract. Tests cover: success paths (all HTTP methods), headers (custom/response/Content-Type/User-Agent), body types (JSON/form/text/empty/large), timing (response time & size metrics), error handling (timeout/invalid URL/connection refused/DNS failure — all as structured errors, never exceptions), and redirects (follow by default, capture chain, disable following). Proxy DTOs defined: `ProxyRequest`, `ProxyResponse`, `ProxyError`, `RedirectEntry`, `IProxyEngine`. Key constraints from security doc: 10MB max body, 30s default timeout (1s–5min range), no SSRF protection by design, structured errors instead of exceptions. Test infrastructure uses a raw `HostBuilder` + Kestrel with port 0 for random port allocation — `WebApplication.CreateSlimBuilder()` fought us on default port binding. FluentAssertions v8 renames: `BeGreaterThanOrEqualTo` (not `BeGreaterOrEqualTo`), `HaveCountGreaterThanOrEqualTo`.

- **2025-07-16 — Phase 2.5 Request Builder UI tests (RED):** Created 5 failing test files in `src/ui/tests/components/` covering MethodSelector (7 tests), UrlInput (6 tests), HeadersEditor (7 tests), BodyEditor (9 tests), RequestBuilder (7 tests) — 36 tests total. All fail because components at `~/components/request-builder/*.vue` don't exist yet. Tests use `mount` from `@vue/test-utils` with `data-testid` attributes for reliable selectors. Test infrastructure uses Vitest 4 + `@nuxt/test-utils` (environment: 'nuxt') with MSW 2 for API mocking. Existing test at `tests/setup.ts` configures MSW server globally.

- **2025-07-16 — Phase 2.7 Response Viewer UI tests (RED):** Created 5 failing test files for Response Viewer components: StatusBadge (8 tests — status display, color-coding for 2xx/3xx/4xx/5xx, status-0 edge case), ResponseBody (9 tests — render, empty state, JSON pretty-print, Pretty/Raw tabs, copy-to-clipboard, large body handling), ResponseHeaders (6 tests — table rendering, alphabetical sort, header count, empty state), ResponseTiming (5 tests — ms display, human-readable size in B/KB/MB, compact layout), ResponseViewer integration (7 tests — tabs, default tab, StatusBadge presence, empty state, child data passing). All 35 tests fail because components at `~/components/response/*.vue` don't exist yet. Used `mountSuspended` from `@nuxt/test-utils/runtime` consistent with Nuxt test conventions and `data-testid` attributes for selectors.

- **2025-07-16 — Phase 2.1 Request CRUD + validation tests (RED):** Created 4 files in `tests/APIneer.Api.Tests/Requests/`: TestData.cs (shared helper with sample payloads, JSON serialization, response DTOs), RequestCrudTests.cs (20 tests — POST create, GET list, GET by ID, PUT update, DELETE, POST send/execute, GET history), RequestValidationTests.cs (11 tests — empty URL→400, invalid method→400, missing name→400, missing collectionId→400, non-existent ID→404 for all endpoints, body>10MB→413 per security doc), RequestModelTests.cs (12 tests — default values, timestamps, headers JSON serialization round-trip, RequestHistory model). Total 53 tests: 31 fail (no endpoints exist — correct RED phase), 22 model tests pass. Also fixed pre-existing build errors in Proxy test files (FluentAssertions v8 API changes: `BeGreaterOrEqualTo`→`BeGreaterThanOrEqualTo`, `HaveCountGreaterOrEqualTo`→`HaveCountGreaterThanOrEqualTo`, `.Subject` removal on `AndConstraint`).

- **2025-07-16 — Phase 3.1 Collections & Folders tests (RED):** Created 6 files defining the Collection/Folder API contract. **Backend (5 files in `tests/APIneer.Api.Tests/Collections/`):** CollectionTestData.cs (shared helpers: SeedWorkspaceAsync, SeedCollectionAsync, CreateFolderAsync, CreateRequestAsync, response DTOs for Collection/Folder/RequestSummary), CollectionCrudTests.cs (17 tests — POST/GET/PUT/DELETE `/api/collections`, timestamps, location header, null description, cascade delete of folders+requests, 400 on missing name, 404 on non-existent), FolderTests.cs (12 tests — POST `/api/collections/{id}/folders`, nested folders, 3+ level deep nesting, PATCH `/api/requests/{id}/move` between folders and to root, DELETE folder cascades to requests and subfolders, 400 missing name, 404 non-existent collection), CollectionOrderingTests.cs (7 tests — auto sort order for requests in folders and at root, PATCH `/api/collections/{id}/reorder`, folder sort order, order preserved after deletion), CollectionDuplicateTests.cs (7 tests — POST `/api/collections/{id}/duplicate` deep copy, "(Copy)" suffix, duplicate folders/requests/nested structure, new IDs, 404 non-existent). Total 41 backend tests: 32 fail (endpoints don't exist — correct RED), 9 pass (404 tests matching unregistered routes). **Frontend (1 file):** CollectionTree.test.ts (14 tests — tree rendering with collections/folders/requests, nested subfolders, method badges, expand/collapse toggle, active request highlighting with `.active` class, select-request emit, empty state). Fails because `~/components/collections/CollectionTree.vue` doesn't exist yet. Key patterns: reuses ApiTestFixture with DB reset, `data-testid` selectors for frontend, `mountSuspended` from `@nuxt/test-utils/runtime`.

- **2025-07-16 — Phase 4.1 Environment & Variable tests (RED):** Created 6 files across backend and frontend. Backend: `tests/APIneer.Api.Tests/Environments/` with EnvironmentTestData.cs (shared helpers, seed methods, response DTOs), EnvironmentCrudTests.cs (24 tests — POST/GET/PUT/DELETE for environments and variables, cascade delete, variable CRUD on sub-resource), VariableResolutionTests.cs (7 tests — `{{var}}` in URL/headers/body, nested vars, undefined stays as-is, escaped braces, empty value→empty string), SecretVariableTests.cs (6 tests — masked `***masked***` in GET, real value at send-time, raw secret never in API response, secret flag persists on update, flag can be toggled off), ActiveEnvironmentTests.cs (7 tests — activate endpoint, only-one-active-per-workspace, resolution uses active env, no active env→no resolution, deactivate stops resolution). Frontend: `src/ui/tests/components/EnvironmentSelector.test.ts` (8 tests — dropdown render, env names listed, active selected, active indicator, emit update:modelValue, emit activate event, empty state, "No Environment" option). Total: 52 tests. Backend: 33 fail / 10 pass (404-based pass because endpoints don't exist). Frontend: all 8 fail (component doesn't exist). Key design decisions: resolve endpoint at POST `/api/environments/resolve`, activate/deactivate at PUT sub-resources, secret masking per security-architecture.md invariants.

- **2025-07-17 — Phase 6.1 Import/Export tests (RED):** Created 6 files across backend and frontend. **Backend (5 files in `tests/APIneer.Api.Tests/ImportExport/`):** ImportExportTestData.cs (shared helpers, seed methods with workspace+collection+folder+requests, response DTOs for import/export, realistic Postman v2.1 fixtures including flat and deeply nested collections, cURL test fixtures with various flags), PostmanImportTests.cs (15 tests — POST `/api/import/postman` accepts Postman v2.1 JSON, creates APIneer collection with matching structure, preserves method/URL/headers/body, handles nested folders 3+ levels deep, validates invalid JSON/empty body/missing schema→400), CurlImportTests.cs (15 tests — POST `/api/import/curl` accepts cURL command string, parses method -X/URL/headers -H/body -d --data, handles multiline backslash continuation, parses -u basic auth flag to Authorization header, validates empty string/non-curl command→400), ExportTests.cs (16 tests — GET `/api/collections/{id}/export?format=json` for APIneer native format with folders+requests, `format=curl` generates cURL commands per request with headers/body, `format=postman` generates Postman v2.1 schema with nested items, invalid format→400, missing format→400, non-existent collection→404), RoundTripTests.cs (10 tests — JSON export→import preserves collection name/request count/folder count/request details with new IDs, Postman export→import→verify preserves name/count/folders/methods). **Frontend (1 file):** `src/ui/tests/components/ImportModal.test.ts` (11 tests — modal render, file upload area, format selector with Postman/cURL/HAR options, import button disabled when no file, preview area with empty state, hidden when visible=false, close button emits close, cURL text area input). Total: 57 backend tests (56 fail, 1 pass coincidentally — 404 on non-existent collection export), 11 frontend tests fail (component `~/components/import-export/ImportModal.vue` doesn't exist). Key patterns: reuses ApiTestFixture with DB reset, realistic Postman v2.1 JSON fixtures, text/plain content type for cURL import, `data-testid` selectors for frontend.

- **2025-07-17 — Collections UI Enhancement tests (RED):** Created 4 test files in `src/ui/tests/components/` defining frontend collection UI contracts. **CollectionContextMenu.test.ts** (12 tests — renders on right-click, positions at coordinates, shows correct actions per item type: collection gets Rename/Delete/Duplicate/New Folder/New Request/Export; folder gets Rename/Delete/New Sub-folder/New Request; request gets Rename/Delete/Duplicate/Move to...; emits action event with item data, closes after selection, closes on click outside via overlay). **CollectionPicker.test.ts** (10 tests — dropdown with collections, shows folder hierarchy, Default option, emits select with collectionId/folderId, inline vs modal mode, disabled state when empty). **InlineRename.test.ts** (10 tests — display mode shows text, double-click activates edit, input auto-focused, Enter saves emitting rename, Escape cancels emitting cancel, blur saves, empty name reverts without emitting, shows updated name after save). **CollectionSidebar.test.ts** (10 tests — renders collection tree, request count per collection, New Request/New Collection buttons with events, click emits select-request, active request highlighting, empty state, search/filter by name). Total: 42 tests across 4 files — all fail (components at `~/components/collections/{CollectionContextMenu,CollectionPicker,InlineRename,CollectionSidebar}.vue` don't exist). Uses `mount` from `@vue/test-utils` (not `mountSuspended`), `data-testid` selectors. Regressions: 0 — existing 9 passing test files (70 tests) unaffected.

## Phase 9: Auth Integration Testing (2026-03-31T19:25Z) — ✅ COMPLETE

- **Task:** Write comprehensive integration tests for AuthEditor + RequestBuilder edge cases, verify auth pipeline
- **Test Coverage:** 30 tests covering:
  - Type selection (None, Bearer, API Key, Basic, OAuth2)
  - Type-specific field validation (empty token, missing key, missing credentials)
  - Bidirectional v-model binding with authConfig state
  - JSON serialization on save/send roundtrip
  - Defensive parsing: string, object, null, invalid JSON inputs
  - Edge cases: switching types, special characters, persistence across re-renders
- **Integration:** Tests verify authConfig flows from AuthEditor → RequestBuilder → emit payload → API
- **Results:** 30/30 tests GREEN, validates auth config pipeline works end-to-end
- **Status:** Integration tests complete, auth edge cases covered, ready for backend proxy testing

