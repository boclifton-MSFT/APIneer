# MCP OAuth Implementation Plan

## Overview

APIneer's MCP page can configure GitHub Remote MCP servers (`https://api.githubcopilot.com/mcp/`) but **cannot connect to them**. This plan documents everything needed to add OAuth support so the GitHub Remote MCP Server works end-to-end.

### Current State
- The MCP page lets you add a Streamable HTTP server with a URL and custom headers
- Clicking "Connect" on the GitHub Remote MCP returns **502 Bad Gateway**
- Root cause: two bugs preventing connection

### Root Causes

**Bug 1: Custom headers are discarded**
- The connect endpoint in `Program.cs` (~line 1656-1737) parses headers from the request body AND from saved `McpServerConfig`, but **never passes them** into `McpConnectionManager.CreateTransport()`
- `HttpMcpTransport` already accepts and uses custom headers — they're just not wired through
- This means even a valid PAT in the `Authorization` header would be ignored

**Bug 2: No OAuth flow exists**
- GitHub's Remote MCP Server at `https://api.githubcopilot.com/mcp/` requires authentication
- VS Code handles this automatically with its built-in GitHub OAuth
- APIneer has **zero OAuth device code support** — only `client_credentials` for generic API requests
- No device code flow, no authorization code flow, no PKCE, no token refresh

---

## Solution Architecture

