# Dutch — History

## Project Context

- **Project:** APIneer — a locally running API platform (Postman alternative)
- **Owner:** boclifton-MSFT
- **Stack:** Nuxt 4 (Vue 3), TypeScript, Pinia, Tailwind CSS, Nuxt UI v4
- **Frontend location:** `src/ui/`
- **Frontend tests location:** `src/ui/tests/`
- **Test stack:** Vitest, MSW (Mock Service Worker), @nuxt/test-utils
- **My focus:** TypeScript/Vue/Nuxt code optimization, modernization, and simplification

## Learnings

### Frontend Architecture (2025-07-18)
- **Composables:** `useApi.ts` (API client, all type definitions), `useCollectionDragDrop.ts` (shared drag state)
- **Pages:** `index.vue` (request builder, main workspace), `collections.vue`, `environments.vue`, `history.vue`, `mcp.vue` (MCP server management — largest file at 1272 lines)
- **Layout:** Single `dashboard.vue` layout with collapsible sidebar + Nuxt UI v4 dashboard components
- **Component tree:** `request-builder/` (5 components), `response/` (4 components), `collections/` (5 components), `auth/` (1), `environments/` (1), `import-export/` (1), `app/` (1 — CommandPalette)
- **Pattern:** All pages use `onMounted` + manual `ref<boolean>` loading state (not `useAsyncData`)
- **Pattern:** Toast notifications follow `{ title, color, icon? }` shape throughout
- **Known duplication:** Type interfaces (Collection/Folder/Request) duplicated across 4+ collection components instead of importing from `useApi.ts`
- **Known duplication:** Key-value editor pattern repeated in headers, query params, body form-data, MCP env vars, MCP headers
- **Known duplication:** Method color and status color mappings defined separately in multiple files
- **v-model pattern:** 5 components use manual `defineProps`+`defineEmits` instead of `defineModel`
- **Nuxt auto-imports:** Several files explicitly import `ref`, `nextTick`, `computed` from 'vue' and components from relative paths unnecessarily
- **`useApi.ts`:** Unused import `UseFetchOptions`, `sendRequest()` contains inline status text map and header parsing logic that could be extracted
- **`mcp.vue`:** Uses many `any` types for tools/resources/prompts despite types existing in `useApi.ts`
- **`mcp.vue`:** Uses many `any` types for tools/resources/prompts despite types existing in `useApi.ts`
- **Test stack:** Vitest + @nuxt/test-utils + MSW, test files mirror component names in `tests/components/`

### 2026-04-02 — Frontend Optimization Phase A Complete

**Status:** ✅ Completed

Kratos implemented 6/8 Phase A optimization items from the architecture review:

1. **Type deduplication (4 components):** Imported `Collection`, `Folder`, `Request` types from `useApi.ts` instead of duplicating across CollectionSidebar, CollectionTreeFolder, RequestBuilder, HistoryPanel
2. **`defineModel` adoption (5 components):** Replaced manual `defineProps() + defineEmits()` pairs with declarative `defineModel<T>()` in RequestBuilder, ResponsePanel, AuthEditor, EnvironmentEditor, FormDataEditor
3. **HTTP color consolidation:** Created `composables/useHttpColors.ts` — single source of truth for method colors, status severity, and CSS color names. Eliminated duplication across 5 components.
4. **Type safety in mcp.vue:** Replaced `any` types with proper interfaces from `useApi.ts` (McpTool[], McpResource[], McpPrompt[], environment variables)
5. **Nuxt auto-import cleanup (3 files):** Removed redundant imports of `ref`, `nextTick`, `computed` from 'vue' in RequestBuilder, CollectionSidebar, EnvironmentManager
6. **Minor wins:** Dead code removal, v-else-if simplification, redundant computed property elimination

**Phase B (deferred):** KeyValueEditor extraction (shared across headers, query params, form-data, MCP env vars, MCP headers) and mcp.vue decomposition (1272-line monolith) deferred for separate session.

**Test results:** 224/224 passing, zero breaking changes, all optimizations backwards-compatible.
