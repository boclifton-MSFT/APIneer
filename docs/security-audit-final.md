# APIneer Phase 8.3 Final Security Audit
**Date:** 2026-03-31  
**Auditor:** Payne, Security Specialist  
**Scope:** Full `src/api/` codebase — all endpoints, proxy engine, auth engine, import/export, WebSocket  
**Status:** 🔴 **CRITICAL ISSUES REMAIN — NOT READY FOR v1.0 RELEASE**

---

## Executive Summary

This is the final comprehensive security review before v1.0 release. APIneer handles sensitive credentials (API keys, OAuth tokens, Basic Auth passwords), making security critical.

**Overall Assessment:** The codebase has **2 CRITICAL + 2 HIGH + 1 MEDIUM unresolved findings** from previous phases, plus **1 NEW MEDIUM finding** (WebSocket state leakage). **3 findings have been FIXED since Phase 5.4.**

**Release Readiness:** 🔴 **BLOCKED** — Cannot release to production with 4 high-severity credential handling vulnerabilities.

### Findings Summary by Severity

| Severity | Count | Status |
|----------|-------|--------|
| 🔴 CRITICAL | 2 | OPEN (P5-001, P5-002) |
| 🟠 HIGH | 2 | OPEN (P8-001, P8-002) |
| 🟡 MEDIUM | 2 | 1 NEW + 1 FOLLOW-UP (P8-003, P8-004) |
| ✅ FIXED | 3 | P2-002, P5-006, (1 partial on P2-003) |

---

## Detailed Findings

### 🔴 CRITICAL FINDINGS

#### **P5-001 (CRITICAL): AuthConfig Serialization Exposes Secrets Plaintext**
- **Category:** Cryptography / Data at Rest
- **Location:** 
  - `src/api/APIneer.Api/Auth/AuthConfig.cs:7-34`
  - `src/api/APIneer.Api/Models/ApiRequest.cs:17`
  - Data/Migrations/20260330220510_InitialCreate.cs (AuthConfig column)
- **Issue:** AuthConfig contains unencrypted plaintext properties: `Token`, `Password`, `ClientSecret`, `AccessToken`, `KeyValue`. Serialized to JSON string in ApiRequest.AuthConfig column in SQLite. DPAPI encryption layer completely bypassed.
- **Violates:** Security Invariant 3 (Encryption at Rest)
- **Risk:** Any local file access to SQLite database exposes all authentication credentials in plaintext.
- **Status:** 🔴 UNRESOLVED since Phase 5.4
- **Blocker:** Must fix before any auth engine integration

---

#### **P5-002 (CRITICAL): OAuth2 Tokens Cached Plaintext in Memory**
- **Category:** Cryptography / Token Management
- **Location:** `src/api/APIneer.Api/Auth/AuthHandler.cs:126-183`, AuthConfig.AccessToken property
- **Issue:** `AccessToken` stored as mutable plaintext string in AuthConfig properties. If persisted or logged, cached token exposed. OAuth2 flow stores the token back in the config object after successful grant.
- **Violates:** Security Invariant 3, Invariant 6
- **Risk:** Access token compromise if database/logs are accessed. Tokens may be replayed.
- **Status:** 🔴 UNRESOLVED since Phase 5.4
- **Blocker:** Must refactor token caching to in-memory only, never persist.

---

### 🟠 HIGH FINDINGS

#### **P8-001 (HIGH): Request Secrets Handled Plaintext Across Lifecycle**
- **Category:** Data Handling / Secrets at Rest
- **Location:**
  - `Program.cs:383-441` (POST /api/requests)
  - `Program.cs:499-621` (PUT /api/requests/{id})
  - `Program.cs:989-1148` (POST /api/requests/{id}/send)
  - `Program.cs:1412-1555` (GET /api/requests/{id}/history)
  - All import/export files: CurlImporter.cs, JsonImporter.cs, PostmanImporter.cs, JsonExporter.cs, PostmanExporter.cs, CurlExporter.cs
- **Issue:** Request definitions store raw Headers/Body. RequestHistory persists and returns RequestHeaders/RequestBody/ResponseHeaders/ResponseBody without sanitization. Import/export/code-gen paths re-emit secrets faithfully. API keys, bearer tokens, Basic auth credentials, secrets embedded in bodies are duplicated into SQLite, history APIs, exports, and generated code.
- **Violates:** Security Invariant 6 (No plaintext secrets in logs)
- **Risk:** Database theft, local file disclosure, sharing exports/history payloads exposes credentials directly to third parties.
- **Status:** 🔴 FOLLOW-UP on **P2-001** and **P5-004** — UNRESOLVED
- **Related:** P2-001 (RequestHistory HIGH), P5-004 (MEDIUM)