### OAuth Device Code Flow
The [OAuth 2.0 Device Authorization Grant (RFC 8628)](https://tools.ietf.org/html/rfc8628) is the standard for locally-running apps that can't open a browser redirect. This is how GitHub CLI authenticates.

**Flow:**
```
1. APIneer Backend                        GitHub
   POST /login/device/code    ──────►    Returns: device_code, user_code, verification_uri
   
2. APIneer Frontend shows:
   "Enter code WDJB-MJHT at https://github.com/login/device"
   [Open GitHub] button
   
3. User goes to github.com/login/device, enters code, authorizes

4. APIneer Backend (polling)              GitHub  
   POST /login/oauth/access_token ──────► Returns: authorization_pending (repeat)
   POST /login/oauth/access_token ──────► Returns: access_token ✅
   
5. Token stored encrypted in McpServerConfig
   
6. On MCP Connect: Authorization: Bearer {token} injected automatically
```

### GitHub API Endpoints Used
| Step | Method | URL | Purpose |
|------|--------|-----|---------|
| Start | POST | `https://github.com/login/device/code` | Get device + user codes |
| Poll | POST | `https://github.com/login/oauth/access_token` | Exchange device code for token |

### GitHub Device Code Response
```json
{
  "device_code": "3584d83530557fdd1f46af8289938c8ef79f9dc5",
  "user_code": "WDJB-MJHT",
  "verification_uri": "https://github.com/login/device",
  "expires_in": 900,
  "interval": 5
}
```

### GitHub Token Exchange Request
```
POST https://github.com/login/oauth/access_token
Accept: application/json

client_id={client_id}
device_code={device_code}
grant_type=urn:ietf:params:oauth:grant-type:device_code
```

### Error Codes During Polling
| Error | Meaning | Action |
|-------|---------|--------|
| `authorization_pending` | User hasn't authorized yet | Keep polling at `interval` |
| `slow_down` | Polling too fast | Add 5s to interval |
| `expired_token` | 15-minute window expired | Show error, allow retry |
| `access_denied` | User denied authorization | Show error |

---

## Implementation Phases

### Phase 1: Wire Headers Through (Quick Fix — PAT Auth Works)

**What:** Pass custom headers from the connect request into the transport. This is a ~5-line fix that immediately enables PAT-based auth as a fallback.

**File:** `src/api/APIneer.Api/Program.cs`

**Current code (simplified):**
```csharp
// Line ~1716 — headers are parsed but NOT passed
var transport = mgr.CreateTransport(transportType, command, args, envVars, url);
// Missing: headers parameter ↑
```

**Fix:**
```csharp
var transport = mgr.CreateTransport(transportType, command, args, envVars, url, headers);
```

**Also verify:** `McpConnectionManager.CreateTransport()` passes headers to `HttpMcpTransport` constructor.

**Test:** After this fix, manually entering `Authorization: Bearer ghp_xxxxx` in the custom headers field and clicking Connect should work for any Streamable HTTP MCP server.

**Owner:** Marcus (backend)

---

### Phase 2: Backend OAuth Device Code Service

**What:** New service implementing the GitHub device code flow, plus API endpoints the frontend can call.

#### New File: `src/api/APIneer.Api/Auth/GitHubDeviceCodeAuth.cs`

```csharp
public class GitHubDeviceCodeAuth(IHttpClientFactory httpClientFactory, ILogger<GitHubDeviceCodeAuth> logger)
{
    // Start the device code flow
    public async Task<DeviceCodeResponse> StartDeviceFlowAsync(string clientId, string? scopes = null);
    
    // Poll for token completion (call this in a loop with the interval)
    public async Task<TokenPollResult> PollForTokenAsync(string clientId, string deviceCode);
}

public record DeviceCodeResponse(
    string DeviceCode, 
    string UserCode, 
    string VerificationUri, 
    int ExpiresIn, 
    int Interval
);

public record TokenPollResult(
    string Status,           // "pending", "complete", "expired", "denied", "slow_down"
    string? AccessToken,
    string? TokenType,
    string? Scope
);
```

**Implementation details:**
- Use `IHttpClientFactory` for HTTP calls (registered in DI)
- POST to `https://github.com/login/device/code` with `client_id` and `scope`
- POST to `https://github.com/login/oauth/access_token` with `client_id`, `device_code`, and `grant_type`
- Always send `Accept: application/json` header
- Parse `error` field in poll response to determine status

#### New API Endpoints in `Program.cs`

**`POST /api/mcp/oauth/start`**
```
Request:  { "serverId": "guid", "clientId": "string", "scopes": "repo" }
Response: { "flowId": "guid", "userCode": "WDJB-MJHT", "verificationUri": "https://github.com/login/device", "expiresIn": 900 }
```

- Creates an in-memory flow state (tracked in a `ConcurrentDictionary<Guid, OAuthFlowState>`)
- Starts background polling task that calls `PollForTokenAsync` at the specified interval
- Returns immediately with the user code for display

**`GET /api/mcp/oauth/status/{flowId}`**
```
Response: { "status": "pending" | "complete" | "expired" | "denied", "error": "string?" }
```

- Frontend polls this every 5 seconds
- When complete, the token is already stored in `McpServerConfig` (see Phase 3)
- Returns status only — never exposes the raw token to the frontend

**`DELETE /api/mcp/oauth/token/{serverId}`**
```
Response: 204 No Content
```

- Clears stored OAuth token for the specified server
- Used for "Logout" / "Revoke" functionality

**In-memory flow state:**
```csharp
record OAuthFlowState(
    Guid ServerId,
    string ClientId,
    string DeviceCode,
    int Interval,
    DateTime ExpiresAt,
    string Status,          // "pending", "complete", "expired", "denied"
    string? AccessToken,
    CancellationTokenSource Cts
);
```

- Auto-cleanup: expired flows removed after `expiresIn` + 60s buffer
- The background polling task updates the flow state and, on success, writes the token to the database

**Owner:** Marcus (backend)

---

### Phase 3: OAuth Token Storage & Auto-Injection

**What:** Persist OAuth tokens securely and auto-inject them when connecting.

#### Model Changes: `src/api/APIneer.Api/Models/McpServerConfig.cs`

Add fields:
```csharp
public string? OAuthAccessToken { get; set; }      // Encrypted with CredentialProtector
public DateTime? OAuthTokenExpiresAt { get; set; }  // Nullable — device flow tokens may not expire
public string? OAuthScopes { get; set; }            // e.g., "repo"
public string? OAuthProvider { get; set; }          // e.g., "github" — future-proofs for other providers
```

#### EF Migration
```bash
cd src/api/APIneer.Api
dotnet ef migrations add AddOAuthTokenFields
```

#### Token Encryption
Use existing `CredentialProtector` service:
```csharp
// On token storage (after successful OAuth flow):
config.OAuthAccessToken = credentialProtector.Encrypt(accessToken);

// On token retrieval (during connect):
var token = credentialProtector.Decrypt(config.OAuthAccessToken);
```

#### Auto-Injection in Connect Endpoint
In the `/api/mcp/connect` handler, after loading the `McpServerConfig`:
```csharp
// If server has a stored OAuth token, inject it
if (!string.IsNullOrEmpty(serverConfig.OAuthAccessToken))
{
    var token = credentialProtector.Decrypt(serverConfig.OAuthAccessToken);
    headers ??= new Dictionary<string, string>();
    headers["Authorization"] = $"Bearer {token}";
}
```

This means after initial OAuth setup, the user just clicks "Connect" — no manual token entry needed.

**Owner:** Marcus (backend)

---

### Phase 4: Frontend OAuth UI

**What:** Add OAuth login UX to the MCP connection form.

#### API Functions: `src/ui/app/composables/useApi.ts`

Add:
```typescript
async function startMcpOAuth(serverId: string, clientId: string, scopes?: string): Promise<{
  flowId: string
  userCode: string
  verificationUri: string
  expiresIn: number
}>

async function pollMcpOAuth(flowId: string): Promise<{
  status: 'pending' | 'complete' | 'expired' | 'denied'
  error?: string
}>

async function revokeMcpOAuth(serverId: string): Promise<void>
```

#### Connection Form: `src/ui/app/components/mcp/McpConnectionForm.vue`

For Streamable HTTP servers, add an **Auth Method** selector below the URL field:

```
┌─────────────────────────────────────────┐
│ Auth Method                             │
│ ○ None  ○ Bearer Token  ● GitHub OAuth  │
│                                         │
│ ┌─────────────────────────────────────┐ │
│ │  🔑 Login with GitHub               │ │
│ │                                     │ │
│ │  Client ID: [_________________]     │ │
│ │  Scopes:    [repo_____________]     │ │
│ │                                     │ │
│ │  [ Login with GitHub ]              │ │
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```

After clicking "Login with GitHub":
```
┌─────────────────────────────────────────┐
│ Auth Method                             │
│ ○ None  ○ Bearer Token  ● GitHub OAuth  │
│                                         │
│ ┌─────────────────────────────────────┐ │
│ │  Enter code at GitHub               │ │
│ │                                     │ │
│ │  Your code:  WDJB-MJHT             │ │
│ │                                     │ │
│ │  [ Open github.com/login/device ]   │ │
│ │                                     │ │
│ │  ⏳ Waiting for authorization...    │ │
│ │  ░░░░░░░░░░░░░░░░░ (expires: 14m)  │ │
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```

After successful auth:
```
┌─────────────────────────────────────────┐
│ Auth Method                             │
│ ○ None  ○ Bearer Token  ● GitHub OAuth  │
│                                         │
│ ┌─────────────────────────────────────┐ │
│ │  ✅ Authenticated with GitHub       │ │
│ │  Scopes: repo                       │ │
│ │                                     │ │
│ │  [ Logout ]                         │ │
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```

#### Orchestrator: `src/ui/app/pages/mcp.vue`

- When auth method is "GitHub OAuth" and user clicks Connect:
  - If no stored token: prompt to login first
  - If token exists: connect normally (backend auto-injects token)
- Poll OAuth status every 5 seconds during device code flow
- Clean up polling on unmount or cancel

**Owner:** Kratos (frontend)

---

### Phase 5: Tests + Optimization Review

#### Unit Tests (Freeman)

**Backend tests:**
- `tests/APIneer.Api.Tests/Auth/GitHubDeviceCodeAuthTests.cs`
  - Mock HTTP responses for device code request
  - Mock polling responses (pending → success, pending → expired, denied, slow_down)
  - Token encryption/decryption round-trip
  - Connect endpoint with stored OAuth token auto-injection

**Frontend tests:**
- `src/ui/tests/components/McpOAuth.test.ts`
  - OAuth flow states (idle, pending, complete, error, expired)
  - User code display
  - Polling behavior
  - Auth method selector

#### Optimization Review
- **Arthur** reviews backend OAuth code for:
  - Token handling security (no token leakage in logs/responses)
  - Error handling completeness
  - Memory management for in-flight OAuth flows
  - Thread safety of ConcurrentDictionary usage
- **Dutch** reviews frontend OAuth code for:
  - Reactive state management
  - Proper cleanup on unmount
  - Error UX
  - Type safety

**Owners:** Freeman (tests), Arthur (C# review), Dutch (Vue review)

---

## Dependency Graph

```
wire-headers ──► oauth-device-service ──► oauth-token-storage ──┐
                        │                                        │
                        └──► oauth-frontend-ui ──────────────────┤
                                                                 │
                                                     oauth-tests ──► oauth-review
```

- Phase 1 (`wire-headers`) has no dependencies — can start immediately
- Phases 2 + 3 are sequential (service before storage)
- Phase 4 (frontend) can start in parallel with Phase 3 once Phase 2 endpoints exist
- Phase 5 waits for both backend and frontend to complete

---

## Key Decisions

### 1. GitHub OAuth App Client ID
APIneer needs a registered GitHub OAuth App with device flow enabled.

**Options:**
- **A) Ship a built-in client_id** — Best UX, but client_id would be in the source code (this is acceptable for OAuth apps — the client_secret is what must stay private, and device flow doesn't use a client_secret)
- **B) User-configurable only** — User registers their own OAuth App at github.com/settings/developers
- **C) Both** — Built-in default + user override in Settings

