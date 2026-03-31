# Project Context

- **Owner:** boclifton-MSFT
- **Project:** APIneer — a locally running API platform (Postman alternative). Desktop app for building, testing, and managing API requests with collections, environments, and response visualization. Handles credentials (API keys, OAuth tokens, basic auth) so security is critical.
- **Stack:** .NET 10 (backend) + Nuxt UI (frontend)
- **Created:** 2026-03-30

## Learnings

### Phase 2.9: Security Review of Proxy Engine + Request API (2026-03-30)

**Review Scope:**
- ProxyEngine.cs (HTTP proxy implementation)
- Program.cs API endpoints for request execution and history
- Data models (ApiRequest, EnvironmentVariable, RequestHistory)

**Key Findings:**

1. **HIGH: RequestHistory Stores Plaintext Credentials (P2-001)**
   - `/api/requests/{id}/send` endpoint stores raw request/response in RequestHistory without sanitization
   - Violates Invariant 6: "No plaintext secrets in request logs"
   - Risk: Authorization headers, API keys, basic auth stored in plaintext SQLite
   - Fix: Implement header/body sanitization before storing; redact Authorization, API-Key, etc. with `[REDACTED]`

2. **MEDIUM: EnvironmentVariable.Value Stored Plaintext (P2-002)**
   - EnvironmentVariable.cs stores Value as string (no DPAPI encryption)
   - Violates Invariant 3: "Credentials encrypted at rest"
   - Risk: If database is accessed, all secrets are readable
   - Fix: Encrypt Value field with DPAPI before storage; only decrypt on backend at request time
   - Status: Planned for Phase 1.5, but blocks full security implementation

3. **MEDIUM: Verbose Error Messages Expose Context (P2-003)**
   - ProxyEngine echoes URLs in error messages (line 30)
   - Connection errors reveal internal implementation details
   - Fix: Replace with generic error messages to frontend; log detailed errors server-side

4. **LOW: No Audit Trail (P2-004)**
   - Request execution not logged
   - Cannot detect unauthorized activity or reconstruct execution history
   - Recommendation: Add server-side audit logging for request execution (method, URL, status, duration)

5. **INFO: ProxyEngine Integration Pending (P2-005)**
   - `/send` endpoint is currently a stub (returns hardcoded response)
   - Real integration required before header stripping can be tested
   - ProxyEngine itself is well-implemented with proper timeout/size limit handling

**Positive Findings:**
- ✅ 10MB request size limit enforced
- ✅ Timeout range (1-300s) correctly configured
- ✅ SSRF design intentionally permissive (aligns with architecture)
- ✅ 20-redirect limit prevents DoS
- ✅ Error handling is non-throwing (returns structured ProxyError)
- ✅ Resource disposal is correct (using statements)

**Invariants Compliance:**
- ❌ Invariant 3 (Encryption at Rest): FAIL — EnvironmentVariable.Value plaintext
- ❌ Invariant 6 (No Plaintext in Logs): FAIL — RequestHistory stores raw credentials
- ⏳ Invariants 1, 2, 4, 7: PARTIAL — Architecturally sound, implementation not fully tested
- ⏳ Invariant 5 (Collection Sanitization): UNKNOWN — deferred to future review

**Blockers for Phase 2.10:**
1. Must implement RequestHistory sanitization (HIGH)
2. Must implement DPAPI encryption for secrets (MEDIUM, planned Phase 1.5)
3. Must integrate real ProxyEngine into /send endpoint for header stripping validation

**Deliverables:**
- `.squad/decisions/inbox/payne-p2-security-review.md` — Full report with fixes and test cases
- Security audit trail documented for future phases

**Architecture Status:** Sound; implementation needs credential handling refinement.

---

### Cross-Agent Context (Phase 2)

#### Freeman — Test Contracts
- **Request API tests (RED):** 53 tests define request CRUD, validation, and execution contract
- **Proxy tests (RED):** 35 tests define HTTP proxy contract with structured error handling
- **UI tests (RED):** 36 builder + 35 response viewer tests define component contracts

