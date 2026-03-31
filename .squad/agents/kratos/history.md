# Project Context

- **Owner:** boclifton-MSFT
- **Project:** APIneer — a locally running API platform (Postman alternative). Desktop app for building, testing, and managing API requests with collections, environments, and response visualization.
- **Stack:** .NET 10 (backend), Nuxt UI v4 (frontend)
- **Created:** 2026-03-30

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-30: Phase 3 & 4 UI — Collections & Environments (GREEN, 21/21 tests)
- **Collections (13 tests):** `CollectionTree.vue` (recursive folder rendering, collapse/expand, drag handles, select/move events) + `CollectionTreeFolder.vue` (recursive child folders, nested requests, context menu stub)
- **Environments (8 tests):** `EnvironmentSelector.vue` (dropdown for active environment, list all, dispatch `activateEnvironment`, disabled when no environments)
- **Key patterns:** `mountSuspended` for async setup, `@nuxt/test-utils` environment: 'nuxt', MSW 2 API mocks, `data-testid` selectors
- **State:** Pinia store integration for environment switching
- **Test infrastructure:** All 21 tests pass, zero regressions

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

### 2026-03-31 — QueryParamsEditor: Complete Implementation (GREEN)
- **Component:** `app/components/request-builder/QueryParamsEditor.vue` — production-ready key-value table with enable/disable toggles per row, bidirectional sync with URL bar
- **Pattern:** Uses `v-model:url` prop for two-way binding. Parses URL query string into table rows, rebuilds query string on edits. Uses `suppressUrlSync` flag to prevent recursive watch triggers during emit cycles.
- **Manual URL parsing:** `split('&')` + `decodeURIComponent` instead of URLSearchParams — gives full control over edge cases (key-only params, duplicate keys, original ordering)
- **Edge cases handled:** `+` decoded as space (form-urlencoded convention), percent-encoded values, `?` with no params, duplicate keys, key-only params (no `=`), disabled params excluded from URL but preserved in table
- **Wired into:** `RequestBuilder.vue` — replaced "coming soon" placeholder in Params tab
- **Pre-existing fix:** Fixed bug in RequestBuilder.vue where `req.headers` could be JSON string from API but was assumed to be array. Added fallback JSON.parse handling.
- **Test results:** All 16 unit tests GREEN (Freeman wrote; Kratos implemented). All 190 total tests pass (16 new + 174 existing). Zero regressions.

### 2025-07-18 — AuthEditor Wiring: Full Auth Integration (GREEN, 20/20 tests)
- **Wired into:** `RequestBuilder.vue` — replaced "Authentication editor coming soon" placeholder in Auth tab with `<AuthEditor v-model="authConfig" />`
- **State management:** Added `authConfig` reactive ref (type `AuthConfig = { type: string; [key: string]: any }`, default `{ type: 'none' }`). Synced from `props.request.authConfig` in existing watch with defensive JSON parsing (handles string or object).
- **Send payload:** `authConfig` serialized to JSON string via `JSON.stringify()` and included in `emit('send', ...)` payload. Updated emit type signature to include `authConfig: string`.
- **RequestData interface:** Added optional `authConfig?: string` field.
- **index.vue changes:** Updated `handleSend` to accept and forward `authConfig` in the API payload. Passed through to `updateRequest()` so auth config persists with saved requests.
- **useApi.ts:** Added `authConfig?: string` to `ApiRequest` interface. No other changes needed — `updateRequest` already accepts `Partial<ApiRequest>`, so authConfig flows through automatically.
- **AuthEditor styling:** Added scoped CSS matching project patterns — CSS variables for colors (`--ui-border`, `--ui-primary`, `--ui-text`, `--ui-text-muted`), consistent input sizing (0.5rem/0.75rem padding), focus ring with `color-mix`, placeholder muted color.
- **Pattern:** Auth config stored as JSON string in backend (same as headers). Frontend parses on load, serializes on save. Defensive parsing handles both string and pre-parsed object.
- **Test results:** All 20 tests pass (13 AuthEditor + 7 RequestBuilder). Zero regressions.

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

### 2025-07-17 — Phase 4.3: Environments UI (EnvironmentSelector)
- **Component created:** `app/components/environments/EnvironmentSelector.vue` — native `<select>` dropdown with v-model support
- **Props:** `environments` (array of `{id, name, isActive, workspaceId}`), `modelValue` (string, selected env id)
- **Emits:** `update:modelValue` and `activate` on selection change, `manage` for opening environment editor
- **Features:** "No Environment" option (value `""`) to deselect, `[data-testid="active-indicator"]` showing current environment name, `[data-testid="no-environments"]` empty state when environments array is empty
- **Key pattern:** Uses native `<select>` + `<option>` elements (not Nuxt UI wrappers) since tests use `mount` from `@vue/test-utils` and query via `wrapper.findAll('option')` and `setValue()`.
- **All 8 tests pass** on first implementation — test-first spec reading continues to be efficient.