**Example Attack Scenario:**
1. User creates request: `GET /api/users` with `Authorization: Bearer sk-1234567890`
2. Request sent, history stored: `RequestHeaders = "Authorization: Bearer sk-1234567890"`
3. User exports collection → JSON includes all history with plaintext tokens
4. User shares exported JSON with colleague → tokens leaked

---

#### **P8-002 (HIGH): Auth Secrets Mutable and Serializable**
- **Category:** Cryptography / Authentication
- **Location:**
  - `Auth/AuthConfig.cs:7-34` (all secret fields public set)
  - `Auth/AuthHandler.cs:126-183` (OAuth2 token caching)
  - `Models/ApiRequest.cs:17` (AuthConfig serialization)
  - Migrations/20260330220510_InitialCreate.cs (schema includes AuthConfig)
- **Issue:** Auth engine carries secrets as mutable plaintext strings. OAuth2 caches AccessToken back into config after refresh. Schema persists AuthConfig. No HTTPS validation on token endpoint. Client secret sent plaintext to token endpoint.
- **Violates:** Security Invariants 1, 3, 6
- **Risk:** Credential compromise, token replay, exposure of client secrets/access tokens in memory or storage.
- **Status:** 🔴 FOLLOW-UP on **P5-001, P5-002, P5-003, P5-005, P5-007, P5-008** — UNRESOLVED
- **Blocker:** Auth engine integration cannot proceed without fixes

**Tech Details:**
- Token endpoint (P5-003): No HTTPS validation — client credentials sent in plaintext over HTTP
- Token caching (P5-002): Mutable public AccessToken property vulnerable to memory inspection
- Error exposure (P5-005): OAuth2 error bodies echoed in exceptions expose auth server details

---

### 🟡 MEDIUM FINDINGS

#### **P8-003 (MEDIUM): Verbose Exception Messages Leak Implementation Details**
- **Category:** Information Disclosure
- **Location:**
  - `Program.cs:1025-1027` (Import endpoints)
  - `Program.cs:1090-1118` (Import endpoints)
  - `Program.cs:1345-1347` (Import endpoints)
  - `Proxy/ProxyEngine.cs:57-72` (Connection errors)
  - `WebSocket/WebSocketProxy.cs:239-241` (WebSocket errors)
- **Issue:** Endpoints return raw exception text to client. ProxyEngine includes low-level connection/DNS details. Error messages echo target exception messages.
- **Violates:** Defense-in-depth principle
- **Risk:** Leaks implementation details, internal hostnames, DNS/connection behavior to frontend.
- **Status:** 🟡 FOLLOW-UP on **P2-003** — PARTIAL (some messages fixed, import/export still leaking)
- **Example:** "Import failed: Newtonsoft.Json.JsonReaderException: Unexpected character..." exposes JSON parser details

---

#### **P8-004 (MEDIUM): WebSocket State Leakage — NEW FINDING**
- **Category:** Access Control / Data Leakage
- **Location:**
  - `WebSocket/WebSocketProxy.cs:35-52, 117, 205, 288-306`
  - `Program.cs:1371-1405` (GET /api/ws/messages and /api/ws/status endpoints)
- **Issue:** WebSocketProxy is a singleton keeping full sent/received message history in memory. `/api/ws/messages` and `/api/ws/status` expose that history, target URL, and error state to any caller with no access control. In multi-session scenarios, cross-session payload leakage.
- **Violates:** Security Invariant 7 (session scoping)
- **Risk:** WebSocket payloads, session tokens, or sensitive messages exposed across connections.
- **Status:** 🆕 **NEW FINDING** — Not identified in previous phases
- **Impact:** If WebSocket proxy is used to relay authenticated sessions (e.g., trading APIs, chat apps), history exposure is a critical data breach.
- **Recommendation:** Scope WebSocket proxy state per session/user, avoid storing raw payload history, clear state on disconnect, gate endpoints behind authentication.

---

## Status of Previous Findings

### ✅ FIXED (Since Phase 5.4)

