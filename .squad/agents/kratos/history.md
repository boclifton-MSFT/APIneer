# Project Context

- **Owner:** boclifton-MSFT
- **Project:** APIneer — a locally running API platform (Postman alternative). Desktop app for building, testing, and managing API requests with collections, environments, and response visualization.
- **Stack:** .NET 10 (backend), Nuxt UI v4 (frontend)
- **Created:** 2026-03-30

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2025-07-16 — Phase 2.6: Request Builder UI Components
- **Components created:** 5 Vue components in `app/components/request-builder/`:
  - `MethodSelector.vue` — native `<select>` with 7 HTTP methods, color-coded via `method-{color}` CSS classes
  - `UrlInput.vue` — text input with overlay highlighting for `{{variable}}` patterns (`.url-variable` class)
  - `HeadersEditor.vue` — key-value `<table>` with add/remove rows, starts with one empty row, emits on every change
  - `BodyEditor.vue` — custom tab buttons (None/Raw/JSON/Form Data) with textarea, JSON validation error display
  - `RequestBuilder.vue` — composes all above + Send button (loading/disabled state) + Ctrl+Enter shortcut + section tabs (Params/Headers/Body/Auth)
- **Key pattern:** Tests use `mount` from `@vue/test-utils` (not `mountSuspended`), so components use native HTML elements (select, table, textarea) rather than Nuxt UI wrapper components. Tests find elements via native selectors and `data-testid` attributes.
- **Important:** `defineOptions({ name: 'ComponentName' })` is required in `<script setup>` components for `findComponent({ name: '...' })` to work in tests.
- **All 41 tests pass** (5 test files, 41 individual test cases including parameterized `it.each` expansions).

### 2025-07-16 — Phase 1.2: Frontend Scaffolding Complete
- **Project location:** `src/ui/` — Nuxt 4.4.2 + Nuxt UI v4 (`@nuxt/ui 4.6.0`)
- **Package manager:** pnpm. After `nuxi init`, must run `pnpm approve-builds` to allow esbuild/msw/parcel-watcher/vue-demi postinstall scripts.
- **Config:** `nuxt.config.ts` registers `@nuxt/ui` and `@pinia/nuxt` modules. Server proxy routes `/api/**` → `http://localhost:5000/api/**` via `routeRules`.
- **CSS:** `app/assets/css/main.css` uses `@import "tailwindcss"` + `@import "@nuxt/ui"` per v4 conventions.
- **Layout:** Dashboard layout at `app/layouts/dashboard.vue` uses `UDashboardGroup` + `UDashboardSidebar` (collapsible/resizable). Sidebar has APIneer branding, nav items (Requests, Collections, Environments), and Settings footer.
- **Pages:** `app/pages/index.vue` uses `definePageMeta({ layout: 'dashboard' })` with `UDashboardPanel` + `UDashboardNavbar`.
- **Testing:** Vitest 4.x with `@nuxt/test-utils` providing `environment: 'nuxt'` (not happy-dom directly — mountSuspended needs the full Nuxt context). MSW 2.x for API mocking in `tests/mocks/handlers.ts`. Setup in `tests/setup.ts`.
- **State management:** Pinia registered via `@pinia/nuxt`.
- **Validation:** Zod installed for schema validation.
- **Scripts:** `pnpm test` = vitest run, `pnpm test:watch` = vitest, `pnpm dev` = nuxt dev (port 3000).
- **Key pattern:** vitest config uses `defineVitestConfig` from `@nuxt/test-utils/config` — do NOT use plain `defineConfig` from vitest or tests requiring Nuxt context will fail.

## Cross-Agent Context (Phase 1)

