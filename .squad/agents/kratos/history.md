# Project Context

- **Owner:** boclifton-MSFT
- **Project:** APIneer — a locally running API platform (Postman alternative). Desktop app for building, testing, and managing API requests with collections, environments, and response visualization.
- **Stack:** .NET 10 (backend), Nuxt UI v4 (frontend)
- **Created:** 2026-03-30

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2025-07-18: Phase B MCP Component Decomposition (224/224 tests GREEN)
- **File structure:** All MCP sub-components live in `src/ui/app/components/mcp/` (McpServerList, McpConnectionForm, McpToolPanel, McpResourcePanel, McpPromptPanel, McpRpcHistory). `KeyValueEditor.vue` lives in `src/ui/app/components/` (general-purpose, not MCP-specific).
- **Shared composables:** `composables/useMcpHelpers.ts` exports `ConnectionState`, `McpFormData`, `buildEnvObject()`, `buildHeadersObject()`, `parseKeyValueJson()`. `composables/useMcpRpcHistory.ts` uses module-level singleton refs so all panels share one history list without Pinia.
- **Panel architecture:** `McpToolPanel`, `McpResourcePanel`, `McpPromptPanel` are self-contained — each calls the API directly and logs to history via `useMcpRpcHistory()`. They take `connectionId: string` and `active: boolean` props. Watch `[connectionId, active]` to lazy-fetch only when their tab is active.
- **Tab state persistence:** Panels use `v-show` (not `v-if`) inside a `v-else` block for `connectionState === 'connected'`. This keeps panel state alive across tab switches (no redundant refetches), matching original behavior. When disconnecting, the `v-else` unmounts all panels, naturally clearing state on reconnect.
- **McpConnectionForm pattern:** "Dumb" form component — owns its own form state (name, transport, command, args, url, envVars, customHeaders), watches the `server` prop to populate itself, emits `connect(McpFormData)`, `disconnect`, `save(McpFormData)`. The page handles API calls and owns `connectionState`/`connectionId`.
- **KeyValueEditor pattern:** `defineModel<{key,value}[]>()` with direct array mutation (push/splice). Props: `keyPlaceholder`, `valuePlaceholder`. Used in `McpConnectionForm` for env vars and custom headers.
- **Brady's organization preference:** Feature-specific components go in a named subfolder under `components/`. Reusable/general-purpose components stay at the `components/` root.
- **`size="2xs"` Nuxt UI issue:** Nuxt UI v4's UButton only accepts `xs | sm | md | lg | xl`. The original mcp.vue used `size="2xs"` (a pre-existing TS error). All new components use `size="xs"` to stay type-clean.
- **Type note:** Dynamic schema-driven toolArgs (`Record<string, any>`) is intentional — MCP input schemas are dynamic JSON Schema objects so `any` is appropriate there. `toolResult` also uses `Record<string, any>` for the same reason.

### 2025-07-18: Phase A Frontend Optimizations (224/224 tests GREEN)
- **Type deduplication:** `Collection`, `CollectionFolder`, `CollectionRequest` interfaces now exported from `~/composables/useApi.ts` and imported by `CollectionSidebar.vue`, `CollectionTree.vue`, `CollectionTreeFolder.vue`, `CollectionPicker.vue`. No more copy-pasted interfaces.
- **`defineModel` adoption:** `UrlInput.vue`, `MethodSelector.vue`, `HeadersEditor.vue`, `BodyEditor.vue`, `EnvironmentSelector.vue` now use Vue 3.4+ `defineModel()` instead of manual prop+emit pattern. BodyEditor has two models: `defineModel<string>()` for body content and `defineModel<string>('bodyType')` for body type.
- **HTTP color composable:** Created `~/composables/useHttpColors.ts` with `METHOD_COLORS` (for UBadge semantic colors), `METHOD_CSS_COLORS` (for CSS class-based coloring), `methodColor()`, `methodCssColor()`, `statusSeverity()`. Used in `history.vue`, `StatusBadge.vue`, `MethodSelector.vue`. Uses `satisfies` for type safety.
- **`any` type cleanup (mcp.vue):** `tools`, `resources`, `prompts` refs now properly typed with `McpTool[]`, `McpResource[]`, `McpPrompt[]`. `selectedTool`/`selectedPrompt` typed with `McpTool | null` / `McpPrompt | null`. Result refs use `Record<string, any> | null` (dynamic API responses).
- **Nuxt auto-import cleanup:** Removed explicit `import { ref } from 'vue'` from `useCollectionDragDrop.ts`, `import { ref, nextTick } from 'vue'` from `InlineRename.vue`, and unused `import type { UseFetchOptions } from 'nuxt/app'` from `useApi.ts`.
- **Minor wins:** `dashboard.vue` nav items changed from `computed()` to plain `const` (no reactive deps). Removed dead `deleteRequest()` from `index.vue`. Changed four `v-if` blocks to `v-else-if` chain in `RequestBuilder.vue`. Removed redundant `safeCollections` computed from `CollectionSidebar.vue` (props already defaults to `[]`).
- **Pattern:** `defineModel<T>('propName', { default: defaultVal })` for named models (like bodyType). Default unnamed model uses `defineModel<T>({ default: val })`.
- **Key file:** `composables/useHttpColors.ts` is the single source of truth for HTTP method and status code colors.