| Finding | Category | Status | Details |
|---------|----------|--------|---------|
| **P2-002** | Data Handling | ✅ FIXED | EnvironmentVariable.Value now encrypted via DPAPI for secret values; masked on read. Tested in EncryptedSecretVariableTests.cs |
| **P5-006** | Auth | ✅ FIXED | Null auth type handling now controlled; no null dereference path. Falls through to ArgumentException with proper message. |

### 🔴 OPEN (Still Unresolved)

| Finding | Severity | Category | Blocker? |
|---------|----------|----------|----------|
| **P2-001** | HIGH | Request history plaintext | Yes — Auth integration depends on this |
| **P2-003** | MEDIUM | Verbose errors (partial fix) | No — But should fix before release |
| **P5-001** | CRITICAL | AuthConfig plaintext | Yes — Blocks auth engine integration |
| **P5-002** | CRITICAL | OAuth2 token caching | Yes — Blocks auth engine integration |
| **P5-003** | HIGH | Missing HTTPS on token endpoint | Yes — Security risk for OAuth2 |
| **P5-004** | MEDIUM | API key query leak | Yes — Related to P2-001 |
| **P5-005** | MEDIUM | OAuth2 error exposure | No — Secondary to P8-003 |
| **P5-007** | MEDIUM | No HTTPS validation | Yes — Related to P5-003 |
| **P5-008** | LOW | AccessToken mutability | No — Secondary to P5-002 |
| **P5-009** | INFO | No audit logging | No — Post-release enhancement |
| **P8-001** | HIGH | Request plaintext lifecycle | Yes — Release blocker |
| **P8-002** | HIGH | Auth secrets mutable | Yes — Release blocker |
| **P8-003** | MEDIUM | Verbose exceptions | No — But should fix |
| **P8-004** | MEDIUM | WebSocket leakage | Yes — If feature ships |

---

## Dependency Audit Results

**Tool:** `dotnet list package --vulnerable --include-transitive`

**Findings:**
- ✅ **Zero vulnerable NuGet packages** — No known CVEs in dependencies
- ⚠️ **Warning NU1510:** `Microsoft.AspNetCore.DataProtection` flagged as possibly unnecessary
  - **Assessment:** False positive — DPAPI is actively used in CredentialProtector.cs for secret encryption
  - **Action:** Ignore warning; dependency is required

**Dependency Integrity:**
- ✅ All 29 NuGet packages are production-approved
- ✅ No deprecated crypto libraries (no MD5, SHA1, DES)
- ✅ No hardcoded secrets in package sources

---

## Secrets Scan Results

**Scope:** All files in `src/api/`, config, and CI/CD

**Findings:**
- ✅ **Zero hardcoded production secrets** — No API keys, tokens, passwords, cloud credentials found
- ✅ **Config files clean** — `appsettings.json` only contains localhost URLs and local SQLite paths
- ✅ **No `.env` files committed** — `.env` in `.gitignore`
- ✅ **No secrets in comments or debug code**

**Assessment:** Secrets hygiene is excellent. All secrets properly externalized or encrypted.

---

## Vulnerability Deep Scan Results

### Injection Flaws
- ✅ **SQL Injection:** No direct SQL construction; EF Core queries properly parameterized
- ✅ **XSS:** Frontend responsibility; backend returns JSON only
- ✅ **Header Injection:** No user-controlled header construction
- ✅ **Log Injection:** Logs use string interpolation with structured logging

### Authentication & Access Control
- 🔴 **Auth Secrets Plaintext (P5-001, P5-002):** CRITICAL
- 🔴 **OAuth2 HTTPS Enforcement (P5-003, P5-007):** CRITICAL/HIGH
- 🟠 **WebSocket Access Control (P8-004):** MEDIUM — No authentication on status/history endpoints
- ✅ **Session Management:** In-memory only; no session fixation risks
- ✅ **CSRF:** Not applicable (stateless API, no form-based auth)

### Data Handling
- 🔴 **Plaintext Secrets in History (P2-001, P8-001):** HIGH
- 🔴 **Plaintext AuthConfig Serialization (P8-002):** HIGH
- 🟡 **Verbose Error Messages (P8-003):** MEDIUM
- ✅ **Deserialization:** Safe JSON parsing via System.Text.Json with schema validation
- ✅ **Path Traversal:** No file serving; file operations properly scoped

