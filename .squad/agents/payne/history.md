# Project Context

- **Owner:** boclifton-MSFT
- **Project:** APIneer — a locally running API platform (Postman alternative). Desktop app for building, testing, and managing API requests with collections, environments, and response visualization. Handles credentials (API keys, OAuth tokens, basic auth) so security is critical.
- **Stack:** .NET 10 (backend) + Nuxt UI (frontend)
- **Created:** 2026-03-30

# Project Context

- **Owner:** boclifton-MSFT
- **Project:** APIneer — a locally running API platform (Postman alternative). Desktop app for building, testing, and managing API requests with collections, environments, and response visualization. Handles credentials (API keys, OAuth tokens, basic auth) so security is critical.
- **Stack:** .NET 10 (backend) + Nuxt UI (frontend)
- **Created:** 2026-03-30

## Learnings

### Phase 5.4: Auth Engine Security Review (2026-03-31)

**Objective:** Review the fully implemented auth engine for security vulnerabilities.

**Scope:** 
- `Auth/AuthHandler.cs` — Auth credential injection (API key, bearer, basic, OAuth2)
- `Auth/AuthConfig.cs` — Auth configuration model
- `Services/CredentialProtector.cs` — DPAPI encryption (already reviewed in Phase 4.4)
- `Program.cs` — Auth integration with /send endpoint

**Findings:** 🔴 **9 CRITICAL FINDINGS** (3 HIGH + 4 MEDIUM + 2 LOW/INFO)

**Critical Issues Identified:**
1. **P5-001 (HIGH):** AuthConfig serialization exposes all secrets plaintext in SQLite
   - AuthConfig contains unencrypted properties (Token, Password, ClientSecret, AccessToken, KeyValue)
   - Serialized to JSON string in ApiRequest.AuthConfig column
   - DPAPI encryption layer completely bypassed
   - **Violates Invariant 3 (Encryption at Rest)**

2. **P5-002 (HIGH):** OAuth2 tokens cached plaintext in memory
   - AccessToken stored in mutable AuthConfig properties
   - If AuthConfig persisted/logged, cached token exposed
   - **Violates Invariant 3, Invariant 6**

3. **P5-003 (HIGH):** Client secret sent in plaintext to token endpoint
   - No HTTPS validation on token endpoint URL
   - Client credentials sent in plain form body
   - Vulnerable to MITM if endpoint is HTTP
   - **Critical risk for OAuth2 flow**

4. **P5-004 (MEDIUM):** API key in query string not redacted in logs
   - Query parameters appended to request URL
   - Full URL logged in RequestHistory without sanitization
   - **Violates Invariant 6 (No plaintext in logs)**

5. **P5-005 (MEDIUM):** OAuth2 error messages echo server responses
   - Exception messages contain token endpoint errors
   - Information disclosure of auth server details

6. **P5-006 (MEDIUM):** Auth type validation too permissive
   - Null type triggers NullReferenceException (not caught)
   - Unknown types throw but not logged

7. **P5-007 (MEDIUM):** No HTTPS enforcement for OAuth2 endpoint
   - HttpClient configuration lacks certificate validation
   - Token endpoint could be HTTP without error

8. **P5-008 (LOW):** AccessToken mutability
   - Token can be modified after caching
   - No versioning or auditing

9. **P5-009 (INFO):** No audit logging for token operations
   - No visibility into token refresh lifecycle

**Blockers for Phase 5.5:**
- ⛔ **P5-BLOCKER-1:** AuthConfig must be encrypted before request execution
- ⛔ **P5-BLOCKER-2:** RequestHistory must be sanitized before storage

**Positive Findings:**
- ✅ CredentialProtector.cs DPAPI implementation correct
- ✅ AuthHandler.ApplyBearer() token injection correct
- ✅ AuthHandler.ResolveAuth() inheritance logic sound
- ✅ AuthHandler.ApplyBasic() Base64 encoding correct

**Deliverables:**
- `.squad/decisions/inbox/payne-auth-security-review.md` — Full security report with 9 findings, blockers, test cases, and fix recommendations
- `tests/APIneer.Api.Tests/Security/AuthSecurityTests.cs` — 9 security regression tests (currently skipped pending fixes):
  - TC5.1: Bearer token not stored plaintext
  - TC5.1 (API Key variant): API key not stored plaintext  
  - TC5.1 (Basic variant): Password not stored plaintext
  - TC5.2: OAuth2 token not cached in database
  - TC5.4: OAuth2 HTTPS enforcement
  - TC5.3: API key query parameter redacted
  - TC5.5: Auth type null validation
  - Plus invalid type rejection test

