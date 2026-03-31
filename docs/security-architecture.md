# APIneer Security Architecture

**Status:** Phase 1.4 — Security Foundation  
**Last Updated:** 2026-03-30  
**Owner:** Payne, Security Specialist

---

## 1. Threat Model for a Local API Tool

### Trust Boundaries

APIneer operates in a three-tier trust model:

1. **Frontend (Browser)** — Nuxt UI running in the user's browser
2. **Backend API** — .NET 10 service handling credential storage and proxy execution
3. **Target APIs** — External services the user wants to test (untrusted, potentially malicious)

```
[Frontend] <--HTTPS--> [Backend API] <--HTTP/S--> [Target APIs]
  (Trusted)              (Trusted)                 (Untrusted)
```

The backend is the **security boundary enforcer**. All secrets must be protected in transit and at rest, and the backend is responsible for safe credential injection and request execution.

### In Scope

- **Credential Storage:** API keys, OAuth tokens, Basic Auth passwords stored locally
- **Credential Injection:** Safe resolution and injection of secrets at request execution time
- **Proxy Security:** Controlled execution of HTTP requests to arbitrary targets
- **Log Sanitization:** Preventing credential exposure in logs and UI responses
- **Malicious Collections:** Imported collections may contain injected scripts or malicious payloads

### Out of Scope

- **Network Security:** We assume the user's machine and network are under their control
- **OS-Level Access Control:** We rely on the user's OS file permissions for protection
- **Host Machine Compromises:** If the user's machine is already compromised, all bets are off
- **Browser Security:** We defer to the browser's sandbox and same-origin policy

### Attack Vectors

1. **Malicious Collection Import**
   - Attacker-controlled collection JSON contains scripts or XSS payloads
   - Mitigation: Sanitize imported collections, reject inline scripts

2. **Credential Exfiltration**
   - Attacker gains read access to credential storage (SQLite + encryption keys)
   - Mitigation: Encrypt secrets at rest, store keys in OS-protected directories

3. **SSRF via Proxy**
   - User is tricked into making requests to internal addresses (localhost, 127.0.0.1, private IPs)
   - Mitigation: No SSRF protection by design (the tool IS designed for localhost API testing)
   - Users are responsible for understanding the requests they send

4. **Unencrypted Credentials in Response**
   - Backend accidentally returns secrets in API responses
   - Mitigation: Never include raw credential values in frontend-facing responses

5. **Proxy Log Exposure**
   - Request/response logs contain full credential values
   - Mitigation: Sanitize all logs, redact credentials before storage or display

6. **Man-in-the-Middle (MitM)**
   - Attacker intercepts traffic between frontend and backend
   - Mitigation: Use HTTPS for all frontend-backend communication; user controls HTTPS on target APIs

---

## 2. Credential Storage Architecture

### Encryption Model

APIneer uses **layered encryption** to protect secrets:

```
User Input (Secret)
    ↓
Stored in-memory only (during request handling)
    ↓
Encrypted using .NET DPAPI (Data Protection API)
    ↓
Ciphertext stored in SQLite
    ↓
Decryption only at request execution time
    ↓
Raw secret used to inject headers
    ↓
Memory cleared after injection
```

### Implementation Details