### 2025-07-18: Delete Request from Sidebar (209/209 tests GREEN)
- **Feature:** Added hover-reveal trash icon on request items in sidebar. Clicking shows a confirmation modal, then deletes via API.
- **CollectionSidebar.vue:** Added `delete-request` emit with `{ requestId, requestName }`. Added `<button class="delete-icon">` with `<UIcon name="i-lucide-trash-2">` inside each `.request-item`. Uses `@click.stop` to prevent triggering selection or drag. CSS: `.delete-icon` is `opacity: 0` by default, `.request-item:hover .delete-icon` sets `opacity: 1`, icon turns red on `:hover`.
- **CollectionTreeFolder.vue:** Same trash icon pattern for requests inside folders. Added `delete-request` emit, forwarded from recursive sub-folders.
- **index.vue:** Added `showDeleteRequestModal` + `deleteTarget` refs. `onDeleteRequest` opens a `<UModal>` confirmation ("Delete '{name}'? This action cannot be undone.") with Cancel (ghost) and Delete (error/red) buttons. On confirm: calls `api.deleteRequest()`, reloads collections + requests, clears selection if deleted was active, shows "Request deleted" toast.
- **useApi.ts:** `deleteRequest` already existed — DELETE to `/api/requests/{id}`. No changes needed.
- **Pattern:** For hover-reveal action icons on list items, use `opacity: 0` on the icon + `.parent:hover .icon { opacity: 1 }` with CSS transition. Always use `@click.stop` to isolate from parent click handlers. Always use `<button>` for accessibility.

### 2025-07-18: Collection Inline Rename (209/209 tests GREEN)
- **Feature:** Added inline rename to collection names in sidebar. Double-click collection name → edit inline → Enter saves → persists via API. Same UX pattern as request rename.
- **CollectionSidebar.vue:** Replaced static `<span class="collection-name">` with `<InlineRename>` component. Added `rename-collection` emit with `{ collectionId, name }`. InlineRename was already imported.
- **index.vue:** Added `@rename-collection="renameCollection"` handler that calls `api.updateCollection()`, reloads collections, and shows "Collection renamed" toast.
- **useApi.ts:** Added `updateCollection(id, data)` method — PUT to `/api/collections/{id}`.
- **Click conflict fix:** Added `@click.stop` to InlineRename's `<input>` element to prevent clicks during editing from bubbling up and toggling collection collapse. Added `@dblclick.stop` on the display span so double-click to rename doesn't also bubble to the header. Single-click on collection name still bubbles normally for collapse/expand.
- **Pattern:** When embedding InlineRename inside a clickable parent (like collection-header with toggle), the input must stop click propagation to avoid parent side-effects during editing.

### 2025-07-18: CollectionSidebar Collapse/Expand + Inline Rename (11/11 tests GREEN)
- **Collapse/Expand:** Added reactive `collapsedCollections` Set to track collapsed state. All collections start expanded. Clicking the collection header toggles collapse. Requests/folders wrapped in `v-show` for performance (keeps DOM alive for drag-drop). Chevron rotates 90° via `.chevron-expanded` CSS class with `transition: transform 0.2s ease`.
- **Inline Rename:** Replaced static `<span class="request-name">` with `<InlineRename>` component. Double-click triggers edit mode; Enter saves, Escape cancels. Emits `rename-request` with `{ requestId, name }`. Wired in `index.vue` to call `api.updateRequest()` and reload collections/requests.
- **Pattern:** Used `v-show` instead of `v-if` for collapse to preserve drag-drop DOM state. `InlineRename` uses `@dblclick` which doesn't interfere with single-click request selection or drag-start.
- **Key decision:** `data-testid="request-name-${request.id}"` placed on InlineRename component root for test targeting.