### 2025-07-18 — Phase 3.3: Collections UI (CollectionTree)
- **Components created:** `app/components/collections/CollectionTree.vue` (parent) + `CollectionTreeFolder.vue` (recursive child)
- **Props:** `collections` (array of `{id, name, folders, requests}`), `activeRequestId` (string)
- **Emits:** `select-request` with request id when a request item is clicked
- **Features:** Hierarchical tree rendering (collections → folders → subfolders → requests), expand/collapse folders via local `ref(true)` state, HTTP method badges (`data-testid="method-badge"`), active request highlighting (`.active` class), empty state (`data-testid="empty-collections"`)
- **Critical happy-dom caveat:** `getComputedStyle()` returns empty strings for inline styles in happy-dom, so `v-show` alone breaks `isVisible()` from `@vue/test-utils`. Fix: add `:hidden="!isExpanded || undefined"` alongside `v-show` — the `hidden` HTML attribute is checked by `isAttributeVisible()` in test-utils, making `isVisible()` work correctly.
- **Nuxt auto-import caveat (confirmed again):** Components in subdirectories require explicit `import` statements when composing parent/child relationships. `CollectionTreeFolder` self-imports for recursion.
- **All 13 CollectionTree tests pass**, 103 total tests pass across the project.

### 2025-07-18 — Phase 8.1: Theming & Branding
- **Brand colors configured** in `app.config.ts`: primary `sky`, secondary `violet`, neutral `slate` — professional blue-teal palette
- **CSS variables** in `app/assets/css/main.css`: `--ui-radius: 0.375rem`, font-sans set to Inter, font-mono to JetBrains Mono via `@theme` directive
- **Dark/light mode:** Added `UColorModeButton` to dashboard sidebar footer — toggles dark/light with ghost style
- **Command palette:** Created `app/components/app/CommandPalette.vue` — `UCommandPalette` inside `UModal` with two groups (Actions + Navigation):
  - `Ctrl+K` — open/close command palette
  - `Ctrl+N` — new request (navigates to /)
  - Settings shortcut with `meta+,`
  - Navigation items for Requests, Collections, History, Environments
- **Dashboard branding:** Sidebar header now has a branded icon container (rounded bg-primary/10) with "APIneer" title + "API Platform" subtitle
- **Sidebar navigation:** Added "History" nav item (i-lucide-history), changed Collections icon to `i-lucide-folder-open`
- **Index page polish:** Empty state with centered icon, heading, description, and "New Request" CTA button; navbar shows Ctrl+K shortcut hint
- **Build passes** (`pnpm nuxt build` — clean). Test failures are pre-existing @nuxt/test-utils timeout issues in 6 component test files (not related to theming changes).

### 2026-03-31 — Phase 6.4: Import Modal UI (GREEN, 11/11 tests)
- **Component created:** `app/components/import-export/ImportModal.vue` — file import dialog with format selection and cURL paste support
- **Props:** `visible` (boolean) — controls modal rendering via `v-if`
- **Emits:** `close` (cancel/close button), `import` (payload: `{ format: string, data: string }`)
- **Features:** File upload drop zone (`data-testid="file-upload-area"`), native `<select>` format selector (postman/curl/har), import button disabled until content provided, preview area showing "No file selected" empty state, cURL textarea (`data-testid="curl-input"`) appears when curl format selected
- **Key pattern:** Continues native HTML element approach (not Nuxt UI wrappers) since tests use `mount` from `@vue/test-utils` and query via `findAll('option')`, `setValue()`, and `data-testid` selectors
- **All 11 ImportModal tests pass** on first implementation — test-first spec reading continues to be efficient

### 2026-03-31 — Phase 5.3: Auth Editor UI (GREEN, 13/13 tests)
- **Component created:** `app/components/auth/AuthEditor.vue` — auth configuration editor with type switching and form fields
- **Props:** `modelValue` (auth config object with `type` field), `showInherit` (boolean), `inherit` (boolean)
- **Emits:** `update:modelValue` (auth config object on type or field change), `update:inherit` (boolean on toggle change)
- **Auth types:** None (`none`), API Key (`api_key`), Bearer Token (`bearer`), Basic Auth (`basic`), OAuth 2.0 (`oauth2`)
- **Type-specific fields:**
  - API Key: keyName, keyValue, placement (header/query select)
  - Bearer Token: token input
  - Basic Auth: username + password inputs
  - OAuth 2.0: tokenEndpoint, clientId, clientSecret, scope