**Encryption at Rest:**
- **Tool:** .NET Data Protection API (DPAPI)
- **Scope:** All secret variable values before storage
- **Keys:** Generated and stored in `%APPDATA%\APIneer\keys\` (Windows) or `~/.config/apineer/keys/` (Unix)
- **Key Rotation:** Not implemented in Phase 1.4; keys are user-specific and machine-bound

**Storage Format:**
- Secret values are stored encrypted in SQLite schema as `BLOB` type
- Metadata (name, environment) stored as plain text
- Example schema:
  ```
  CREATE TABLE SecretVariables (
    Id INTEGER PRIMARY KEY,
    EnvironmentId INTEGER,
    Name TEXT NOT NULL,           -- plaintext (e.g., "api_key")
    EncryptedValue BLOB NOT NULL, -- encrypted value
    CreatedAt TIMESTAMP,
    UpdatedAt TIMESTAMP
  );
  ```

**Decryption Constraints:**
- Decryption ONLY happens on the backend, never on the frontend
- Decryption happens at request execution time, not during UI rendering
- Decrypted value is never stored back to disk after use
- Decrypted value is held in-memory only as long as needed for request injection

**Frontend Handling:**
- Frontend NEVER receives raw secret values
- Secrets are displayed as `***masked***` in the UI
- Frontend can read metadata (variable name, environment) but not values
- Copy/paste operations on secret fields are blocked

### Database Security

- SQLite file stored in `%APPDATA%\APIneer\` (Windows) or `~/.local/share/apineer/` (Unix)
- File-level permissions: readable only by the current user (enforced by OS)
- No database password (relying on OS file permissions + DPAPI encryption)
- Future consideration: Add master password for additional security layer

---

## 3. Auth Header Injection Pattern

### Request Execution Flow

```
1. User clicks "Send Request"
   ↓
2. Backend receives request with reference to auth config (not the credentials themselves)
   ↓
3. Backend resolves auth config (reads from collection/environment/request-level)
   ↓
4. Backend decrypts secrets for this auth config
   ↓
5. Backend injects Authorization headers (and other auth headers)
   ↓
6. Backend sends HTTP request to target API
   ↓
7. Backend receives response from target API
   ↓
8. Backend strips injected auth headers from response object
   ↓
9. Backend returns response to frontend (WITHOUT injected headers)
   ↓
10. Frontend displays response body/headers to user
```

### Auth Configuration Storage

**Per-Request Level:**
- Each request can have optional auth config
- Auth config references a secret variable or static value
- Auth config types: Basic Auth, Bearer Token, API Key (header or query param), OAuth 2.0

**Environment Inheritance:**
- Collections define default auth configs
- Environments can override auth
- Requests can override environment auth
- Resolution order: Request > Environment > Collection > None

**Example Structure:**
```json
{
  "request": {
    "name": "Get User",
    "auth": {
      "type": "bearer",
      "secretVariableId": "{{ api_key }}"
    }
  },
  "environment": {
    "variables": [
      {
        "id": "api_key",
        "name": "api_key",
        "value": "[ENCRYPTED_BLOB]"
      }
    ]
  }
}
```

### Header Injection Rules

1. **Resolve Secrets:** Before sending, backend resolves all secret variable references
2. **Inject Headers:** Backend adds Authorization headers based on auth type:
   - Basic Auth: `Authorization: Basic base64(username:password)`
   - Bearer: `Authorization: Bearer <token>`
   - API Key (header): Custom header with secret value
   - API Key (query): Append to query string (not recommended)

3. **Send Request:** Backend sends the request with injected headers

4. **Strip Injected Headers:** Before returning response to frontend, remove:
   - Any Authorization headers added by the backend
   - Any custom auth headers added during injection
   - Cookies set by the backend (if applicable)

5. **Log Sanitization:** Never log the raw values of injected headers, only `[REDACTED]`

### Response Object Guarantee

**Contract:** The response object returned to the frontend contains:
- Response status code
- Response headers (EXCLUDING injected auth headers)
- Response body
- Timing information
- Request metadata (URL, method, params) — NO credentials

**What is NOT included:**
- Raw secret variable values
- Injected Authorization headers
- Plaintext representations of secrets

---

## 4. Proxy Security

### Proxy Engine Design

The proxy engine is **intentionally permissive** — it sends HTTP requests to any URL the user specifies. This is by design: APIneer is a tool for testing APIs, and users may need to test localhost services, internal APIs, or arbitrary endpoints.

```
Request from User
    ↓
Validate request structure (not URL contents)
    ↓
Resolve auth credentials
    ↓
Inject auth headers
    ↓
