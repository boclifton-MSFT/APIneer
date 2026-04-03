export type ConnectionState = 'disconnected' | 'connecting' | 'connected'
export type AuthMethod = 'none' | 'bearer-token' | 'github-oauth'
export type OAuthFlowStatus = 'idle' | 'pending' | 'complete' | 'expired' | 'denied' | 'error'

export interface OAuthUIState {
  status: OAuthFlowStatus
  userCode?: string
  verificationUri?: string
  /** ms timestamp: Date.now() + expiresIn * 1000 */
  expiresAt?: number
  errorMessage?: string
}

export interface McpFormData {
  name: string
  transportType: 'stdio' | 'streamable-http'
  command: string
  args: string
  url: string
  envVars: { key: string; value: string }[]
  customHeaders: { key: string; value: string }[]
  authMethod: AuthMethod
  oauthClientId: string
  oauthScopes: string
}

export function buildEnvObject(
  entries: { key: string; value: string }[]
): Record<string, string> | undefined {
  const obj: Record<string, string> = {}
  for (const entry of entries) {
    if (entry.key.trim()) obj[entry.key.trim()] = entry.value
  }
  return Object.keys(obj).length > 0 ? obj : undefined
}

export function buildHeadersObject(
  entries: { key: string; value: string }[]
): Record<string, string> | undefined {
  const obj: Record<string, string> = {}
  for (const entry of entries) {
    if (entry.key.trim()) obj[entry.key.trim()] = entry.value
  }
  return Object.keys(obj).length > 0 ? obj : undefined
}

export function parseKeyValueJson(json: string | undefined): { key: string; value: string }[] {
  try {
    const parsed = json ? JSON.parse(json) : {}
    const entries = Object.entries(parsed)
    return entries.length > 0
      ? entries.map(([key, value]) => ({ key, value: String(value) }))
      : [{ key: '', value: '' }]
  } catch {
    return [{ key: '', value: '' }]
  }
}