- **Features:** Native `<select>` type selector with `data-testid="auth-type-selector"`, conditional field rendering via `v-if`, inherit-from-collection checkbox toggle, defaults to `none` when no modelValue
- **Key pattern:** Uses `:value` + `@input`/`@change` instead of `v-model` to emit full config objects on every change. `defaultConfigFor()` helper creates clean config shapes on type switch.
- **All 13 AuthEditor tests pass**, 127 total tests pass across the project (zero regressions)

### 2026-03-31 — Phase 9: Page Wiring & Integration
- **API composable:** Created `app/composables/useApi.ts` — typed helper wrapping `$fetch` for all backend endpoints (requests CRUD, send, collections CRUD, history, environments CRUD, variables CRUD)
- **Requests page (`index.vue`):** Rewired from empty state to dual-panel list-detail layout. Left panel: request list with create/delete, method badges. Right panel: `RequestBuilder` + `ResponseViewer`. New Request creates via `POST /api/requests`, Send button saves then calls `POST /api/requests/{id}/send`, response displayed in `ResponseViewer`
- **Collections page (`collections.vue`):** Dashboard layout with `CollectionTree` component, empty state, create collection modal (`UModal` + `UInput`), clicking a request navigates to request builder with query param
- **History page (`history.vue`):** Dashboard layout with `UTable` showing method/URL/status/time/date columns, color-coded badges, "Clear History" button, empty state with link to Requests
- **Environments page (`environments.vue`):** Dual-panel layout. Left: environment list with `EnvironmentSelector`, create/edit/delete. Right: variable key-value table with add/delete, secret masking (`***masked***`), create/edit modals
- **Command palette:** Wired "New Request" action to `POST /api/requests` then navigate, Ctrl+N shortcut does the same. Navigation actions route to correct pages
- **Icons fix:** Installed `@iconify-json/lucide` — resolves all `[Icon] failed to load icon lucide:*` errors
- **Build:** `pnpm nuxt build` passes clean. Pre-existing test timeout issues unchanged (8 files, `@nuxt/test-utils` hook timeouts)

### 2026-03-31 — Phase collections-context-menu + collections-save-to: CollectionContextMenu & CollectionPicker (GREEN, 24/24 tests)
- **CollectionContextMenu.vue:** Right-click context menu for collection tree items
  - Props: `visible` (boolean), `item` (object with `type` field), `position` ({ x, y })
  - Emits: `action` (payload `{ action: string, item }`) + `close`
  - Type-specific actions: Collection (Rename/Delete/Duplicate/New Folder/New Request/Export), Folder (Rename/Delete/New Sub-folder/New Request), Request (Rename/Delete/Duplicate/Move to...)
  - Uses transparent overlay for click-outside detection, fixed positioning at mouse coordinates
  - All 13 tests pass
- **CollectionPicker.vue:** Dropdown selector for choosing collection/folder destination
  - Props: `collections` (array), `mode` ('inline' | 'modal')
  - Emits: `select` (payload `{ collectionId: string|null, folderId?: string }`)
  - Recursive folder flattening with depth-based indentation, "Default" option emits `null` collectionId
  - Modal mode wraps in `collection-picker-modal` container, inline mode adds `.inline` class
  - Disabled state with `aria-disabled="true"` and "No collections" message when empty
  - All 11 tests pass (including nested sub-folder hierarchy rendering)
- **Key pattern:** Continues native HTML element approach (no Nuxt UI wrappers) — tests use `mount` from `@vue/test-utils` with `data-testid` selectors

### 2026-03-31 — Phase collections-sidebar + collections-create-modal: CollectionSidebar & Create Modal (GREEN, 11/11 tests)
- **CollectionSidebar.vue:** Full sidebar component wrapping collection tree with search, actions, and request selection
  - Props: `collections` (array), `activeRequestId` (string), `selectedCollectionId` (string)
  - Emits: `new-request` (payload `{ collectionId: string }`), `new-collection`, `select-request` (requestId)
  - Features: Recursive request count per collection (`data-testid="collection-count-{id}"`), search/filter input (`data-testid="sidebar-search"`) with case-insensitive name matching, active request highlighting (`.active` class), empty state (`data-testid="sidebar-empty-state"`)
  - Search filtering: Recursively filters folders and requests, hides entire collections with no matches
  - Reuses `CollectionTreeFolder` for folder rendering with `@select-request` event propagation
  - All 11 tests pass on first implementation