Build full HTTP request
    ↓
Execute with configured timeout
    ↓
Capture response
    ↓
Sanitize response
    ↓
Return to frontend
```

### SSRF Considerations

**No SSRF Protection Against:**
- Localhost (127.0.0.1, ::1, localhost)
- Private IPs (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16)
- Link-local addresses (169.254.0.0/16)

**Rationale:** APIneer's primary use case IS testing localhost and internal APIs. Users explicitly construct requests to these targets; they are not blind redirects or sneaky auto-discovery attacks.

**Security Responsibility:** The user is responsible for understanding:
- What URLs they are constructing
- What credentials they are injecting
- What requests they are sending

If a user is tricked into sending requests to unintended targets, that's a social engineering attack, not an SSRF vulnerability in the tool.

### Request Size Limits

- Max request body size: **10 MB**
- Max request headers size: **64 KB**
- Rationale: Prevent memory exhaustion or slowdown from maliciously large payloads

### Timeout Configuration

- Default timeout: **30 seconds**
- Configurable per-request or globally: Yes
- Min timeout: 1 second
- Max timeout: 5 minutes
- Rationale: Prevent hanging requests from blocking the UI; allow long-running operations

### Log Sanitization

**Every log entry must follow:**
1. Never include raw secret values
2. Never include Authorization header contents
3. Never include Bearer tokens, API keys, or passwords
4. Use `[REDACTED]` placeholder for all sensitive data
5. Log only: method, URL, status code, duration, error messages (with credentials redacted)

**Example Log Entry:**
```
[2026-03-30 14:23:45] POST http://api.example.com/v1/users → Status 200 (1234ms)
  Auth: Bearer [REDACTED]
  Headers: Content-Type: application/json (2 more headers)
```

**Anti-Pattern Log Entry (DO NOT DO THIS):**
```
[2026-03-30 14:23:45] POST http://api.example.com/v1/users
  Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
  Body: {"api_key": "sk-12345678..."}
  Status 200 (1234ms)
```

### Response Handling

Responses from target APIs are treated as untrusted:
- No code injection (responses are not executed)
- HTML responses are escaped before display
- JSON responses are parsed safely
- Binary responses are offered as download, not displayed

---

## 5. Security Invariants (Test Stubs)

These are hard constraints that **must always be true**. Each invariant is testable and will become part of the security test suite.

### Invariant 1: No Raw Secrets in API Responses

**Statement:** Secret variable values must NEVER appear in any API response to the frontend.

**Test Stub:**
```
Scenario: User creates a request with a secret variable
  Given: A secret variable with value "super-secret-api-key"
  When: Backend executes the request
  Then: Response body does NOT contain "super-secret-api-key"
  And: Response headers do NOT contain "super-secret-api-key"
  And: Response metadata does NOT contain "super-secret-api-key"
```

### Invariant 2: Secret Variables Displayed as Masked

**Statement:** When frontend renders a secret variable reference, it must show `***masked***` or similar, never the actual value.

**Test Stub:**
```
Scenario: User views a request with secret variable
  Given: A request references secret variable "api_key"
  When: Frontend renders the request details
  Then: Secret value is not visible in the UI
  And: Variable name IS visible (e.g., "{{ api_key }}")
  And: Copy/paste of the value is blocked or returns [REDACTED]
```

### Invariant 3: Credentials Encrypted at Rest

**Statement:** All secret variable values stored in SQLite must be encrypted using DPAPI.

**Test Stub:**
```
Scenario: Secret variable is saved to database
  Given: A secret variable with plaintext value
  When: Backend stores the variable
  Then: SQLite database contains only ENCRYPTED bytes
  And: Plaintext value does NOT appear in database dump
  And: Only the backend (with DPAPI keys) can decrypt