#### Marcus — Implementation (GREEN)
- **Request API:** 7 endpoints implemented, all 53 tests pass. RequestHistory logs method/URL/timing/response data.
- **ProxyEngine:** Implemented with manual redirect handling, structured error contract, 10MB body limit, 30s timeout default.
- **DevEnv:** Root package.json with concurrently for backend+frontend orchestration.
- **Total:** 90/90 backend tests pass.

#### Kratos & Ralph — UI (GREEN)
- **Builder UI:** 5 components, 41 tests pass — MethodSelector, UrlInput, HeadersEditor, BodyEditor, RequestBuilder
- **Response UI:** 5 components, 82 tests pass — StatusBadge, ResponseBody, ResponseHeaders, ResponseTiming, ResponseViewer
- **Total:** 123 frontend tests pass.

**Security Impact for Phase 3:**
- RequestHistory sanitization still needed (P2-001 blocker)
- DPAPI encryption for EnvironmentVariable.Value still needed (P2-002 blocker)
- Verbose error messages in ProxyEngine should be addressed before Phase 3 (P2-003)

### Phase 1.4: Security Architecture Foundation (2026-03-30)

**Architecture Decisions:**
1. **Encryption at Rest:** Use .NET DPAPI for all secret variable encryption. Keys stored in OS-protected app data directory (`%APPDATA%\APIneer\keys\` on Windows, `~/.config/apineer/keys/` on Unix).
2. **Backend-Only Decryption:** Secrets ONLY decrypted server-side at request execution time. Frontend receives only variable names and `***masked***` representations. Never send raw secrets to UI.
3. **Auth Header Injection Pattern:** Credentials resolved → headers injected → request sent → injected headers stripped from response before returning to frontend.
4. **Intentional SSRF Permissiveness:** No SSRF protection by design. Tool IS meant for localhost/internal API testing. User is responsible for understanding request targets.
5. **Log Sanitization Rule:** All logs use `[REDACTED]` for sensitive values. No raw tokens, API keys, or passwords in logs.

**Key Files:**
- `docs/security-architecture.md` — Complete security design (threat model, invariants, test stubs)

**Security Invariants (enforced by future tests):**
1. Raw secrets never in API responses
2. Secret variables always masked in UI
3. Encrypted storage in SQLite (DPAPI)
4. Injected auth headers removed from response logs
5. Imported collections sanitized
6. No plaintext secrets in request logs
7. Environment variable scoping respected

**Out-of-Scope Threats:**
- Network-level security (user's responsibility)
- OS-level access control (relying on file permissions)
- Host machine compromises (assumed trusted)

**Next Phase (1.5):**
- Implement DPAPI encryption layer
- Add request/response sanitization
- Implement log redaction
- Write security tests based on invariants

## Cross-Agent Context (Phase 1)

### Backend (Marcus)
- **Location:** `src/api/APIneer.Api/` — .NET 10 Minimal API on `localhost:5000`
- **Database:** EF Core SQLite with **EnvironmentVariable** model (key, value, environment_id, is_secret)
- **7 models:** Workspace, Collection, CollectionFolder, ApiRequest, Environment, EnvironmentVariable, RequestHistory
- **Test fixture:** `ApiTestFixture.cs` uses WebApplicationFactory + in-memory SQLite (shared connection isolation pattern)
- **CORS:** Enabled for `http://localhost:3000` (Nuxt frontend)

### Frontend (Kratos)
- **Location:** `src/ui/` — Nuxt 4.4.2 + Nuxt UI v4 on `localhost:3000`
- **API proxy:** `/api/**` → backend (routing rule in nuxt.config.ts)
- **State management:** Pinia (for environment variables, secrets state, etc.)
- **Validation:** Zod installed for schema validation
- **Key constraint:** Frontend receives variable names + `***masked***`, never raw secrets
- **Test pattern:** Vitest + MSW 2.x for API mocking (handlers in `tests/mocks/handlers.ts`)