### 2025-07-18: CollectionSidebar & CollectionTreeFolder Visual Polish (24/24 tests GREEN)
- **Problem:** Sidebar tree was flat, unstyled text with no visual hierarchy — "GETUntitled Request" smushed together, no icons, no hover/active states, no separators.
- **CollectionSidebar.vue changes:** Added `<UIcon>` for folder + chevron icons in collection headers. Added `:class="'method-' + request.method.toLowerCase()"` binding to method badges for color coding. Full scoped CSS: collection-node separators, collection-header flex layout with hover, collection-count pill badge, request-item indent + hover + active left-border accent, method-badge monospace/color-coded (GET=green, POST=blue, PUT=orange, PATCH=purple, DELETE=red), request-name truncation, sidebar-search focus ring, empty-state centered layout.
- **CollectionTreeFolder.vue changes:** Matching method-badge color class binding. Matching scoped CSS for folder-header, folder-icon, folder-name, folder-content indent, request-item, method-badge colors, request-name truncation.
- **Dark mode:** All colors use CSS variables (`--ui-text`, `--ui-text-muted`, `--ui-text-dimmed`, `--ui-border`, `--color-primary-500`) with fallbacks. Hover/active backgrounds use `rgba()` with neutral opacity that works in both light and dark modes.
- **Pattern:** `rgba(128, 128, 128, 0.08)` is a reliable hover background that works in both light and dark modes without needing color-mode-specific rules.
- **Tests:** All 11 CollectionSidebar + 13 CollectionTree tests pass. UIcon renders as stub in plain `mount()` tests (no Nuxt context) without breaking text/class assertions.

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

### 2025-07-19 — Form Data Editor in BodyEditor (GREEN, 18/18 tests)
- **Component modified:** `app/components/request-builder/BodyEditor.vue` — added form-data key-value table editor
- **Features:** Table with Key/Value/Actions columns, Add Field button, remove (✕) per row, data-testid attributes (`formdata-key-input`, `formdata-value-input`, `remove-formdata`, `add-formdata`, `formdata-table`)
- **Serialization:** URL-encoded format (`key1=value1&key2=value2`) via `encodeURIComponent`/`decodeURIComponent`. Handles `+` as space, empty strings, single pairs, encoded special characters

### 2026-04-02: Code Optimization Review — Dutch Frontend Findings

Optimization review by Dutch identified 8 categories of frontend optimization opportunities:

**HIGH-IMPACT REFACTORING (4):**
1. **Shared type definitions** — `Collection`, `CollectionFolder`, `CollectionRequest` interfaces duplicated in `CollectionSidebar.vue`, `CollectionTree.vue`, `CollectionTreeFolder.vue`, `CollectionPicker.vue`. Import from `useApi.ts` instead.
2. **defineModel adoption** — 5 components using manual `defineProps`+`defineEmits` pattern: `UrlInput`, `MethodSelector`, `HeadersEditor`, `BodyEditor`, `EnvironmentSelector`. Vue 3.4+ `defineModel()` eliminates boilerplate entirely.
3. **KeyValueEditor extraction** — Key-value pair table (add/remove row) reimplemented in `HeadersEditor.vue`, `QueryParamsEditor.vue`, `BodyEditor.vue` (form-data), `mcp.vue` (env vars/custom headers). Create single reusable component (~200 lines consolidated).
4. **mcp.vue decomposition** — Largest file at 1272 lines (63KB), 4x typical size. Split into: `McpServerList`, `McpConnectionForm`, `McpCapabilityTabs`, `McpToolPanel`, `McpResourcePanel`, `McpPromptPanel`, `McpRpcHistory`.

**MEDIUM-IMPACT (2):**
5. **Color mapping consolidation** — Method→color mapping in `history.vue`, `MethodSelector.vue`, `CollectionSidebar.vue`, `CollectionTreeFolder.vue` (CSS). Status→color in `history.vue` and `StatusBadge.vue`. Create shared utilities.
6. **Type annotations** — `mcp.vue` uses `any` for tools/resources/prompts. Types exist in `useApi.ts`. Replace `any` with proper types.

**LOW-IMPACT (2):**
7. **Nuxt auto-import cleanup** — Remove unnecessary explicit imports of `ref`, `computed`, `nextTick` from 'vue' (Nuxt auto-imports). Remove unused `UseFetchOptions` from `useApi.ts`.
8. **Dashboard constant in computed** — `dashboard.vue` wraps constant array in `computed()` without reactivity. Use plain `const`.

**Estimated effort:** Items 1-4 = 4-6 hours. Items 5-8 = 3-5 hours. Test updates = 3-4 hours.

Full details: `.squad/decisions/decisions.md` and `.squad/orchestration-log/2026-04-02T14-54-dutch.md`
- **State management:** Internal `formDataEntries` ref (array of `{key, value}`), `suppressFormDataSync` flag to prevent recursive watch loops (same pattern as QueryParamsEditor)
- **Bidirectional sync:** `watch(modelValue)` parses URL-encoded string → entries when bodyType is form-data. `emitFormDataUpdate()` serializes entries → URL-encoded string and emits `update:modelValue`
- **Mode isolation:** Form-data table only renders when `bodyType === 'form-data'`. Textarea only renders for raw/json. None mode shows neither. All existing modes unchanged.
- **Styling:** Scoped CSS matching HeadersEditor pattern — same table layout, input sizing, button styling, `var(--ui-*)` CSS variables
- **Tests:** All 18 pass (9 original + 9 new form-data tests). Zero regressions.

