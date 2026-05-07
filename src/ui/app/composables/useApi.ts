export interface ApiRequest {
  id: string
  name: string
  method: string
  url: string
  headers?: { key: string; value: string }[]
  body?: string
  bodyType?: string
  authConfig?: string
  collectionId?: string | null
}

export interface ApiResponse {
  statusCode: number
  statusText: string
  body: string
  contentType: string
  headers: { name: string; value: string }[]
  timeMs: number
  sizeBytes: number
  error?: string
}

export interface Collection {
  id: string
  name: string
  description?: string
  folders: CollectionFolder[]
  requests: CollectionRequest[]
}

export interface CollectionFolder {
  id: string
  name: string
  sortOrder: number
  subFolders: CollectionFolder[]
  requests: CollectionRequest[]
}

export interface CollectionRequest {
  id: string
  name: string
  method: string
  url: string
  sortOrder: number
}

export interface HistoryEntry {
  id: string
  method: string
  url: string
  responseStatus: number
  statusText: string
  responseTimeMs: number
  responseSizeBytes: number
  executedAt: string
  requestId?: string
}

export interface Environment {
  id: string
  name: string
  isActive: boolean
  workspaceId: string
  variables?: EnvironmentVariable[]
}

export interface EnvironmentVariable {
  id: string
  key: string
  value: string
  isSecret: boolean
  environmentId: string
}

// MCP Types
export interface McpServerConfig {
  id: string
  name: string
  transportType: 'stdio' | 'streamable-http'
  command?: string
  args?: string
  environmentVariables?: string
  url?: string
  headers?: string
}

export interface McpTool {
  name: string
  description?: string
  inputSchema?: any
}

export interface McpResource {
  uri: string
  name: string
  description?: string
  mimeType?: string
}

export interface McpPrompt {
  name: string
  description?: string
  arguments?: any[]
}

export interface McpOAuthStartResponse {
  flowId: string
  userCode: string
  verificationUri: string
  expiresIn: number
}

export interface McpOAuthStatusResponse {
  status: 'pending' | 'complete' | 'expired' | 'denied'
  error?: string
}

/**
 * Composable for making API calls to the APIneer backend.
 * All routes are proxied through Nuxt's routeRules to localhost:5000.
 */
