# Project Context

- **Project:** APIneer — a locally running API platform (Postman alternative). Desktop app for building, testing, and managing API requests with collections, environments, and response visualization.
- **Stack:** .NET 10 (backend), Nuxt UI v4 (frontend)
- **Created:** 2026-03-30

## Learnings

### 2026-03-30: Phase 2.7 — Response Viewer UI Components (GREEN)
- **5 Vue components** created in `app/components/response/`:
  - `StatusBadge.vue` — HTTP status display with color-coding: 2xx green, 3xx blue, 4xx orange, 5xx red, status-0 special case
  - `ResponseBody.vue` — response body rendering with JSON pretty-print, Pretty/Raw tabs, copy-to-clipboard button, large body truncation handling
  - `ResponseHeaders.vue` — headers table with alphabetical sort, header count display, empty state
  - `ResponseTiming.vue` — response timing metrics (duration in ms, size in B/KB/MB human-readable format)
  - `ResponseViewer.vue` — integration component with tabs (Status/Body/Headers/Timing), default tab selection, child data passing
- **All 82 tests pass** (5 test files covering edge cases and integration)
- **Key learnings:** Test-first approach enabled clean implementation — Freeman's RED tests defined exact DOM structure, CSS classes, and data-testid attributes upfront
- **Nuxt auto-import caveat:** Components in subdirectories like `components/response/StatusBadge.vue` get path-prefixed as `ResponseStatusBadge` by Nuxt. When composing child components inside parent components in the same subdirectory, use explicit `import` statements to avoid resolution failures
- **happy-dom quirk:** `navigator.clipboard` is a getter-only property. Tests fail unless `setup.ts` predefines it as writable via `Object.defineProperty`
- **Test infrastructure:** Vitest 4 + @nuxt/test-utils (environment: 'nuxt'), `mountSuspended` for Nuxt context, MSW 2 for API mocking

## Cross-Agent Context (Phase 2)

### Freeman — Response UI Tests (RED)
- **35 failing tests** defining Response Viewer contract: StatusBadge (8), ResponseBody (9), ResponseHeaders (6), ResponseTiming (5), ResponseViewer (7)
- **Component locations:** `~/components/response/*.vue`
- **Test patterns:** `mountSuspended` from @nuxt/test-utils/runtime, `data-testid` for element selection
- **All fail initially** because components don't exist until GREEN phase

### Marcus — Request API & ProxyEngine (GREEN)
- **Request API:** 7 endpoints for request CRUD, validation, send execution, history logging
- **ProxyEngine:** HTTP proxy with manual redirect handling (301/302/303→GET, 307/308→preserve), error contract (structured errors, never throws), timing/size metrics
- **Results:** 53 Request tests pass, 35 Proxy tests pass, 90/90 total backend tests

### Kratos — Builder UI (GREEN)
- **5 components:** MethodSelector, UrlInput, HeadersEditor, BodyEditor, RequestBuilder
- **41 tests pass** — all RED contracts satisfied
- **Locations:** `~/components/request-builder/*.vue`

## Recent Updates

📌 Phase 2 completed: 213 tests total (90 backend + 123 frontend). All RED contracts satisfied by GREEN implementations.