### 2025-07-19 — Phase 2 MCP: Navigation + Connection UI (GREEN, 209/209 tests)
- **Nav item added:** `dashboard.vue` — added "MCP" nav item with `i-lucide-plug` icon, route `/mcp`, positioned after Environments
- **Page created:** `app/pages/mcp.vue` — full two-panel dashboard layout following environments.vue pattern
  - **Left panel:** Server list with loading spinner, empty state (icon + "No MCP servers configured" + CTA), server items with name/transport badge/status dot/hover-reveal delete
  - **Right panel:** Selected server detail with connection form + capability tabs
  - **Connection form:** Server name, transport type radio selector (stdio/Streamable HTTP), type-conditional fields (command/args/env vars for stdio, URL for HTTP), Connect/Disconnect buttons styled like Send button, live status indicator (🔴/🟡/🟢)
  - **Env vars editor:** Replicates HeadersEditor key-value table pattern with add/remove rows
  - **Capability tabs:** Tools/Resources/Prompts with placeholder content, connected/disconnected states
  - **New Server modal:** UModal with name + transport type + type-specific fields
- **API methods added to `useApi.ts`:**
  - Types: `McpServerConfig`, `McpTool`, `McpResource`, `McpPrompt`
  - Server configs: `getServerConfigs`, `createServerConfig`, `updateServerConfig`, `deleteServerConfig`
  - Connection: `mcpConnect`, `mcpDisconnect`, `mcpStatus`
  - Operations: `mcpListTools`, `mcpCallTool`, `mcpListResources`, `mcpReadResource`, `mcpListPrompts`, `mcpGetPrompt`, `mcpPing`
- **Styling:** Scoped CSS with CSS variables for dark mode, transport badges (purple for stdio, blue for HTTP), `rgba(128,128,128,0.08)` hover backgrounds, smooth transitions
- **Pattern:** Transport type selector uses hidden radio inputs + styled labels with `.active` class — visually toggle-like but semantically correct. Env vars stored as JSON string (same pattern as headers).
- **Tests:** All 209 pass, zero regressions. Backend endpoints not yet built (Marcus) — UI structure is wired and ready.

### 2025-07-19 — Phase 3 MCP: Capability Browser UI (GREEN, 209/209 tests)
- **Feature:** Replaced placeholder capability tabs in `mcp.vue` with fully interactive Tools/Resources/Prompts browser and request/response history panel
- **Tools tab:** Fetches tool list via `mcpListTools`, clickable list with selection highlight, auto-generated form fields from `inputSchema` (string→text, number→number, boolean→checkbox, object/array→JSON textarea), required field red asterisks, "Call Tool" button with loading state, result display supporting text/image/resource content types, error styling for `isError: true`, collapsible Raw JSON view
- **Resources tab:** Fetches resource list via `mcpListResources`, each row shows name/URI/mimeType badge/description with "Read" button, per-resource loading state, content display handles text/JSON/image, Raw JSON toggle
- **Prompts tab:** Fetches prompt list via `mcpListPrompts`, clickable with argument count badge, form auto-generated from `arguments` array, "Get Prompt" button, result renders messages with role badges (user=blue, assistant=green, system=gray), Raw JSON toggle
- **Request History panel:** Collapsible bottom section tracking last 50 JSON-RPC entries, each entry shows timestamp/method/status badge, click to expand full request/response JSON. Uses `addRpcEntry()` helper called from all API methods.
- **Auto-fetch behavior:** `watch(connectionState)` resets all capability state and fetches tools on new connection. `watch(activeTab)` fetches on tab activation when data is empty.
- **Refresh button:** Each tab has a refresh UButton that re-fetches the list.
- **Styling patterns:** `.cap-*` prefixed classes for all capability UI. Uses existing CSS variable patterns (`--ui-primary`, `--ui-border`, `--ui-text-muted`). `color-mix()` for selected state backgrounds. `rgba(128,128,128,0.08)` hover backgrounds. Monospace font for code blocks and URIs. History panel with max-height scroll. Role badges with type-specific colors. Dark mode handled via `:root.dark` selectors where needed.
- **Key pattern:** Tool form generation iterates `inputSchema.properties` entries with type-dispatched input elements. Args are processed (type-converted) before sending to API — numbers parsed via `Number()`, objects/arrays via `JSON.parse()` with fallback.
- **Tests:** All 209 pass, zero regressions.