### Backend (Marcus)
- **Location:** `src/api/APIneer.Api/` — .NET 10 Minimal API on `localhost:5000`
- **Database:** EF Core + SQLite (`apineer.db`)
- **7 entity models:** Workspace, Collection, CollectionFolder, ApiRequest, Environment, EnvironmentVariable, RequestHistory
- **Relationships:** Workspace→Collections/Environments, Collection→Folders/Requests, CollectionFolder→self, ApiRequest→RequestHistory, Environment→Variables
- **Health check:** `GET /health` → `{ status: "healthy" }`
- **Swagger:** `/swagger` (OpenAPI schema at `/swagger/v1/swagger.json`)

### Security Architecture (Payne)
- **Auth pattern:** Header injection — credentials resolved → injected → request sent → stripped from response before returning to frontend
- **Secret handling:** DPAPI encryption at rest, decryption backend-only at request execution time
- **Frontend invariant:** Secrets ALWAYS masked in UI as `***masked***`, never raw values
- **Encryption keys:** Windows `%APPDATA%\APIneer\keys\`, Unix `~/.config/apineer/keys/`
- **Document:** `docs/security-architecture.md` with 7 testable invariants

### 2025-07-17 — Phase 2.8: Response Viewer UI Components
- **Components created:** `app/components/response/` — StatusBadge, ResponseBody, ResponseHeaders, ResponseTiming, ResponseViewer (5 Vue components, 35 tests passing)
- **Nuxt auto-import caveat:** Components in subdirectories like `components/response/StatusBadge.vue` get path-prefixed as `ResponseStatusBadge`. When composing child components inside parent components in the same subdirectory, use explicit `import` statements (e.g., `import StatusBadge from '~/components/response/StatusBadge.vue'`) to avoid resolution failures in tests.
- **happy-dom clipboard mocking:** `navigator.clipboard` is a getter-only property in happy-dom. Tests using `Object.assign(navigator, { clipboard: ... })` fail unless `setup.ts` first redefines `clipboard` as a writable property via `Object.defineProperty`.
- **Test-first pattern works well:** Reading Freeman's tests as specs made component implementation straightforward — data-testids, CSS class conventions, and DOM structure were all clearly defined.

## Cross-Agent Context (Phase 2)

### Freeman — Builder UI Tests (RED)
- **36 failing tests** defining RequestBuilder contract: MethodSelector (7), UrlInput (6), HeadersEditor (7), BodyEditor (9), RequestBuilder (7)
- **Test patterns:** `mount` from @vue/test-utils, `data-testid` for element selection, native HTML elements (select, table, textarea)
- **Key requirement:** `defineOptions({ name: 'ComponentName' })` for `findComponent({ name: '...' })` to work in tests
- **Vitest config:** Must use `defineVitestConfig` from @nuxt/test-utils/config

### Marcus — Request API (GREEN)
- **7 endpoints:** POST/GET/PUT/DELETE `/api/requests`, POST `/api/requests/{id}/send`, GET `/api/requests/{id}/history`
- **Validation:** Empty URL→400, invalid method→400, missing name→400, missing collectionId→400, body>10MB→413
- **Valid HTTP methods:** GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS (case-insensitive, stored uppercase)
- **RequestHistory:** Logged on each send with method, URL, timing, response data
- **DTOs:** CreateRequestDto, UpdateRequestDto (records)
- **Result:** 53/53 Request tests pass

### Ralph — Response Viewer UI (GREEN)
- **5 components:** StatusBadge, ResponseBody, ResponseHeaders, ResponseTiming, ResponseViewer — **82 tests pass**
- **Color-coding:** 2xx green, 3xx blue, 4xx orange, 5xx red, status-0 special case
- **ResponseBody features:** JSON pretty-print, Pretty/Raw tabs, copy-to-clipboard, large body handling
- **ResponseHeaders:** Alphabetical sort, count display, empty state
- **ResponseTiming:** ms display, human-readable size (B/KB/MB)
- **Nuxt auto-import caveat:** Path-prefixed subdir components; explicit imports needed in parent components in same subdir
- **Clipboard mocking:** Object.defineProperty needed for happy-dom navigator.clipboard writable

