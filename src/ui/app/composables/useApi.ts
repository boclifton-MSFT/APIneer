import type { UseFetchOptions } from 'nuxt/app'

export interface ApiRequest {
  id: string
  name: string
  method: string
  url: string
  headers?: { key: string; value: string }[]
  body?: string
  bodyType?: string
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
  statusCode: number
  statusText: string
  timeMs: number
  sizeBytes: number
  createdAt: string
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
    return await $fetch<ApiResponse>(`/api/requests/${id}/send`, { method: 'POST' })
  }

  // Collections
  async function getCollections() {
    return await $fetch<Collection[]>('/api/collections')
  }

  async function createCollection(data: { name: string; description?: string }) {
    return await $fetch<Collection>('/api/collections', {
      method: 'POST',
      body: data
    })
  }

  async function deleteCollection(id: string) {
    return await $fetch(`/api/collections/${id}`, { method: 'DELETE' })
  }

  // History
  async function getHistory() {
    return await $fetch<HistoryEntry[]>('/api/history')
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
    deleteVariable
  }
}