export function useApi() {
  // Requests
  async function getRequests() {
    return await $fetch<ApiRequest[]>('/api/requests')
  }

  async function getRequest(id: string) {
    return await $fetch<ApiRequest>(`/api/requests/${id}`)
  }

  async function createRequest(data: { name: string; method?: string; url?: string; collectionId?: string }) {
    return await $fetch<ApiRequest>('/api/requests', {
      method: 'POST',
      body: data
    })
  }

  async function updateRequest(id: string, data: Partial<ApiRequest>) {
    return await $fetch<ApiRequest>(`/api/requests/${id}`, {
      method: 'PUT',
      body: data
    })
  }

  async function deleteRequest(id: string) {
    return await $fetch(`/api/requests/${id}`, { method: 'DELETE' })
  }

  async function sendRequest(id: string) {
    const raw = await $fetch<{
      responseStatus: number
      responseHeaders: string | null
      responseBody: string | null
      responseTimeMs: number
      responseSizeBytes: number
      error: { code: string; message: string } | null
    }>(`/api/requests/${id}/send`, { method: 'POST' })

    if (raw.error) {
      return {
        statusCode: 0,
        statusText: 'Error',
        body: '',
        contentType: '',
        headers: [],
        timeMs: 0,
        sizeBytes: 0,
        error: raw.error.message
      } as ApiResponse
    }

    let headers: { name: string; value: string }[] = []
    let contentType = ''
    if (raw.responseHeaders) {
      try {
        const parsed = JSON.parse(raw.responseHeaders)
        headers = Object.entries(parsed).map(([name, value]) => ({
          name,
          value: Array.isArray(value) ? value.join(', ') : String(value)
        }))
        const ct = parsed['Content-Type'] || parsed['content-type'] || ''
        contentType = Array.isArray(ct) ? ct[0] : ct
      } catch {
        // If header parsing fails, leave empty
      }
    }

    const statusTexts: Record<number, string> = {
      200: 'OK', 201: 'Created', 204: 'No Content',
      301: 'Moved Permanently', 302: 'Found', 304: 'Not Modified',
      400: 'Bad Request', 401: 'Unauthorized', 403: 'Forbidden',
      404: 'Not Found', 405: 'Method Not Allowed', 409: 'Conflict',
      422: 'Unprocessable Entity', 429: 'Too Many Requests',
      500: 'Internal Server Error', 502: 'Bad Gateway',
      503: 'Service Unavailable', 504: 'Gateway Timeout'
    }

    return {
      statusCode: raw.responseStatus,
      statusText: statusTexts[raw.responseStatus] || '',
      body: raw.responseBody || '',
      contentType,
      headers,
      timeMs: raw.responseTimeMs,
      sizeBytes: raw.responseSizeBytes
    } as ApiResponse
  }

  // Collections
  async function getCollections() {
    const response = await $fetch<{ items: Collection[], page: number, pageSize: number, totalCount: number }>('/api/collections')
    return response.items
  }

  async function createCollection(data: { name: string; description?: string }) {
    return await $fetch<Collection>('/api/collections', {
      method: 'POST',
      body: data
    })
  }

  async function updateCollection(id: string, data: Partial<Collection>) {
    return await $fetch<Collection>(`/api/collections/${id}`, {
      method: 'PUT',
      body: data
    })
  }

  async function deleteCollection(id: string) {
    return await $fetch(`/api/collections/${id}`, { method: 'DELETE' })
  }

  // History
  async function getHistory() {
    const response = await $fetch<{ items: HistoryEntry[], page: number, pageSize: number, totalCount: number }>('/api/history')
    return response.items
  }

  async function clearHistory() {
    return await $fetch('/api/history', { method: 'DELETE' })
  }

  // Environments
  async function getEnvironments() {
    return await $fetch<Environment[]>('/api/environments')
  }

  async function getEnvironment(id: string) {
    return await $fetch<Environment>(`/api/environments/${id}`)
  }

  async function createEnvironment(data: { name: string; workspaceId?: string }) {
    return await $fetch<Environment>('/api/environments', {
      method: 'POST',
      body: data
    })
  }

  async function updateEnvironment(id: string, data: Partial<Environment>) {
    return await $fetch<Environment>(`/api/environments/${id}`, {
      method: 'PUT',
      body: data
    })
  }

  async function deleteEnvironment(id: string) {
    return await $fetch(`/api/environments/${id}`, { method: 'DELETE' })
  }

  // Environment Variables
  async function addVariable(environmentId: string, data: { key: string; value: string; isSecret?: boolean }) {
    return await $fetch<EnvironmentVariable>(`/api/environments/${environmentId}/variables`, {
      method: 'POST',
      body: data
    })
  }

  async function updateVariable(environmentId: string, variableId: string, data: { key: string; value: string; isSecret?: boolean }) {
    return await $fetch<EnvironmentVariable>(`/api/environments/${environmentId}/variables/${variableId}`, {
      method: 'PUT',
      body: data
    })
  }

  async function deleteVariable(environmentId: string, variableId: string) {
    return await $fetch(`/api/environments/${environmentId}/variables/${variableId}`, { method: 'DELETE' })
  }

  // Collection drag-drop
  async function moveRequest(requestId: string, target: { collectionId: string; folderId?: string | null }) {
    return await $fetch(`/api/requests/${requestId}/move`, {
      method: 'PATCH',
      body: { collectionId: target.collectionId, folderId: target.folderId ?? null }
    })
  }

  async function reorderCollection(collectionId: string, itemIds: string[]) {
    return await $fetch(`/api/collections/${collectionId}/reorder`, {
      method: 'PATCH',
      body: { itemIds }
    })
  }

  // MCP Server Configs
  async function getServerConfigs() {
    return await $fetch<McpServerConfig[]>('/api/mcp/servers')
  }

  async function createServerConfig(data: Partial<McpServerConfig>) {
    return await $fetch<McpServerConfig>('/api/mcp/servers', {
      method: 'POST',
      body: data
    })
  }

  async function updateServerConfig(id: string, data: Partial<McpServerConfig>) {
    return await $fetch<McpServerConfig>(`/api/mcp/servers/${id}`, {
      method: 'PUT',
      body: data
    })
  }

  async function deleteServerConfig(id: string) {
    return await $fetch(`/api/mcp/servers/${id}`, { method: 'DELETE' })
  }

  // MCP Connection
  async function mcpConnect(config: { serverId?: string; transportType: string; command?: string; args?: string; env?: Record<string, string>; url?: string; headers?: Record<string, string> }) {
    return await $fetch<{ connectionId: string; capabilities: any; serverInfo: any }>('/api/mcp/connect', {
      method: 'POST',
      body: config
    })
  }

  async function mcpDisconnect(connectionId: string) {
    return await $fetch<void>(`/api/mcp/connections/${connectionId}`, { method: 'DELETE' })
  }

  async function mcpStatus(connectionId: string) {
    return await $fetch<{ state: string; capabilities: any; serverInfo: any }>(`/api/mcp/connections/${connectionId}/status`)
  }

  // MCP Operations
  async function mcpListTools(connectionId: string) {
    return await $fetch<{ tools: McpTool[] }>(`/api/mcp/connections/${connectionId}/tools`)
  }

  async function mcpCallTool(connectionId: string, name: string, args: Record<string, any>) {
    return await $fetch<any>(`/api/mcp/connections/${connectionId}/tools/${name}`, {
      method: 'POST',
      body: args
    })
  }

  async function mcpListResources(connectionId: string) {
    return await $fetch<{ resources: McpResource[] }>(`/api/mcp/connections/${connectionId}/resources`)
  }

  async function mcpReadResource(connectionId: string, uri: string) {
    return await $fetch<any>(`/api/mcp/connections/${connectionId}/resources/read`, {
      method: 'POST',
      body: { uri }
    })
  }

  async function mcpListPrompts(connectionId: string) {
    return await $fetch<{ prompts: McpPrompt[] }>(`/api/mcp/connections/${connectionId}/prompts`)
  }

  async function mcpGetPrompt(connectionId: string, name: string, args: Record<string, any>) {
    return await $fetch<any>(`/api/mcp/connections/${connectionId}/prompts/${name}`, {
      method: 'POST',
      body: args
    })
  }

  async function mcpPing(connectionId: string) {
    return await $fetch<void>(`/api/mcp/connections/${connectionId}/ping`, { method: 'POST' })
  }

  // MCP OAuth
  async function startMcpOAuth(serverId: string, clientId: string, scopes?: string): Promise<McpOAuthStartResponse> {
    return await $fetch<McpOAuthStartResponse>('/api/mcp/oauth/start', {
      method: 'POST',
      body: { serverId, clientId, scopes }
    })
  }

  async function pollMcpOAuth(flowId: string): Promise<McpOAuthStatusResponse> {
    return await $fetch<McpOAuthStatusResponse>(`/api/mcp/oauth/status/${flowId}`)
  }

  async function revokeMcpOAuth(serverId: string): Promise<void> {
    await $fetch(`/api/mcp/oauth/token/${serverId}`, { method: 'DELETE' })
  }

  return {
    // Requests
    getRequests,
    getRequest,
    createRequest,
    updateRequest,
    deleteRequest,
    sendRequest,
    // Collections
    getCollections,
    createCollection,
    updateCollection,
    deleteCollection,
    // History
    getHistory,
    clearHistory,
    // Environments
    getEnvironments,
    getEnvironment,
    createEnvironment,
    updateEnvironment,
    deleteEnvironment,
    // Variables
    addVariable,
    updateVariable,
    deleteVariable,
    // Collection drag-drop
    moveRequest,
    reorderCollection,
    // MCP Server Configs
    getServerConfigs,
    createServerConfig,
    updateServerConfig,
    deleteServerConfig,
    // MCP Connection
    mcpConnect,
    mcpDisconnect,
    mcpStatus,
    // MCP Operations
    mcpListTools,
    mcpCallTool,
    mcpListResources,
    mcpReadResource,
    mcpListPrompts,
    mcpGetPrompt,
    mcpPing,
    // MCP OAuth
    startMcpOAuth,
    pollMcpOAuth,
    revokeMcpOAuth
  }
}
