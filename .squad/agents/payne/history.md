# Project Context

- **Owner:** boclifton-MSFT
- **Project:** APIneer — a locally running API platform (Postman alternative). Desktop app for building, testing, and managing API requests with collections, environments, and response visualization. Handles credentials (API keys, OAuth tokens, basic auth) so security is critical.
- **Stack:** .NET 10 (backend) + Nuxt UI (frontend)
- **Created:** 2026-03-30

## Learnings

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
