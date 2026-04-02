# Decisions

## Code Optimization Review — 2026-04-02

### Arthur — C# Modernization & Optimization (16 Findings)

**Review scope:** Full codebase `src/api/APIneer.Api/` and `tests/APIneer.Api.Tests/` (45 files)  
**Focus:** .NET 10 / C# 13+ patterns, performance, readability

**HIGH-IMPACT — Performance (5):**
1. **Static JsonSerializerOptions** — `Program.cs` lines ~600, ~615 allocate `new JsonSerializerOptions { PropertyNameCaseInsensitive = true }` per request. Move to static readonly field to leverage System.Text.Json caching.
2. **ExecuteDelete for bulk deletions** — `Program.cs` line ~778 and `ApiTestFixture.cs` lines ~47-55 use `RemoveRange` + `SaveChanges` (materializes all rows). Replace with `ExecuteDeleteAsync()` for direct SQL DELETE.
3. **FrozenSet for validation sets** — `Program.cs` lines ~141, ~785, ~1276, ~1562 use `HashSet<string>` for read-only validation. Use `FrozenSet<string>` instead (optimized for read-heavy, write-never).
4. **ExecuteUpdate for batch updates** — `Program.cs` lines ~1053-1059 load sibling environments and mutate each. Use `ExecuteUpdateAsync()` for single SQL UPDATE.
5. **N+1 query in reorder endpoint** — `Program.cs` lines ~330-335 call `FindAsync()` per item. Load all items in one query with `Where().ToListAsync()` then update.

**HIGH-IMPACT — Readability (4):**
6. **Primary constructors** (4 files) — `AuthHandler.cs`, `CredentialProtector.cs`, `McpConnectionManager.cs`, `McpConnection.cs` use traditional ctor+field boilerplate. Convert to primary constructors (~20 lines removed).
7. **Records for DTOs** — `ProxyError.cs` and `RedirectEntry.cs` are mutable classes acting as data carriers. Convert to records for equality, ToString, deconstruction.
8. **Collection expressions** — `Program.cs` line ~1423 uses `Array.Empty<object>()`. Replace with `[]` collection expression.
9. **AuthHandler switch expression** — `AuthHandler.cs` lines 28-51 use traditional switch. Consider switch expression (lower priority, complex due to async/sync mix).

**MEDIUM-IMPACT — Performance (3):**
10. **Compiled regex for variable resolution** — `Program.cs` line ~1455 compiles regex per call. Use `[GeneratedRegex]` attribute for build-time compilation.
11. **Recursive DB queries in folder deletion** — `Program.cs` `CollectDescendantFolderIds` method (lines ~1400-1412) fires one SELECT per tree level. Flatten with queue-based batch queries.
12. **WebSocketProxy StringBuilder allocation** — `ReceiveLoopAsync` allocates `StringBuilder` per message even for single-frame. Use string directly for common case.

**LOWER-PRIORITY — Code Quality (3):**
13. **Remove unused field** — `CredentialProtector.cs` stores `_provider` field never used after ctor initialization. Remove dead field.
14. **Duplicate regex in CurlImporter** — Line 18 uses `Regex.Replace` for backslash continuation. Use `[GeneratedRegex]`.
15. **StringComparison in CurlExporter** — `CurlExporter` line 35 `request.Method != "GET"` uses default comparison. Use `StringComparison.OrdinalIgnoreCase`.

**Estimated effort:** Top 5 items = ~1 hour. Full recommendations = 2-3 hours.

---

### Dutch — Frontend Optimization Review (8 Categories)

**Review scope:** All Vue/TS files in `src/ui/` (components, pages, composables, layout)  
**Focus:** Nuxt 4/Vue 3 patterns, type safety, component reuse

**HIGH-IMPACT — Refactoring (4):**
1. **Shared type definitions** — `Collection`, `CollectionFolder`, `CollectionRequest` interfaces duplicated in `CollectionSidebar.vue`, `CollectionTree.vue`, `CollectionTreeFolder.vue`, `CollectionPicker.vue`. Already exist in `useApi.ts` — import from there instead.
2. **defineModel adoption** — 5 components use manual `defineProps`+`defineEmits` pattern: `UrlInput`, `MethodSelector`, `HeadersEditor`, `BodyEditor`, `EnvironmentSelector`. Vue 3.4+ `defineModel()` eliminates ceremony entirely.
3. **KeyValueEditor extraction** — Key-value pair table (add row, remove row, key/value inputs) implemented independently in `HeadersEditor.vue`, `QueryParamsEditor.vue`, `BodyEditor.vue` (form-data mode), and `mcp.vue` (env vars/custom headers). Create single reusable `KeyValueEditor` component (~200 lines consolidated).
4. **mcp.vue decomposition** — Largest file in codebase at 1272 lines (63KB), 4x typical component size. Split into: `McpServerList`, `McpConnectionForm`, `McpCapabilityTabs`, `McpToolPanel`, `McpResourcePanel`, `McpPromptPanel`, `McpRpcHistory`.

**MEDIUM-IMPACT (2):**
5. **Color mapping consolidation** — Method→color mapping in `history.vue` (`methodColor()`), `MethodSelector.vue` (`METHOD_COLORS`), `CollectionSidebar.vue`, `CollectionTreeFolder.vue` (CSS). Status→color in `history.vue` (`statusColor()`) and `StatusBadge.vue` (`statusClass`). Create shared utilities module.
6. **Type annotations** — `mcp.vue` uses `any` for tools/resources/prompts. Types exist in `useApi.ts`. Replace `any` with proper types for IDE support and type safety.

**LOW-IMPACT (2):**
7. **Nuxt auto-import cleanup** — Remove unnecessary explicit imports of `ref`, `computed`, `nextTick` from 'vue' (Nuxt auto-imports). Remove redundant component imports. Remove unused `UseFetchOptions` from `useApi.ts`.
8. **Dashboard constant in computed** — `dashboard.vue` wraps constant array in `computed()` without reactive dependencies. Use plain `const` instead.

**Estimated effort:** Items 1-4 = 4-6 hours. Items 5-8 = 3-5 hours. Tests = 3-4 hours. Total = 10-15 hours.

---

## Implementation Coordination

### Backend Items → Marcus (Backend APIs)
- Static JsonSerializerOptions
- ExecuteDelete/ExecuteUpdate refactorings
- N+1 query fixes
- Regex compilation
- Recursive query optimization

### Frontend Items → Kratos (Frontend Components)
- Shared type definitions
- defineModel adoption
- KeyValueEditor extraction
- mcp.vue decomposition
- Color consolidation
- Type annotations

### Tests → Freeman (Test Contracts)
- Update tests for refactored components (defineModel patterns)
- Add tests for new KeyValueEditor component
- Update mcp.vue test structure
- Verify API test changes don't break test suite

---

## Status
- **Arthur findings:** Documented 2026-04-02T14:54:00Z
- **Dutch findings:** Documented 2026-04-02T14:54:00Z
- **Orchestration logs:** Created for both agents
- **Session log:** Created summarizing both reviews