```

### Invariant 4: Injected Headers Removed from Response Log

**Statement:** Authorization headers injected by the backend must NOT be included in the response log returned to the frontend.

**Test Stub:**
```
Scenario: Backend injects auth header and receives response
  Given: Backend injects "Authorization: Bearer [token]" header
  When: Backend receives response from target API
  Then: Response log returned to frontend does NOT include Authorization header
  And: Response body is included
  And: Other response headers are included
```

### Invariant 5: Imported Collections are Sanitized

**Statement:** When a user imports a collection JSON, any scripts, inline code, or injection payloads must be sanitized or rejected.

**Test Stub:**
```
Scenario: User imports a collection with malicious payload
  Given: Imported collection contains "<script>alert('xss')</script>"
  When: Backend processes the import
  Then: Script tags are removed or escaped
  And: No inline JavaScript is executed
  And: Collection is safely imported with sanitized content
```

### Invariant 6: No Plaintext Secrets in Request Logs

**Statement:** Request logs stored on disk or displayed in UI must NOT contain plaintext secrets.

**Test Stub:**
```
Scenario: User sends request with Basic Auth
  Given: Basic Auth username: "admin", password: "p@ssw0rd"
  When: Backend logs the request
  Then: Log file does NOT contain "p@ssw0rd"
  And: Log shows "Authorization: Basic [REDACTED]"
```

### Invariant 7: Environment Variable Inheritance Respects Auth Scope

**Statement:** Auth credentials from environments are only used within the scope of that environment. Switching environments switches the active secrets.

**Test Stub:**
```
Scenario: User switches between environments with different credentials
  Given: Environment "Production" has api_key "prod-secret"
  And: Environment "Staging" has api_key "staging-secret"
  When: User switches to "Staging" and sends request
  Then: Request uses "staging-secret", NOT "prod-secret"
  And: Previous environment's secrets are not re-used
```

---

## 6. Implementation Roadmap

### Phase 1.4 (Current)
- [x] Define threat model and trust boundaries
- [x] Specify encryption architecture (DPAPI)
- [x] Design auth header injection pattern
- [x] Define proxy security constraints
- [x] Write security invariants

### Phase 1.5 (Upcoming)
- [ ] Implement DPAPI encryption for secret variables
- [ ] Add request/response sanitization
- [ ] Implement log redaction
- [ ] Write security tests (based on invariants)

### Phase 1.6+ (Future)
- [ ] Add request audit logging
- [ ] Implement master password (optional user protection)
- [ ] Add export security checks (don't export secrets)
- [ ] Implement request signing (HmacSha256, etc.)
- [ ] Add role-based collection sharing (future feature)

---

## 7. Security Review Checklist

Before deploying APIneer to users, verify:

- [ ] All secret variables are encrypted at rest
- [ ] Frontend never receives raw secret values
- [ ] Authorization headers are injected server-side only
- [ ] Response logs don't include injected headers
- [ ] Imported collections are sanitized
- [ ] All logs use redaction for credentials
- [ ] Request/response timeouts are enforced
- [ ] Request size limits are enforced
- [ ] HTTPS is used for frontend-backend communication
- [ ] Database file has OS-level read restrictions
- [ ] DPAPI keys are stored securely (OS-protected directory)

---

## 8. Glossary

- **DPAPI:** Data Protection API (Windows) or equivalent (macOS/Linux) — OS-level encryption service
- **Credential:** Any secret (API key, token, password) used to authenticate to target APIs
- **Auth Header Injection:** Process of adding Authorization headers to a request before sending
- **Sanitization:** Removing or escaping potentially harmful content (scripts, secrets, etc.)
- **Invariant:** A constraint that must always be true; verified by tests
- **SSRF:** Server-Side Request Forgery — attack where a service makes unintended requests to internal/private targets
- **TOCTOU:** Time-of-Check-Time-of-Use — race condition vulnerability
- **Redaction:** Replacing sensitive data with `[REDACTED]` in logs or UI

---

**Document Version:** 1.0  
**Approval Status:** Pending Review  
**Next Review Date:** After Phase 1.5 implementation
