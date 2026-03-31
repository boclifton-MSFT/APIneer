# Architecture

APIneer is built on a **two-tier distributed architecture** optimized for local API development and testing.

---

## System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                      User's Machine                             │
│                                                                 │
│  ┌──────────────────────────────┐   ┌──────────────────────┐  │
│  │    Frontend (Nuxt UI)        │   │  SQLite Database     │  │
│  │    localhost:3000            │   │  (Local Storage)     │  │
│  │                              │   │                      │  │
│  │  ┌──────────────────────┐    │   │  ┌────────────────┐  │  │
│  │  │ Request Builder      │    │   │  │ Collections    │  │  │
│  │  │ • URL, Method        │    │   │  │ Requests       │  │  │
│  │  │ • Headers, Body      │    │   │  │ Environments   │  │  │
│  │  │ • Auth Config        │    │   │  │ Variables      │  │  │
│  │  └──────────────────────┘    │   │  │ History        │  │  │
│  │                              │   │  │ Assertions     │  │  │
│  │  ┌──────────────────────┐    │   │  └────────────────┘  │  │
│  │  │ Response Viewer      │    │   │                      │  │
│  │  │ • Status, Headers    │    │   └──────────────────────┘  │
│  │  │ • Body, Timing       │    │                              │
│  │  │ • Assertions         │    │                              │
│  │  └──────────────────────┘    │                              │
│  │                              │                              │
│  └──────────────┬───────────────┘                              │
│                 │ HTTP/REST                                    │
│  ┌──────────────▼───────────────────────────────────────────┐  │
│  │      Backend API (.NET 10 Minimal API)                    │  │
│  │      localhost:5000                                       │  │
│  │                                                            │  │
│  │  ┌────────────────────────────────────────────────────┐  │  │
│  │  │ API Endpoints                                      │  │  │
│  │  │ • /api/requests, /api/collections, /api/environ...│  │  │
│  │  │ • /api/requests/{id}/send (Execute)              │  │  │
│  │  │ • /api/requests/{id}/assertions (Test)           │  │  │
│  │  │ • /api/import/*, /api/collections/*/export       │  │  │
│  │  │ • /api/ws/* (WebSocket)                          │  │  │
│  │  └────────────────────────────────────────────────────┘  │  │
│  │                                                            │  │
│  │  ┌──────────────────┐  ┌────────────────────────────┐    │  │
│  │  │ Proxy Engine     │  │ Service Layer              │    │  │
│  │  │ • HTTP Client    │  │ • Auth Injection           │    │  │
│  │  │ • Timeouts       │  │ • Variable Resolution      │    │  │
│  │  │ • Redirects      │  │ • Log Sanitization         │    │  │
│  │  │ • Size Limits    │  │ • Assertion Evaluation     │    │  │
│  │  │ • Error Handling │  │ • Import/Export Logic      │    │  │
│  │  └────────┬─────────┘  └────────────────────────────┘    │  │
│  │           │                                               │  │
│  │  ┌────────▼──────────────────────────────────────────┐  │  │
│  │  │ Data Access Layer (Entity Framework Core)         │  │  │
│  │  │ • AppDbContext                                    │  │  │
│  │  │ • DbSets for all entities                         │  │  │
│  │  │ • Migrations                                      │  │  │
│  │  └──────────────────────────────────────────────────┘  │  │
│  │                                                            │  │
│  └────────────────────────────────────────────────────────────┘  │
│                         │                                        │
│                         │ HTTPS                                  │
└─────────────────────────┼────────────────────────────────────────┘
                          │
                          │ HTTP/HTTPS
                          │ (Proxied)
                          │
┌─────────────────────────▼──────────────────────────────────────┐
│                    Target APIs                                  │
│         (Any HTTP/HTTPS endpoint being tested)                 │
└────────────────────────────────────────────────────────────────┘
```

---

## Component Overview

### Frontend (Nuxt 4.4 + Nuxt UI v4)

The interactive web UI runs on `localhost:3000`.

**Key Components:**

- **RequestBuilder** — Form-based interface for constructing HTTP requests
  - URL input with environment variable substitution (`{{VAR_NAME}}`)
  - Method selector (GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS)
  - Headers editor (key-value pairs, toggle active/inactive)
  - Body editor (JSON, form data, raw text)
  - Auth config selector (reference environment credentials)
  - Timeout configuration

- **ResponseViewer** — Display execution results
  - Status code and status text
  - Response headers (formatted, copyable)
  - Response body (formatted JSON, syntax highlighting)
  - Timing and size metrics
  - Assertion results (pass/fail per test)

- **CollectionNavigator** — Sidebar tree view
  - Collections (top-level grouping)
  - Folders (nested hierarchy)
  - Requests (leaf nodes)
  - Quick actions (create, delete, duplicate, move)
  - Drag-to-reorder support

- **EnvironmentManager** — Environment and variable management
  - List environments
  - Switch active environment
  - Create/edit/delete variables
  - Secret masking for sensitive values

- **HistoryPanel** — View past requests and responses
  - Chronological list
  - Filter by collection, request, status
  - Restore from history (copy to new request)
  - Redacted secrets for security

**State Management (Pinia):**

- `request.ts` — Current request being built
- `collection.ts` — Collections, folders, and request hierarchy
- `environment.ts` — Environments and active environment selection
- `history.ts` — Request/response history

**API Client:**

- Proxied to `http://localhost:5000/api/**` via Nuxt dev server configuration
- Handles all CRUD operations on backend

---

### Backend (.NET 10 Minimal API)

The REST API runs on `localhost:5000`.

**Architecture:**

```
Program.cs (Route Definitions)
    ↓
AppDbContext (EF Core)
    ↓
Models (Request, Collection, Environment, etc.)
    ↓
Services (ProxyEngine, AuthService, etc.)
    ↓
SQLite Database
```

**Key Components:**

- **Program.cs** — Minimal API route definitions
  - Endpoint mapping for all CRUD operations
  - Dependency injection setup
  - Middleware configuration (CORS, logging, error handling)
  - Swagger configuration

- **Models** (Entity Framework Core domain models)
  - `Request` — HTTP request definition
  - `Collection` — Request grouping
  - `Folder` — Nested hierarchy within collections
  - `Environment` — Variable container
  - `EnvironmentVariable` — Key-value pairs (encrypted at rest)
  - `RequestHistory` — Audit trail of executions (secrets redacted)
  - `Assertion` — Test assertions on responses
  - `AuthConfig` — Credential configuration (encrypted)

- **AppDbContext** (Entity Framework Core)
  - Manages all data access via LINQ
  - Handles migrations
  - Auto-migrations on startup (`Database.Migrate()`)
  - In-memory SQLite for testing

- **ProxyEngine** — HTTP request forwarding
  - `ProxyEngine.SendAsync(ProxyRequest)` — Main execution method
  - Enforces timeout (1–300 seconds)
  - Enforces body size limit (10MB)
  - Handles redirects (max 20)
  - Catches and structures errors (no exceptions thrown)
  - Returns `ProxyResponse` with status, headers, body, timing

- **AuthService** — Credential handling
  - Resolves auth configs from environment
  - Injects Authorization headers into requests
  - Uses `ICredentialProtector` for encryption/decryption
  - Sanitizes credentials from response logs

- **ImportExportService** — Data import/export
  - `PostmanImporter` — Parse Postman collection JSON
  - `CurlImporter` — Parse cURL command strings
  - `JsonExporter` — Export collections as JSON
  - `PostmanExporter` — Export in Postman-compatible format

- **WebSocketProxy** — WebSocket support
  - Establishes connections to target WebSocket servers
  - Relays messages bidirectionally
  - Maintains connection state

- **Data** folder
  - `AppDbContext.cs` — DbContext definition
  - `Migrations/` — EF Core migrations

- **Services** folder
  - Business logic services (extracted from endpoints)

---

## Data Flow

### Request Execution Flow

```
1. User clicks "Send" in Frontend
   └─ Serializes current request (URL, method, headers, body, auth, timeout)

2. Frontend sends POST /api/requests/{id}/send to Backend
   └─ Optionally includes { environmentId }

3. Backend /send endpoint handler
   ├─ Fetch request from database
   ├─ If environmentId provided:
   │  ├─ Fetch environment
   │  ├─ Resolve variables in URL, headers, body ({{VAR}} substitution)
   │  └─ Resolve auth config → extract credentials
   ├─ Inject Authorization header (if auth configured)
   ├─ Call ProxyEngine.SendAsync(request)
   └─ Receive ProxyResponse

4. ProxyEngine.SendAsync()
   ├─ Validate request (URL, size, timeout)
   ├─ Create HttpClient with timeout
   ├─ Send HTTP request to target API
   ├─ Handle response or error
   └─ Return ProxyResponse (no exceptions)

5. Backend sanitizes response
   ├─ Redact Authorization, API-Key, etc. from headers
   ├─ Redact sensitive fields from body (if configured)
   ├─ Create RequestHistory record (with redacted data)
   └─ Return response to Frontend

6. Frontend receives response
   ├─ Display status, headers, body, timing
   ├─ Run assertions against response
   └─ Update state (response history, current request state)
```

### Variable Resolution Flow

```
User request contains: {{BASE_URL}}/users
                       {{API_TOKEN}} in Authorization header

Environment contains:
  - BASE_URL = "https://api.dev.example.com"
  - API_TOKEN = "secret_xyz" (stored encrypted)

Backend resolves:
  1. Fetch environment by ID
  2. Decrypt EnvironmentVariable.Value (DPAPI)
  3. Replace {{BASE_URL}} → "https://api.dev.example.com"
  4. Replace {{API_TOKEN}} → "secret_xyz"
  5. Execute request with resolved values
  6. Redact token from response before returning to Frontend
```

---

## Data Model

### Entity Relationships

```
┌─────────────────┐
│  Collection     │
├─────────────────┤
│ id (PK)         │
│ name            │
│ description     │
│ createdAt       │
└────────┬────────┘
         │ 1:N
         │
    ┌────▼──────────┐
    │  Folder       │
    ├───────────────┤
    │ id (PK)       │
    │ collectionId  │ (FK)
    │ parentId      │ (FK, self-ref)
    │ name          │
    └────┬──────────┘
         │ 1:N
         │
    ┌────▼──────────────┐
    │  Request          │
    ├───────────────────┤
    │ id (PK)           │
    │ collectionId (FK) │
    │ folderId (FK)     │
    │ name              │
    │ method            │
    │ url               │
    │ headers (JSON)    │
    │ body              │
    │ timeout           │
    │ createdAt         │
    └────┬──────────────┘
         │ 1:N
         │
    ┌────▼──────────────┐
    │  RequestHistory   │
    ├───────────────────┤
    │ id (PK)           │
    │ requestId (FK)    │
    │ status            │
    │ headers (JSON)    │
    │ body              │
    │ duration          │
    │ executedAt        │
    └───────────────────┘

┌─────────────────┐
│  Environment    │
├─────────────────┤
│ id (PK)         │
│ name            │
│ description     │
│ isActive        │
└────────┬────────┘
         │ 1:N
         │
    ┌────▼──────────────────────┐
    │  EnvironmentVariable      │
    ├───────────────────────────┤
    │ id (PK)                   │
    │ environmentId (FK)        │
    │ key                       │
    │ value (DPAPI-encrypted)   │
    │ isSecret                  │
    │ createdAt                 │
    └───────────────────────────┘

┌─────────────────┐
│  Assertion      │
├─────────────────┤
│ id (PK)         │
│ requestId (FK)  │
│ name            │
│ type            │
│ expectedValue   │
│ createdAt       │
└─────────────────┘
```

---

## Security Architecture

See [`security-architecture.md`](security-architecture.md) for detailed security design.

**Quick Summary:**

- **At Rest** — Credentials encrypted via DPAPI, bound to user + machine
- **In Transit** — HTTPS for all external requests; localhost traffic unencrypted (acceptable for dev tool)
- **In Logs** — All secrets redacted with `[REDACTED]` in history and response logs
- **Scope** — No SSRF protection against localhost (intentional; this tool tests local/internal APIs)

---

## Technology Choices & Rationale

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Backend** | .NET 10 Minimal API | Lightweight, fast, native to Windows development; great for local tools |
| **Frontend** | Nuxt 4.4 + Nuxt UI v4 | Modern, performant, beautiful UI components out of the box |
| **Database** | SQLite | Zero-setup, file-based, perfect for local-first desktop tools |
| **ORM** | Entity Framework Core | Type-safe, LINQ queries, built-in migrations |
| **Testing Backend** | xUnit + FluentAssertions | Standard .NET testing; clear, expressive assertions |
| **Testing Frontend** | Vitest + @nuxt/test-utils | Fast, Nuxt-native, supports Vue 3 composition API |
| **API Mocking** | MSW (Mock Service Worker) | Intercepts HTTP requests at the browser level; no test infrastructure changes needed |
| **State Management** | Pinia | Vue 3–native, lightweight alternative to Vuex |
| **HTTP Client** | HttpClient (.NET) | Built-in to .NET; sufficient for proxying use case |

---

## Deployment Model

APIneer is **intentionally local-only**:

- No server deployment required
- No cloud sync or backend infrastructure
- Users run `pnpm dev` to start both services locally
- Data stored in local SQLite database (~/.apineer/ or configurable)
- Browser-based UI (no Electron, Tauri, or desktop framework)

This makes APIneer:
- ✅ Lightweight and fast
- ✅ Zero external dependencies
- ✅ Full user data ownership
- ✅ Works offline
- ✅ No privacy concerns (data never leaves the user's machine)

---

## Performance Considerations

- **Database** — In-memory SQLite in tests ensures fast, isolated test runs; disk-based in production
- **Proxy Engine** — Direct HTTP forwarding with minimal overhead; no heavy middleware
- **Frontend** — Nuxt SSR disabled for SPA efficiency; lazy-loading of components
- **Caching** — Frontend caches collections and environments in Pinia; minimal redundant API calls
- **History** — Paginated (20 items default) to avoid loading thousands of old entries

---

## Extensibility

Future extensions are straightforward:

- **New Import Format** — Implement new importer (e.g., Thunder Client, Insomnia) in `ImportExport/`
- **New Auth Scheme** — Add new auth type in `Auth/` (e.g., OAuth 2.0, Digest, API Key)
- **Assertion Types** — Extend assertion evaluator for new assertion types (e.g., response time, regex matching)
- **Code Generation** — Add new language generator in `CodeGen/` (e.g., Go, Rust, PHP)
- **Custom Proxy Rules** — Extend ProxyEngine with request/response middleware