**Recommendation:** Option C — configurable with optional default. Store the client_id in Settings/config. For initial development, use a user-configurable client_id.

### 2. Token Scope
GitHub MCP Server needs at minimum `repo` scope for full functionality. Default to `repo` but make configurable in the OAuth form.

### 3. Token Lifetime
GitHub OAuth tokens from device flow don't expire by default (unless the OAuth App is configured with expiring tokens). Handle both cases:
- If `expires_in` is present: store expiry, prompt re-auth when expired
- If no expiry: token persists until explicitly revoked

### 4. Settings Page
The app has a "Settings" button in the header but it doesn't open anything yet. This plan doesn't require Settings, but it would be a natural place for the default GitHub OAuth client_id. Can be added as a follow-up.

---

## Files Summary

### New Files
| File | Purpose |
|------|---------|
| `src/api/APIneer.Api/Auth/GitHubDeviceCodeAuth.cs` | Device code flow service |
| `src/api/APIneer.Api/Data/Migrations/*_AddOAuthTokenFields.cs` | DB migration (auto-generated) |
| `tests/APIneer.Api.Tests/Auth/GitHubDeviceCodeAuthTests.cs` | Backend OAuth tests |
| `src/ui/tests/components/McpOAuth.test.ts` | Frontend OAuth tests |

