# Project Context

- **Owner:** boclifton-MSFT
- **Project:** APIneer — a locally running API platform (Postman alternative). Desktop app for building, testing, and managing API requests with collections, environments, and response visualization.
- **Stack:** .NET 10 (backend), Nuxt UI v4 (frontend)
- **Created:** 2026-03-30

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

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