### Cryptography
- 🔴 **AuthConfig Secrets Plaintext (P5-001, P5-002):** CRITICAL
- ✅ **DPAPI Implementation:** Correct usage in CredentialProtector.cs for environment variables
- ✅ **Random Number Generation:** Uses `RNGCryptoServiceProvider` for session tokens
- ✅ **No Weak Crypto:** No MD5, SHA1, DES, RC4

### Business Logic
- ✅ **No SSRF Protection:** Intentional per security-architecture.md; users responsible for target URLs
- ✅ **Request Size Limits:** 10MB enforced
- ✅ **Timeout Limits:** 1-300s configurable
- ✅ **Redirect Limits:** 20-hop limit prevents DoS
- ✅ **Rate Limiting:** Not required for local desktop tool

---

## Cross-File Data Flow Analysis

### Credential Flow: User Input → Storage → Resolution → Injection → Response

```
1. User Input (API Request Form)
   ↓
2. Request DTO: ApiRequest { Headers, Body, AuthConfig }
   ↓
3. Storage: SQLite
   - Headers stored PLAINTEXT ← [VULNERABILITY P8-001]
   - Body stored PLAINTEXT ← [VULNERABILITY P8-001]
   - AuthConfig stored PLAINTEXT ← [VULNERABILITY P8-002]
   ↓
4. Retrieval: GET /api/requests/{id}
   ↓
5. Auth Resolution: AuthHandler.ResolveAuth()
   - Reads AuthConfig from request ← [PLAINTEXT]
   - Decrypts secret variable (if referenced) ← [OK]
   - Builds Authorization header ← [OK]
   ↓
6. Proxy Execution: ProxyEngine.SendAsync()
   - Injects Authorization header
   - Sends request
   - Receives response
   - STRIPS injected headers? ← [IMPLEMENTATION PENDING]
   ↓
7. Response Return
   - RequestHistory saved ← [STORES PLAINTEXT HEADERS/BODY — VULNERABILITY P8-001]
   - RequestHistory returned to UI ← [EXPOSES PLAINTEXT SECRETS]
```

**Problem:** Secrets enter as plaintext, persist as plaintext, and exit as plaintext. Only EnvironmentVariable decryption (DPAPI) works correctly; request-level secrets bypass encryption entirely.

---

## Security Invariants Compliance

| Invariant | Status | Evidence |
|-----------|--------|----------|
| **1. No Raw Secrets in API Responses** | 🔴 FAIL | RequestHistory endpoints return plaintext headers/body |
| **2. Secret Variables Masked in UI** | ✅ PASS | EnvironmentVariable responses show `***masked***` |
| **3. Encryption at Rest** | 🔴 PARTIAL | EnvironmentVariable encrypted (✅) but AuthConfig/RequestHistory plaintext (🔴) |
| **4. Injected Headers Stripped** | ⏳ UNKNOWN | Auth integration not yet implemented in /send |
| **5. Imported Collections Sanitized** | ⏳ UNKNOWN | Sanitization logic not yet implemented |
| **6. No Plaintext in Logs** | 🔴 FAIL | RequestHistory stores raw credentials |
| **7. Environment Scoping Respected** | ✅ PASS | Variable resolution correctly scoped per environment |

---

## Release Readiness Checklist

- [ ] All secret variables encrypted at rest ← **BLOCKER: P8-002 AUTH SECRETS STILL PLAINTEXT**
- [ ] Frontend never receives raw secret values ← **BLOCKER: P8-001 HISTORY RETURNS PLAINTEXT**
- [ ] Authorization headers injected server-side only ← **PENDING: Auth integration deferred**
- [ ] Response logs don't include injected headers ← **BLOCKER: P2-001 REQUESTHISTORY PLAINTEXT**
- [ ] Imported collections sanitized ← **PENDING: Not implemented**
- [ ] All logs use redaction for credentials ← **BLOCKER: ERROR MESSAGES STILL VERBOSE**
- [ ] Request/response timeouts enforced ← ✅ PASS
- [ ] Request size limits enforced ← ✅ PASS
- [ ] HTTPS for frontend-backend ← ✅ PASS (localhost only in v1.0)
- [ ] Database file has OS-level read restrictions ← ✅ PASS
- [ ] DPAPI keys stored securely ← ✅ PASS
- [ ] WebSocket state properly scoped ← **BLOCKER: P8-004 NO SCOPING**
- [ ] No hardcoded secrets in dependencies ← ✅ PASS
- [ ] Zero CVEs in dependencies ← ✅ PASS

