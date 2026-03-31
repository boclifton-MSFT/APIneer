# API Reference

APIneer exposes a complete RESTful API for managing requests, collections, environments, and executing HTTP calls against target APIs.

**Base URL:** `http://localhost:5000`

**Documentation:** [Swagger UI](http://localhost:5000/swagger) (available in development mode)

---

## Core Endpoints

### Requests API

Manage HTTP requests stored in your workspace.

#### `POST /api/requests`
Create a new request.

**Request Body:**
```json
{
  "name": "Get Users",
  "method": "GET",
  "url": "https://api.example.com/users",
  "collectionId": "abc123",
  "description": "Fetch all users",
  "headers": [
    { "key": "Authorization", "value": "Bearer token123", "isActive": true }
  ],
  "body": null,
  "timeout": 30
}
```

**Response:** `201 Created`
```json
{
  "id": "req_xyz",
  "name": "Get Users",
  "method": "GET",
  "url": "https://api.example.com/users",
  "createdAt": "2026-03-30T12:00:00Z"
}
```

**Validation:**
- `name` required, non-empty string
- `url` required, valid HTTP/HTTPS URL
- `method` required, one of: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS
- `collectionId` required, valid GUID
- `body` max 10MB
- `timeout` 1–300 seconds (optional, default 30s)

#### `GET /api/requests`
List all requests (paginated).

**Query Parameters:**
- `page` (optional, default 1)
- `pageSize` (optional, default 20, max 100)

**Response:** `200 OK`
```json
{
  "data": [
    {
      "id": "req_xyz",
      "name": "Get Users",
      "method": "GET",
      "url": "https://api.example.com/users",
      "collectionId": "col_abc"
    }
  ],
  "total": 42,
  "page": 1,
  "pageSize": 20
}
```

#### `GET /api/requests/{id}`
Get a single request by ID.

**Response:** `200 OK`
```json
{
  "id": "req_xyz",
  "name": "Get Users",
  "method": "GET",
  "url": "https://api.example.com/users",
  "collectionId": "col_abc",
  "headers": [...],
  "body": null,
  "timeout": 30,
  "createdAt": "2026-03-30T12:00:00Z",
  "updatedAt": "2026-03-30T12:05:00Z"
}
```

#### `PUT /api/requests/{id}`
Update a request.

**Request Body:** Same as POST (any field can be updated)

**Response:** `200 OK` (returns updated request)

#### `DELETE /api/requests/{id}`
Delete a request.

**Response:** `204 No Content`

#### `POST /api/requests/{id}/send`
Execute a request against the target API.

**Request Body:**
```json
{
  "environmentId": "env_123"  // optional, for variable resolution
}
```

**Response:** `200 OK`
```json
{
  "status": 200,
  "statusText": "OK",
  "headers": {
    "content-type": "application/json",
    "content-length": "256"
  },
  "body": "{\"users\": []}",
  "size": 256,
  "duration": 145,
  "sentAt": "2026-03-30T12:00:00Z"
}
```

**Error Responses:**
- `400 Bad Request` — Invalid request (missing URL, invalid method)
- `408 Request Timeout` — Request exceeded timeout limit
- `413 Payload Too Large` — Request body exceeds 10MB
- `500 Internal Server Error` — Proxy engine error (e.g., DNS failure, connection refused)

#### `GET /api/requests/{id}/history`
Get request/response history for a request (paginated).

**Query Parameters:**
- `page` (optional, default 1)
- `pageSize` (optional, default 20)

**Response:** `200 OK`
```json
{
  "data": [
    {
      "id": "hist_001",
      "status": 200,
      "statusText": "OK",
      "headers": { "content-type": "application/json" },
      "body": "[REDACTED]",  // secrets sanitized
      "size": 256,
      "duration": 145,
      "executedAt": "2026-03-30T12:00:00Z"
    }
  ],
  "total": 15,
  "page": 1,
  "pageSize": 20
}
```

**Note:** Secrets in history are redacted — Authorization headers, API keys, etc. are replaced with `[REDACTED]`.

---

### Collections API

Organize requests into collections and folders.

#### `POST /api/collections`
Create a new collection.

**Request Body:**
```json
{
  "name": "User API",
  "description": "Collection for user management endpoints"
}
```

**Response:** `201 Created`

#### `GET /api/collections`
List all collections (paginated).

**Response:** `200 OK`
```json
{
  "data": [
    {
      "id": "col_abc",
      "name": "User API",
      "description": "Collection for user management endpoints",
      "requestCount": 5,
      "createdAt": "2026-03-30T12:00:00Z"
    }
  ],
  "total": 10,
  "page": 1,
  "pageSize": 20
}
```

#### `GET /api/collections/{id}`
Get a collection with all nested folders and requests.

**Response:** `200 OK`
```json
{
  "id": "col_abc",
  "name": "User API",
  "description": "Collection for user management endpoints",
  "folders": [
    {
      "id": "fold_001",
      "name": "Users",
      "requests": [
        {
          "id": "req_xyz",
          "name": "Get Users",
          "method": "GET"
        }
      ]
    }
  ],
  "requests": [...]
}
```

#### `PUT /api/collections/{id}`
Update a collection.

**Response:** `200 OK`

#### `DELETE /api/collections/{id}`
Delete a collection (and all nested requests/folders).

**Response:** `204 No Content`

#### `POST /api/collections/{collectionId}/folders`
Create a folder within a collection.

**Request Body:**
```json
{
  "name": "Users",
  "description": "User management endpoints"
}
```

**Response:** `201 Created`

#### `DELETE /api/collections/{collectionId}/folders/{folderId}`
Delete a folder and all nested requests.

**Response:** `204 No Content`

#### `PATCH /api/collections/{id}/reorder`
Reorder folders or requests within a collection.

**Request Body:**
```json
{
  "items": [
    { "id": "fold_001", "position": 0 },
    { "id": "fold_002", "position": 1 },
    { "id": "req_xyz", "position": 2 }
  ]
}
```

**Response:** `200 OK`

#### `POST /api/collections/{id}/duplicate`
Duplicate a collection (with all nested folders and requests).

**Response:** `201 Created` (returns new collection)

---

### Environments API

Manage environments and variables for request templating.

#### `POST /api/environments`
Create a new environment.

**Request Body:**
```json
{
  "name": "Development",
  "description": "Development server environment"
}
```

**Response:** `201 Created`

#### `GET /api/environments`
List all environments.

**Response:** `200 OK`
```json
{
  "data": [
    {
      "id": "env_dev",
      "name": "Development",
      "description": "Development server environment",
      "isActive": true,
      "variableCount": 3,
      "createdAt": "2026-03-30T12:00:00Z"
    }
  ]
}
```

#### `GET /api/environments/{id}`
Get an environment with all variables.

**Response:** `200 OK`
```json
{
  "id": "env_dev",
  "name": "Development",
  "description": "Development server environment",
  "isActive": true,
  "variables": [
    {
      "id": "var_001",
      "key": "BASE_URL",
      "value": "[REDACTED]",  // actual value encrypted
      "isSecret": false
    }
  ]
}
```

#### `PUT /api/environments/{id}`
Update an environment.

**Response:** `200 OK`

#### `DELETE /api/environments/{id}`
Delete an environment.

**Response:** `204 No Content`

#### `PUT /api/environments/{id}/activate`
Activate an environment (only one environment can be active).

**Response:** `200 OK`

#### `PUT /api/environments/{id}/deactivate`
Deactivate an environment.

**Response:** `200 OK`

#### `POST /api/environments/{id}/variables`
Create a variable in an environment.

**Request Body:**
```json
{
  "key": "BASE_URL",
  "value": "https://api.dev.example.com",
  "isSecret": false
}
```

**Response:** `201 Created`

#### `GET /api/environments/{id}/variables/{varId}`
Get a variable (value is redacted if secret).

**Response:** `200 OK`

#### `PUT /api/environments/{id}/variables/{varId}`
Update a variable.

**Response:** `200 OK`

#### `DELETE /api/environments/{id}/variables/{varId}`
Delete a variable.

**Response:** `204 No Content`

#### `POST /api/environments/resolve`
Resolve environment variables in a request (for variable substitution).

**Request Body:**
```json
{
  "environmentId": "env_dev",
  "url": "{{BASE_URL}}/users",
  "headers": {
    "Authorization": "Bearer {{API_TOKEN}}"
  }
}
```

**Response:** `200 OK`
```json
{
  "url": "https://api.dev.example.com/users",
  "headers": {
    "Authorization": "Bearer xyz123abc"
  }
}
```

---

### History API

Access global request/response history.

#### `GET /api/history`
Get all request/response history (paginated, most recent first).

**Query Parameters:**
- `page` (optional, default 1)
- `pageSize` (optional, default 20)

**Response:** `200 OK`
```json
{
  "data": [
    {
      "id": "hist_001",
      "requestId": "req_xyz",
      "requestName": "Get Users",
      "method": "GET",
      "url": "https://api.example.com/users",
      "status": 200,
      "duration": 145,
      "executedAt": "2026-03-30T12:00:00Z"
    }
  ],
  "total": 250,
  "page": 1,
  "pageSize": 20
}
```

#### `DELETE /api/history`
Clear all history.

**Response:** `204 No Content`

---

### Assertions API

Define and run test assertions on responses.

#### `POST /api/requests/{id}/assertions`
Create an assertion for a request.

**Request Body:**
```json
{
  "name": "Status is 200",
  "type": "status",
  "expectedValue": "200"
}
```

Assertion types:
- `status` — HTTP status code
- `header` — Response header value (requires `headerName` field)
- `body` — Response body content (JSON path or substring match)
- `time` — Response time in milliseconds

**Response:** `201 Created`

#### `GET /api/requests/{id}/assertions`
Get all assertions for a request.

**Response:** `200 OK`

#### `POST /api/requests/{id}/test`
Execute all assertions for a request and return results.

**Response:** `200 OK`
```json
{
  "passed": true,
  "results": [
    {
      "name": "Status is 200",
      "passed": true,
      "message": "Status code 200 matches expected 200"
    }
  ]
}
```

---

### Code Generation

Generate API client code from requests.

#### `GET /api/requests/{id}/code`
Generate code snippet for a request.

**Query Parameters:**
- `language` — Target language: `curl`, `javascript`, `python`, `go`, `rust`, `csharp`

**Response:** `200 OK` (plain text)
```bash
# Example: language=curl
curl -X GET https://api.example.com/users \
  -H "Authorization: Bearer token123" \
  -H "Content-Type: application/json"
```

---

### Import/Export API

Import requests from external tools and export collections.

#### `POST /api/import/postman`
Import a Postman collection.

**Request:** Form data with `file` (JSON file upload)

**Response:** `200 OK`
```json
{
  "collectionId": "col_imported",
  "requestCount": 42,
  "folderCount": 5
}
```

#### `POST /api/import/curl`
Import a request from a cURL command.

**Request Body:**
```json
{
  "curl": "curl -X GET https://api.example.com/users -H 'Authorization: Bearer token123'"
}
```

**Response:** `201 Created` (returns created request)

#### `POST /api/import/json`
Import a request or collection from JSON.

**Request Body:**
```json
{
  "format": "collection",
  "data": { ... }
}
```

**Response:** `200 OK`

#### `GET /api/collections/{id}/export`
Export a collection.

**Query Parameters:**
- `format` — `json` (default) or `postman`

**Response:** `200 OK` (JSON or Postman-compatible format)

---

### WebSocket API

Interactive WebSocket client for real-time testing.

#### `GET /api/ws/connect`
Establish a WebSocket connection to a target.

**Query Parameters:**
- `url` — WebSocket URL (e.g., `wss://echo.websocket.org`)

**Response:** `101 Switching Protocols`

#### `POST /api/ws/send`
Send a message over the active WebSocket connection.

**Request Body:**
```json
{
  "message": "Hello, WebSocket!"
}
```

**Response:** `200 OK`

#### `GET /api/ws/status`
Get the status of the current WebSocket connection.

**Response:** `200 OK`
```json
{
  "connected": true,
  "url": "wss://echo.websocket.org",
  "connectedAt": "2026-03-30T12:00:00Z",
  "messageCount": 5
}
```

#### `DELETE /api/ws/disconnect`
Close the WebSocket connection.

**Response:** `204 No Content`

#### `GET /api/ws/messages`
Get all messages from the current WebSocket session.

**Response:** `200 OK`
```json
{
  "messages": [
    {
      "id": "msg_001",
      "direction": "sent",
      "content": "Hello, WebSocket!",
      "timestamp": "2026-03-30T12:00:00Z"
    },
    {
      "id": "msg_002",
      "direction": "received",
      "content": "Hello, WebSocket!",
      "timestamp": "2026-03-30T12:00:01Z"
    }
  ]
}
```

---

## Utility Endpoints

#### `GET /`
Redirects to Swagger UI.

#### `GET /health`
Health check endpoint.

**Response:** `200 OK`
```json
{
  "status": "healthy"
}
```

#### `GET /swagger`
Interactive Swagger API documentation (development mode only).

---

## Error Handling

All errors follow a standard format:

```json
{
  "error": "Request validation failed",
  "details": "URL is required",
  "code": "VALIDATION_ERROR"
}
```

**Common Status Codes:**
- `400 Bad Request` — Validation error
- `404 Not Found` — Resource not found
- `408 Request Timeout` — Request exceeded timeout
- `413 Payload Too Large` — Body exceeds 10MB
- `500 Internal Server Error` — Unexpected error (details logged server-side)

---

## Authentication & Security

- **Credentials:** Encrypted at rest using DPAPI, never stored plaintext
- **Secrets in Responses:** Authorization headers, API keys, and passwords are redacted with `[REDACTED]` in history and logs
- **Timeout:** Default 30 seconds, configurable 1–300 seconds per request
- **Size Limit:** Max 10MB request body
- **Redirects:** Max 20 automatic redirects

See [`security-architecture.md`](security-architecture.md) for full security details.

---

## Environment Variable Substitution

Use double-brace syntax to reference environment variables in requests:

```
URL: {{BASE_URL}}/users
Header: Authorization: Bearer {{API_TOKEN}}
Body: { "userId": "{{USER_ID}}" }
```

The backend automatically resolves these to the active environment's variable values before execution.

---

## Rate Limiting

No built-in rate limiting. APIneer respects the target API's rate limits — adhere to your downstream service's constraints.