**Test Results:**
- ✅ AuthSecurityTests.cs compiles (with 1 pre-existing ProxyEngine test compilation issue unrelated to auth review)
- ✅ 9 tests written and properly skipped pending implementation fixes
- ✅ All tests are regression tests targeting security invariants

**Risk Assessment:**
- **Overall Risk Level:** 🔴 **CRITICAL**
- **Immediate Threat:** Plaintext secrets in SQLite database
- **Attack Surface:** Local file access to SQLite + auth config inspection
- **Compliance Impact:** GDPR (credential exposure), SOC 2 (encryption at rest)

**Next Steps (Phase 5.5):**
1. Refactor AuthConfig to separate concerns:
   - Move all secret properties to encrypted storage
   - Create EncryptedAuthSecrets DTO with DPAPI layer
2. Implement in-memory token cache (ConcurrentDictionary + SemaphoreSlim)
3. Add request history sanitization (redact URLs, auth headers)
4. Validate OAuth2 endpoints are HTTPS-only
5. Fix auth type validation (explicit whitelist)
6. Run security regression tests to verify fixes

---

**Objective:** Implement the Data Protection encryption layer for secrets per security-architecture.md

**Implementation:**
1. **CredentialProtector Service** (`src/api/APIneer.Api/Services/CredentialProtector.cs`)
   - ICredentialProtector interface with Encrypt/Decrypt methods
   - Uses Microsoft.AspNetCore.DataProtection (DPAPI)
   - Keys persisted to OS-protected directory (`%APPDATA%\APIneer\keys\`)
   - Logs at DEBUG level for audit trail (key names, not values)

2. **Program.cs Configuration**
   - Added `builder.Services.AddDataProtection().PersistKeysToFileSystem()`
   - Registered `ICredentialProtector` as scoped dependency
   - All three environment variable endpoints (POST/PUT/GET) integrated with encryption/decryption

3. **Encryption Strategy**
   - **On Write:** When IsSecret=true, plaintext encrypted via DPAPI → base64-encoded → stored in Value
   - **On Read (API):** MapToVariableResponse masks with `***masked***` when IsSecret=true
   - **On Read (Resolution):** /api/environments/resolve decrypts on backend for variable substitution
   - Supports mixed plaintext + secret variables in same environment

4. **Security Tests** (17 tests, all passing)
   - `CredentialProtectorTests.cs` (10 tests)
     - Encrypt/decrypt round-trip with various secret formats (API keys, tokens, passwords)
     - Verifies ciphertext is non-trivial (not plaintext)
     - Error handling for null/empty inputs
     - Decryption failures on invalid ciphertext
   - `EncryptedSecretVariableTests.cs` (7 tests)
     - Invariant 1: No raw secrets in API responses
     - Invariant 3: Encrypted storage verified
     - Invariant 6: Plaintext never visible in logs
     - Mixed plaintext + secret variables resolve correctly
     - Update encryption changes (old secret not used after update)

**Test Results:**
- ✅ 60/60 Environments + Security tests pass (10 credential protector + 7 integration + 43 existing env tests)
- ✅ Build succeeds with 0 errors
- ✅ Encryption/decryption verified at both storage and resolution layers

**Security Invariants Compliance:**
- ✅ Invariant 1 (No Raw Secrets in API Responses): PASS — POST/GET/PUT responses show `***masked***`
- ✅ Invariant 2 (Masking in UI): PASS — Frontend receives masked responses
- ✅ Invariant 3 (Encrypted at Rest): PASS — Secrets stored as encrypted base64 in SQLite
- ✅ Invariant 6 (No Plaintext in Logs): PASS — Only key names logged, not values

**Edge Cases Handled:**
- Empty/null secret values rejected (ArgumentNullException)
- Invalid ciphertext decryption throws InvalidOperationException (caught in endpoint, returns 400)
- Variable type changes (plaintext ↔ secret) correctly re-encrypt on update
- Switching environments switches active secrets (environment scope respected)

**Key Design Decisions:**
1. **Base64 Encoding:** Encrypted bytes stored as base64 in Value field for database compatibility
2. **Per-Request Decryption:** Secrets decrypted at resolution time (not on GET) to minimize plaintext lifetime
3. **Audit Logging:** Only symmetric logging (key name, timestamp) — never the decrypted value
4. **Error Messages:** User sees "Failed to encrypt/decrypt secret value" without technical details

**Blockers Resolved:**
- P2-002 (MEDIUM): EnvironmentVariable plaintext → FIXED with DPAPI layer
- Secrets no longer violate Invariant 3

**Known Limitations:**
- Key rotation not implemented (Phase 1.6 future work)
- No master password (optional user protection, Phase 1.6 future work)
- DPAPI is machine-bound (not portable across machines)

**Next Phase (4.5+):**
- RequestHistory sanitization (P2-001 blocker)
- Verbose error message reduction (P2-003)
- Audit logging for request execution (P2-004)

**Deliverables:**
- Services/CredentialProtector.cs (78 lines, 2 classes)
- Program.cs updated (3 endpoints modified, 1 service configured)
- Security/CredentialProtectorTests.cs (154 lines, 10 tests)
- Security/EncryptedSecretVariableTests.cs (289 lines, 7 tests)

---

### Phase 8.3: Final Security Audit (2026-03-31)

**Objective:** Comprehensive final security review of entire `src/api/` codebase before v1.0 release.

**Scope:** All endpoints, proxy engine, auth engine, import/export, WebSocket, dependency audit, secrets scan, data flow analysis.

**Methodology:**
- Dependency audit: `dotnet list package --vulnerable` — Zero CVEs
- Secrets scan: All files scanned for hardcoded credentials — None found
- Vulnerability deep scan: Injection, auth, data handling, cryptography, business logic
- Cross-file data flow: Traced credential lifecycle from input → storage → resolution → injection → response
- Self-verification: Reassessed all previous findings (P2-001...P5-009)

**Findings Summary:**

🔴 **2 CRITICAL + 2 HIGH + 2 MEDIUM = 6 UNRESOLVED SECURITY BLOCKERS**
✅ **3 findings FIXED since Phase 5.4** (P2-002, P5-006, partial P2-003)

**Critical Findings (Release Blockers):**
1. **P5-001 (CRITICAL):** AuthConfig secrets still plaintext in SQLite — must encrypt before auth integration
2. **P5-002 (CRITICAL):** OAuth2 access tokens cached plaintext — must move to in-memory cache only

**High Findings (Release Blockers):**
3. **P8-001 (HIGH):** Request secrets (headers, body, auth) stored plaintext in RequestHistory — violates Invariant 6. Users sharing exports leak credentials.
4. **P8-002 (HIGH):** Auth secrets remain mutable public fields, serializable to JSON, persisted to SQLite — violates Invariants 1, 3, 6

**Medium Findings:**
5. **P8-003 (MEDIUM):** Verbose exception messages leak implementation details (import endpoints, ProxyEngine, WebSocket) — follow-up on P2-003
6. **P8-004 (MEDIUM NEW):** WebSocketProxy singleton stores full message history in memory, exposed to all callers via `/api/ws/messages` and `/api/ws/status` — no access control or per-session scoping

**Fixed Findings:**
- ✅ **P2-002:** EnvironmentVariable secrets now encrypted via DPAPI, masked on read (Phase 4.4)
- ✅ **P5-006:** Null auth type handling fixed — no null dereference (Phase 5.5)

**Still Open:**
- 🔴 **P2-001:** RequestHistory plaintext (related to P8-001)
- 🔴 **P2-003:** Verbose errors (partial — some fixed, import/export still leaking)
- 🔴 **P5-003:** OAuth2 token endpoint HTTPS not enforced
- 🔴 **P5-004:** API key query string not redacted (related to P8-001)
- 🔴 **P5-005:** OAuth2 error messages expose server details
- 🔴 **P5-007:** No HTTPS validation on OAuth2 endpoint
- 🔴 **P5-008:** AccessToken mutable/public set (secondary to P5-002)
- ⚪ **P5-009:** No audit logging (post-release enhancement)

**Data Flow Analysis:**

Request lifecycle shows plaintext leakage at every stage:
```
User Input (plaintext) 
  → ApiRequest.Headers/Body/AuthConfig (plaintext) 
  → SQLite storage (plaintext) 
  → RequestHistory (plaintext) 
  → GET /api/requests/{id}/history response (plaintext) ← INVARIANT 6 VIOLATION
  → User exports collection (plaintext) ← CREDENTIALS LEAKED
```

EnvironmentVariable secrets are correctly encrypted via DPAPI, but request-level secrets (headers, auth) bypass encryption entirely.

**Vulnerability Categories:**
- ✅ No SQL injection (EF Core parameterized)
- ✅ No XSS (JSON responses only)
- ✅ No header injection
- 🔴 Auth secrets plaintext (P5-001, P5-002, P8-002)
- 🔴 Data handling plaintext (P8-001)
- 🔴 Info disclosure via errors (P8-003)
- 🔴 Access control gaps (P8-004)

**Security Invariants Compliance:**
- Invariant 1 (No raw secrets in responses): 🔴 FAIL — RequestHistory returns plaintext
- Invariant 2 (Masked in UI): ✅ PASS — EnvironmentVariable masked
- Invariant 3 (Encrypted at rest): 🔴 PARTIAL — Variables encrypted, AuthConfig/RequestHistory plaintext
- Invariant 4 (Headers stripped): ⏳ UNKNOWN — Auth integration deferred
- Invariant 5 (Collections sanitized): ⏳ UNKNOWN — Not implemented
- Invariant 6 (No plaintext in logs): 🔴 FAIL — RequestHistory stores plaintext
- Invariant 7 (Scoping respected): ✅ PASS — Variables correctly scoped

**Release Readiness:** 🔴 **NOT READY FOR v1.0**

All 6 blockers must be fixed before release:
1. Encrypt request secrets before storage (P8-001)
2. Encrypt auth secrets, remove from schema (P8-002)
3. Enforce OAuth2 HTTPS (P5-003)
4. Scope WebSocket state per session (P8-004)
5. Redact error messages (P8-003)
6. Implement request history sanitization (P2-001)

**Estimated Remediation:** 9-10 days

**Deliverables:**
- `docs/security-audit-final.md` — Full report (19.5KB) with executive summary, detailed findings by severity, status of all previous findings, data flow analysis, invariants compliance, release checklist, recommendations, test coverage gaps

**Key Decisions:**
- No SSRF protection is intentional (tool designed for localhost testing)
- Secrets in imports/exports should be redacted when generating code
- WebSocket feature requires authentication gate if shipping
- Auth engine integration must not proceed without P5-001, P5-002 fixes
- Error messages should never echo user input or show implementation details

**Architecture Assessment:** ✅ Sound — threat model, invariants, DPAPI implementation correct
**Implementation Status:** 🔴 Vulnerable — Request/auth secret handling gaps critical
**Overall Risk:** 🔴 CRITICAL — Not suitable for production release in current state

---

### 2026-03-30: Phase 2.9 Security Review — Proxy Engine + Request API
- **Scope:** ProxyEngine.cs, Program.cs endpoints, Models (ApiRequest, EnvironmentVariable, RequestHistory)
- **Review against:** Security Architecture Document v1.0
- **Findings:** 1 HIGH, 2 MEDIUM, 1 LOW, 2 INFO
  - **P2-001 (HIGH):** RequestHistory stores plaintext secrets in SQLite — violates Invariant 6. Fix: sanitize headers/body before storage.
  - **P2-002 (MEDIUM):** EnvironmentVariable.Value plaintext (no DPAPI) — violates Invariant 3. Fix: implement DPAPI encryption layer.
  - **P2-003 (MEDIUM):** Verbose error messages expose context (URL echo, implementation details) — violates defense-in-depth. Fix: generic messages to frontend, detailed logs server-side.
  - **P2-004 (LOW):** No audit trail for request execution — observability gap. Recommendation: add server-side audit logging.
  - **P2-005 (INFO):** ProxyEngine stub in /send endpoint — known placeholder, integration deferred.
- **Positive findings:** 10MB size limit enforced, timeout configured (1-300s), SSRF intentionally permissive, method validation present, URL validation robust, redirect limit (20), error handling never throws, resource disposal correct
- **Overall Risk:** MEDIUM — findings addressable with targeted fixes. No CRITICAL vulnerabilities.
- **Recommendations:** Immediate P2-001/P2-003 fixes, Phase 1.5 DPAPI implementation, pre-production audit of history logs and assertion tests
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
