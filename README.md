# APIneer

**APIneer** — A locally running API development platform built with .NET 10 and Nuxt UI. Design, test, and debug API requests with built-in collections, environments, assertions, and full request/response introspection.

Think Postman, but local-first and desktop-ready — no cloud sync, no sign-ups, just fast API development at localhost.

## Features

- 🚀 **Request Builder** — Intuitive UI for crafting HTTP requests with full method support (GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS)
- 📦 **Collections & Folders** — Organize requests into nested hierarchies with reusable folder structures
- 🌍 **Environments** — Define environment variables and switch contexts seamlessly
- 🔐 **Authentication** — Built-in auth config with secure credential storage (encrypted at rest via DPAPI)
- 📤 **Import/Export** — Load requests from Postman, cURL, or JSON; export collections as JSON or Postman-compatible format
- 🧪 **Assertions** — Write test assertions on responses (status code, headers, body content)
- 💾 **History** — Full request/response history with redacted secrets for security
- 🔌 **WebSocket Support** — Interactive WebSocket client for real-time testing
- 📝 **Code Generation** — Generate API client code (cURL, JavaScript, Python, etc.) from requests
- ⚡ **Proxy Engine** — Intelligent HTTP proxy with timeout, redirect, and size limits

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [pnpm](https://pnpm.io/) (`npm install -g pnpm`)

## Quick Start

```bash
pnpm install
pnpm dev
```

This starts both services concurrently:

| Service  | URL                    |
| -------- | ---------------------- |
| API      | http://localhost:5000  |
| Frontend | http://localhost:3000  |
| Swagger  | http://localhost:5000/swagger |

## Scripts

| Command        | Description                          |
| -------------- | ------------------------------------ |
| `pnpm dev`     | Run API + UI concurrently            |
| `pnpm build`   | Build both projects                  |
| `pnpm test`    | Run all tests (backend + frontend)   |
| `pnpm test:api`| Run backend tests only               |
| `pnpm test:ui` | Run frontend tests only              |
| `pnpm dev:api` | Run backend only                     |
| `pnpm dev:ui`  | Run frontend only                    |

## TDD Workflow

This project follows strict **Red → Green → Refactor**:

1. **Red** — Write a failing test that describes the behavior you want
2. **Green** — Write the minimal code to make the test pass
3. **Refactor** — Clean up the code while keeping tests green

Backend tests use **xUnit + FluentAssertions + NSubstitute** with `WebApplicationFactory` for integration tests. Frontend tests use **Vitest + Vue Test Utils + MSW** for component and API mocking.

## Architecture

APIneer uses a **two-tier architecture**:

- **Backend API** (.NET 10 Minimal API) on `localhost:5000` — handles request execution, environment resolution, authentication, and proxying
- **Frontend UI** (Nuxt 4.4 + Nuxt UI v4) on `localhost:3000` — interactive request builder and response viewer
- **Database** — SQLite with Entity Framework Core, stored locally

### Data Flow

```
User Input (Request Builder)
    ↓
Frontend sends to Backend API
    ↓
Backend resolves environment variables
    ↓
Backend injects auth headers (if configured)
    ↓
Proxy Engine sends HTTP request to target API
    ↓
Response captured and sanitized (secrets redacted)
    ↓
Response returned to Frontend for rendering
```

### Key Components

**Backend:**
- `ProxyEngine` — HTTP client with timeout, redirect, and size limit enforcement
- `Models` — Request, Collection, Environment, EnvironmentVariable, RequestHistory, Assertion entities
- `Auth` — Credential storage and injection (DPAPI-encrypted at rest)
- `ImportExport` — Postman, cURL, and JSON import/export
- `WebSocket` — WebSocket proxy for real-time testing

**Frontend:**
- `layouts/dashboard` — Main two-panel layout (sidebar + content)
- `components/RequestBuilder` — URL, method, headers, body, auth editor
- `components/ResponseViewer` — Status, headers, body, timing, size
- `stores/request` — Pinia store for current request state
- `stores/collection` — Collection/folder hierarchy management

## Project Structure

```
APIneer/
├── src/
│   ├── api/
│   │   └── APIneer.Api/            # .NET 10 Minimal API backend
│   │       ├── Data/               # EF Core DbContext + migrations
│   │       ├── Models/             # Entity models (Request, Collection, etc.)
│   │       ├── Auth/               # Authentication config & credential protection
│   │       ├── Proxy/              # ProxyEngine for HTTP forwarding
│   │       ├── ImportExport/       # Postman/cURL/JSON importers and exporters
│   │       ├── WebSocket/          # WebSocket proxy implementation
│   │       ├── Services/           # Business logic services
│   │       └── Program.cs          # Minimal API route definitions
│   └── ui/
│       ├── app/
│       │   ├── components/         # Vue components (RequestBuilder, ResponseViewer, etc.)
│       │   ├── composables/        # Reusable Vue composition functions
│       │   ├── layouts/            # Dashboard and utility layouts
│       │   ├── pages/              # Page components
│       │   ├── stores/             # Pinia state management
│       │   └── app.vue             # Root component
│       ├── public/                 # Static assets
│       ├── tests/                  # Vitest unit & component tests
│       └── nuxt.config.ts          # Nuxt configuration
├── tests/
│   └── APIneer.Api.Tests/          # Backend xUnit test suite
│       ├── Fixtures/               # Test infrastructure & mocks
│       ├── Proxy/                  # ProxyEngine integration tests
│       ├── Requests/               # Request CRUD endpoint tests
│       ├── Collections/            # Collection management tests
│       └── Environments/           # Environment and variable tests
├── docs/
│   ├── api-reference.md            # API endpoint documentation
│   ├── architecture.md             # System architecture & design
│   └── security-architecture.md    # Security design & threat model
├── package.json                    # Root orchestration scripts
└── APIneer.slnx                    # .NET solution file
```

## API Overview

APIneer exposes a RESTful API on `localhost:5000` with the following endpoint groups:

| Group | Endpoints | Purpose |
|-------|-----------|---------|
| **Requests** | `GET/POST /api/requests`, `GET/PUT/DELETE /api/requests/{id}` | CRUD operations on HTTP requests |
| **Collections** | `GET/POST /api/collections`, `GET/PUT/DELETE /api/collections/{id}` | Organize requests into collections |
| **Environments** | `GET/POST /api/environments`, manage variables | Define and switch environment contexts |
| **History** | `GET /api/history`, `GET /api/requests/{id}/history` | Access request/response history with redacted secrets |
| **Execution** | `POST /api/requests/{id}/send` | Execute a request against a target API |
| **Assertions** | `POST/GET /api/requests/{id}/assertions`, `POST /api/requests/{id}/test` | Define and run test assertions |
| **Code Generation** | `GET /api/requests/{id}/code?language=curl\|js\|python` | Generate client code snippets |
| **Import/Export** | `POST /api/import/{postman\|curl\|json}`, `GET /api/collections/{id}/export` | Import from/export to Postman, cURL, JSON |
| **WebSocket** | `GET /api/ws/connect`, `POST /api/ws/send`, `GET /api/ws/status` | Real-time WebSocket testing |

**Full API documentation** — Swagger UI available at `http://localhost:5000/swagger` when running in development mode.

See [`docs/api-reference.md`](docs/api-reference.md) for detailed endpoint documentation.

## Security

**APIneer's security model is built on three pillars:**

1. **Encryption at Rest** — Credentials and environment variables are encrypted using DPAPI (Data Protection API) before storage in SQLite. Keys are bound to the current user and machine.

2. **Backend-Only Decryption** — The frontend never handles raw secrets. When executing a request, the backend resolves environment variables and injects auth headers server-side, then sanitizes responses before sending them to the UI.

3. **Log Sanitization** — All request/response logs use `[REDACTED]` placeholders for sensitive data (Authorization headers, API keys, passwords, etc.). No plaintext secrets appear in logs, history, or UI responses.

**See [`docs/security-architecture.md`](docs/security-architecture.md) for the full security design, threat model, and implementation details.**

Key constraints:
- Max request body: 10MB
- Request timeout: 1–300 seconds (default 30s)
- Max redirects: 20
- No SSRF protection against localhost (intentional — this tool is for local/internal API testing)

## Configuration

Copy `.env.example` to `.env` and adjust as needed:

```bash
cp .env.example .env
```

## Running Tests

APIneer uses **Test-Driven Development (TDD)** exclusively. Tests are your safety net and first line of defense.

Run all tests:
```bash
pnpm test
```

Run backend tests only:
```bash
pnpm test:api
```

Run frontend tests only:
```bash
pnpm test:ui
```

### Backend Tests
- **Framework:** xUnit
- **Assertions:** FluentAssertions
- **Mocking:** NSubstitute
- **Integration:** `WebApplicationFactory<Program>` with in-memory SQLite

### Frontend Tests
- **Framework:** Vitest 4
- **Utilities:** @nuxt/test-utils with Nuxt environment
- **Mocking:** MSW (Mock Service Worker) for API intercepts
- **Vue Testing:** @vue/test-utils

## Contributing

All development follows **Red → Green → Refactor**:

1. **Red** — Write a failing test that describes the desired behavior
2. **Green** — Write the minimal code to make the test pass
3. **Refactor** — Improve the code while keeping tests green

**Guidelines:**
- Write tests first, implementation second — never the other way around
- Keep tests focused and independent; avoid cross-test dependencies
- Use descriptive test names that explain what is being tested and why
- Run the full test suite before pushing (no breaking changes)
- Document non-obvious behavior in code comments

## Development with Squad

Built with [GitHub Copilot Squad](https://github.com/features/copilot) — AI-powered team collaboration. Squad agents work together to design features, implement backend and frontend code, write tests, and ship end-to-end solutions.

**Team Members:**
- **Geralt** — Project Lead, architecture & integration
- **Marcus** — Backend engineer, .NET 10 API implementation
- **Freeman** — QA & testing specialist, TDD discipline
- **Kratos** — Frontend engineer, Nuxt UI & component development
- **Payne** — Security specialist, threat modeling & compliance