### Modified Files
| File | Changes |
|------|---------|
| `src/api/APIneer.Api/Program.cs` | Wire headers + 3 new OAuth endpoints |
| `src/api/APIneer.Api/Models/McpServerConfig.cs` | Add OAuth token fields |
| `src/ui/app/composables/useApi.ts` | Add OAuth API functions |
| `src/ui/app/components/mcp/McpConnectionForm.vue` | Auth method selector + OAuth login UX |
| `src/ui/app/pages/mcp.vue` | OAuth flow orchestration |

### Existing Infrastructure to Leverage
| Component | Location | Used For |
|-----------|----------|----------|
| `HttpMcpTransport` | `Mcp/HttpMcpTransport.cs` | Already accepts headers — just needs wiring |
| `CredentialProtector` | `Services/CredentialProtector.cs` | Encrypting OAuth tokens at rest |
| `McpConnectionManager` | `Mcp/McpConnectionManager.cs` | Connection lifecycle management |
| `IHttpClientFactory` | Registered in DI | Making OAuth HTTP calls |

---

## Agent Assignments

| Agent | Role | Phases | Model |
|-------|------|--------|-------|
| Marcus | Backend Dev | 1, 2, 3 | claude-sonnet-4.6 |
| Kratos | Frontend Dev | 4 | claude-sonnet-4.6 |
| Freeman | Tester | 5 (tests) | claude-sonnet-4.6 |
| Arthur | C# Optimization Expert | 5 (review) | claude-sonnet-4.6 |
| Dutch | TS/Vue Optimization Expert | 5 (review) | claude-sonnet-4.6 |

---

## How to Resume

When picking this back up:
1. Read this file for the full plan
2. Start with Phase 1 (`wire-headers`) — it's a quick win
3. The API and UI dev servers need to be running:
   - API: `cd src/api/APIneer.Api && dotnet run --urls "http://localhost:5000"`
   - UI: `cd src/ui && npx nuxi dev --port 3000`
4. After Phase 1, test PAT auth manually by entering a real GitHub PAT in the custom headers
5. Then proceed to Phases 2-5 for full OAuth support