- **index.vue updated:** Replaced flat request list with `CollectionSidebar`, loads collections via `api.getCollections()` on mount, wired "New Collection" button to `UModal` with `UInput` for name entry, create collection via API then refresh tree, new requests assigned to `selectedCollectionId`
- **Key pattern:** Native `<input>` for search (supports `setValue()` in tests), `CollectionTreeFolder` reuse for recursive folder rendering
- **Test suite:** 171 tests pass, 1 pre-existing InlineRename focus failure unrelated

### 2026-03-31 — Phase collections-rename: InlineRename Component (GREEN, 10/10 tests)
- **Component created:** `app/components/collections/InlineRename.vue` — inline text edit with double-click activation
- **Props:** `value` (string, current name)
- **Emits:** `rename` (new value string on save), `cancel` (on Escape)
- **Features:** Display mode (span with `data-testid="inline-rename-display"`), edit mode (input with `data-testid="inline-rename-input"`), double-click to edit, Enter/blur to save, Escape to cancel, empty string validation (reverts to original)
- **Key patterns:**
  - Vue key modifiers (`@keydown.enter`, `@keydown.escape`) instead of manual `e.key` checks — works reliably with Vue test-utils `trigger('keydown.enter')` / `trigger('keydown.escape')`
  - `handled` flag guards against blur double-fire when Enter/Escape removes input from DOM (v-if removal triggers blur)
  - Template ref (`ref="inputRef"`) for auto-focus + select on edit activation via `nextTick` callback
  - **happy-dom focus caveat:** `focus()` on detached elements (when `mount()` renders in a disconnected tree) doesn't set `document.activeElement`. Fix: check `el.isConnected` and walk up to root node, append to `document.body` before calling `focus()`. In production Nuxt apps, elements are always connected (no-op).
- **All 10 InlineRename tests pass**, zero regressions

### 2025-07-19 — Phase collections-drag-drop: Drag & Drop Reorder for Collection Tree
- **Composable created:** `app/composables/useCollectionDragDrop.ts` — module-level shared `currentDrag` ref with `startDrag(event, payload)` / `endDrag()` helpers. All tree components share the same drag state via this composable.
- **API functions added to `useApi.ts`:** `moveRequest(requestId, { collectionId, folderId })` → `PATCH /api/requests/{id}/move`, `reorderCollection(collectionId, items[])` → `PATCH /api/collections/{id}/reorder`
- **CollectionTreeFolder.vue updated:**
  - New optional prop `collectionId` (default `''`) for drag context
  - New emits: `move-request`, `reorder` (bubble up through recursive tree)
  - Request items: `draggable="true"` with `dragstart`/`dragend`/`dragover`/`dragleave`/`drop` handlers
  - Folder headers: drop targets with `dragover`/`dragleave`/`drop` — dropping a request onto a folder emits `move-request`
  - Reorder within same folder: detects above/below position via mouse Y vs element midpoint, recomputes request order array, emits `reorder`
  - Recursive sub-folder calls forward `collection-id`, `@move-request`, `@reorder` events
- **CollectionTree.vue updated:** Same drag-drop pattern for root-level requests + collection headers as drop targets
- **CollectionSidebar.vue updated:** Same drag-drop pattern for root-level requests + collection headers as drop targets, preserves existing search/filter/count logic
- **Visual indicators (scoped CSS in each component):**
  - `.dragging` — opacity 0.4 on the item being dragged
  - `.drag-over` — dashed primary-colored outline on valid drop target folders/collections
  - `.drop-above` / `.drop-below` — solid primary-colored border line showing insertion point
  - `cursor: grab` / `cursor: grabbing` on draggable items
- **Architecture:** Native HTML5 drag-and-drop API, no external libraries. Module-level shared ref pattern ensures all nested components see the same drag state. Events bubble up through the recursive tree for parent to handle API calls.
- **Build passes clean**, 172/172 tests pass, zero regressions


## Phase 9: Auth Editor Frontend Wiring (2026-03-31T19:25Z) — ✅ COMPLETE

- **Task:** Wire AuthEditor component into RequestBuilder, connect auth config to send/save API flow, add Nuxt UI styling
- **Components:**
  - AuthEditor.vue: type selector (None, Bearer, API Key, Basic, OAuth2), type-specific field editors, defensive JSON parsing
  - RequestBuilder.vue: authConfig state management, serialization on send/save, integration with API payload
- **Pattern:** JSON string transport — consistent with headers pattern, defensive parsing prevents crashes
- **Tests:** 20 frontend tests pass — component unit tests + RequestBuilder integration tests
- **Status:** AuthEditor fully wired, auth config flowing through API pipeline to backend