**Result:** 🔴 **6 blockers** prevent v1.0 release.

---

## Recommendations for v1.0 Release

### Must-Fix (CRITICAL/HIGH Blockers)

1. **P8-001 & P8-002: Request/Auth Secrets Handling**
   - Refactor request model to separate secret-bearing fields
   - Encrypt headers/body containing auth credentials before storage
   - Implement request history sanitization (redact Authorization headers, API keys in body)
   - Update import/export to redact secrets in generated code/exports
   - **Effort:** 3-4 days
   - **Blocking:** Auth integration, release approval

2. **P5-001: AuthConfig Encryption**
   - Move auth secrets to encrypted store (not JSON serialization)
   - Implement EncryptedAuthConfig wrapper using DPAPI
   - Never persist secret fields to SQLite
   - **Effort:** 2-3 days
   - **Blocking:** Auth engine integration

3. **P5-002: OAuth2 Token Caching**
   - Implement in-memory token cache with short TTL
   - Remove AccessToken from persistent AuthConfig
   - Use ConcurrentDictionary + SemaphoreSlim for thread safety
   - **Effort:** 1-2 days
   - **Blocking:** OAuth2 feature

4. **P5-003 & P5-007: HTTPS Enforcement**
   - Validate token endpoint is HTTPS-only
   - Reject HTTP token endpoints with clear error
   - Add tests for HTTPS validation
   - **Effort:** 0.5-1 day
   - **Blocking:** OAuth2 feature security

5. **P8-004: WebSocket State Scoping**
   - Remove singleton pattern; scope per connection/session
   - Stop storing raw payload history (or encrypt if needed)
   - Gate `/api/ws/messages` and `/api/ws/status` behind authentication
   - **Effort:** 1-2 days
   - **Blocking:** WebSocket feature security

### Should-Fix (MEDIUM — Code Quality)

6. **P8-003: Verbose Error Messages**
   - Return generic error codes to frontend
   - Log detailed errors server-side only
   - Update import/export error handling
   - **Effort:** 0.5-1 day
   - **Blocking:** Code review sign-off

---

## Security Test Coverage

**Existing Security Tests:**
- ✅ `AuthSecurityTests.cs` — 9 regression tests (auth credential handling) — Currently **SKIPPED pending fixes**
- ✅ `CredentialProtectorTests.cs` — 10 tests (DPAPI encryption) — All PASS
- ✅ `EncryptedSecretVariableTests.cs` — 7 tests (variable masking) — All PASS

**Missing Security Tests:**
- [ ] Request history sanitization (P8-001)
- [ ] AuthConfig encryption (P8-002)
- [ ] OAuth2 HTTPS validation (P5-003)
- [ ] WebSocket access control (P8-004)

---

## Final Assessment

### Current State
APIneer is **architecturally sound** (threat model, security invariants, DPAPI implementation correct) but **operationally vulnerable** in request/auth secret handling. The gap between architecture and implementation is significant.

### v1.0 Release Status
🔴 **NOT RELEASE-READY**

**Blockers:**
1. Request/auth secrets still plaintext in storage and history
2. AuthConfig mutable and serializable
3. OAuth2 HTTPS not enforced
4. WebSocket state not properly scoped
5. Verbose error messages

### Path to Release
1. **Fix P8-001 & P8-002** (request/auth secrets) — 4-5 days
2. **Fix P5-003 & P5-007** (OAuth2 HTTPS) — 1 day
3. **Fix P8-004** (WebSocket scoping) — 2 days
4. **Fix P8-003** (error messages) — 1 day
5. **Run security regression tests** — 0.5 days
6. **Final audit pass** — 1 day

**Estimated Total:** 9-10 days to release-ready status.

---

## Audit Metadata

- **Auditor:** Payne, Security Specialist
- **Date:** 2026-03-31
- **Scope:** Full src/api/ codebase, dependencies, config
- **Methodology:** Manual code review + data flow analysis + invariant verification
- **Tools:** dotnet list package, ripgrep, visual inspection
- **Previous Audits:** Phase 2.9 (P2-001..P2-005), Phase 4.4 (CredentialProtector), Phase 5.4 (AuthEngine — P5-001...P5-009)
- **Status:** 🔴 CRITICAL FINDINGS UNRESOLVED — DO NOT RELEASE TO PRODUCTION

---

**Next Review:** After implementing fixes in Section "Recommendations for v1.0 Release". Final audit sign-off required before release.
