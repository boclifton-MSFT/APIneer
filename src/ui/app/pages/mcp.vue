<script setup lang="ts">
import type { McpServerConfig } from '~/composables/useApi'
import type { McpFormData, ConnectionState, OAuthUIState } from '~/composables/useMcpHelpers'
import { buildEnvObject, buildHeadersObject } from '~/composables/useMcpHelpers'

definePageMeta({
  layout: 'dashboard'
})

const api = useApi()
const toast = useToast()
const { clearRpcHistory } = useMcpRpcHistory()

const servers = ref<McpServerConfig[]>([])
const loading = ref(false)
const selectedServerId = ref<string | null>(null)
const connectionState = ref<ConnectionState>('disconnected')
const connectionId = ref<string | null>(null)
const showNewDialog = ref(false)
const activeTab = ref('tools')

// OAuth flow state
const oauthState = ref<OAuthUIState>({ status: 'idle' })
const oauthFlowId = ref<string | null>(null)
let oauthPollTimer: ReturnType<typeof setInterval> | null = null

const tabs = [
  { label: 'Tools', value: 'tools', icon: 'i-lucide-wrench' },
  { label: 'Resources', value: 'resources', icon: 'i-lucide-file-text' },
  { label: 'Prompts', value: 'prompts', icon: 'i-lucide-message-square' }
]

const newForm = reactive({
  name: '',
  transportType: 'stdio' as 'stdio' | 'streamable-http',
  command: '',
  args: '',
  url: ''
})

const selectedServer = computed(() =>
  servers.value.find(s => s.id === selectedServerId.value) ?? null
)

async function loadServers() {
  loading.value = true
  try {
    servers.value = await api.getServerConfigs()
  } catch {
    servers.value = []
  } finally {
    loading.value = false
  }
}

function stopOAuthPolling() {
  if (oauthPollTimer) {
    clearInterval(oauthPollTimer)
    oauthPollTimer = null
  }
}

function selectServer(id: string) {
  stopOAuthPolling()
  oauthFlowId.value = null
  oauthState.value = { status: 'idle' }
  selectedServerId.value = id
  connectionState.value = 'disconnected'
  connectionId.value = null
}

async function handleSave(formData: McpFormData) {
  if (!selectedServerId.value || !formData.name.trim()) return
  try {
    const envObj = buildEnvObject(formData.envVars)
    const headersObj = formData.authMethod !== 'github-oauth'
      ? buildHeadersObject(formData.customHeaders)
      : undefined
    await api.updateServerConfig(selectedServerId.value, {
      name: formData.name.trim(),
      transportType: formData.transportType,
      command: formData.transportType === 'stdio' ? formData.command : undefined,
      args: formData.transportType === 'stdio' ? formData.args : undefined,
      url: formData.transportType === 'streamable-http' ? formData.url : undefined,
      environmentVariables: envObj ? JSON.stringify(envObj) : undefined,
      headers: formData.transportType === 'streamable-http' && headersObj ? JSON.stringify(headersObj) : undefined
    })
    await loadServers()
    toast.add({ title: 'Server config updated', color: 'success', icon: 'i-lucide-check' })
  } catch {
    toast.add({ title: 'Failed to update config', color: 'error' })
  }
}

async function handleConnect(formData: McpFormData) {
  if (!selectedServerId.value) return
  connectionState.value = 'connecting'
  try {
    const env = buildEnvObject(formData.envVars)
    // GitHub OAuth: backend auto-injects the stored token; don't pass custom headers
    const headers = formData.transportType === 'streamable-http' && formData.authMethod !== 'github-oauth'
      ? buildHeadersObject(formData.customHeaders)
      : undefined
    const result = await api.mcpConnect({
      serverId: selectedServerId.value,
      transportType: formData.transportType,
      command: formData.transportType === 'stdio' ? formData.command : undefined,
      args: formData.transportType === 'stdio' ? formData.args : undefined,
      url: formData.transportType === 'streamable-http' ? formData.url : undefined,
      env,
      headers
    })
    connectionId.value = result.connectionId
    connectionState.value = 'connected'
    clearRpcHistory()
    toast.add({ title: 'Connected to MCP server', color: 'success', icon: 'i-lucide-check' })
  } catch (err: unknown) {
    connectionState.value = 'disconnected'
    const e = err as { data?: { message?: string }; message?: string }
    toast.add({
      title: 'Connection failed',
      description: e?.data?.message || e?.message || 'Could not connect to MCP server',
      color: 'error'
    })
  }
}

async function handleDisconnect() {
  if (!connectionId.value) return
  try {
    await api.mcpDisconnect(connectionId.value)
  } catch {
    // Best-effort
  } finally {
    connectionState.value = 'disconnected'
    connectionId.value = null
  }
}

async function handleStartOAuth(clientId: string, scopes: string) {
  if (!selectedServerId.value) return
  try {
    const result = await api.startMcpOAuth(selectedServerId.value, clientId, scopes)
    oauthFlowId.value = result.flowId
    oauthState.value = {
      status: 'pending',
      userCode: result.userCode,
      verificationUri: result.verificationUri,
      expiresAt: Date.now() + result.expiresIn * 1000
    }
    oauthPollTimer = setInterval(async () => {
      if (!oauthFlowId.value) return
      try {
        const status = await api.pollMcpOAuth(oauthFlowId.value)
        if (status.status === 'complete') {
          stopOAuthPolling()
          oauthState.value = { status: 'complete' }
          toast.add({ title: 'Authenticated with GitHub', color: 'success', icon: 'i-lucide-check' })
        } else if (status.status === 'expired') {
          stopOAuthPolling()
          oauthState.value = { status: 'expired', errorMessage: 'Authorization window expired. Please try again.' }
        } else if (status.status === 'denied') {
          stopOAuthPolling()
          oauthState.value = { status: 'denied', errorMessage: 'Authorization was denied.' }
        }
        // 'pending' — keep polling
      } catch {
        // Polling errors are transient — keep polling
      }
    }, 5000)
  } catch (err: unknown) {
    const e = err as { data?: { message?: string }; message?: string }
    oauthState.value = {
      status: 'error',
      errorMessage: e?.data?.message || e?.message || 'Failed to start OAuth flow'
    }
  }
}

function handleCancelOAuth() {
  stopOAuthPolling()
  oauthFlowId.value = null
  oauthState.value = { status: 'idle' }
}

async function handleRevokeOAuth() {
  if (!selectedServerId.value) return
  try {
    await api.revokeMcpOAuth(selectedServerId.value)
    oauthState.value = { status: 'idle' }
    toast.add({ title: 'GitHub authentication revoked', color: 'success' })
  } catch {
    toast.add({ title: 'Failed to revoke token', color: 'error' })
  }
}

async function deleteServer(id: string) {
  try {
    await api.deleteServerConfig(id)
    if (selectedServerId.value === id) {
      stopOAuthPolling()
      oauthFlowId.value = null
      oauthState.value = { status: 'idle' }
      selectedServerId.value = null
      connectionState.value = 'disconnected'
      connectionId.value = null
    }
    await loadServers()
    toast.add({ title: 'Server deleted', color: 'success' })
  } catch {
    toast.add({ title: 'Failed to delete server', color: 'error' })
  }
}

function openNewDialog() {
  newForm.name = ''
  newForm.transportType = 'stdio'
  newForm.command = ''
  newForm.args = ''
  newForm.url = ''
  showNewDialog.value = true
}

async function createServer() {
  if (!newForm.name.trim()) return
  try {
    const newServer = await api.createServerConfig({
      name: newForm.name.trim(),
      transportType: newForm.transportType,
      command: newForm.transportType === 'stdio' ? newForm.command : undefined,
      args: newForm.transportType === 'stdio' ? newForm.args : undefined,
      url: newForm.transportType === 'streamable-http' ? newForm.url : undefined
    })
    showNewDialog.value = false
    await loadServers()
    selectServer(newServer.id)
    toast.add({ title: 'Server config saved', color: 'success', icon: 'i-lucide-check' })
  } catch {
    toast.add({ title: 'Failed to save server config', color: 'error' })
  }
}

onMounted(() => loadServers())
onUnmounted(() => stopOAuthPolling())
</script>

<template>
  <!-- Left panel — Server List -->
  <McpServerList
    :servers="servers"
    :loading="loading"
    :selected-server-id="selectedServerId"
    :connection-state="connectionState"
    @select="selectServer"
    @delete="deleteServer"
    @add="openNewDialog"
  />

  <!-- Right panel — Selected Server -->
  <UDashboardPanel id="mcp-server-detail" class="hidden lg:flex">
    <template #header>
      <UDashboardNavbar :title="selectedServer?.name || 'MCP Server'" />
    </template>

    <template #body>
      <!-- No server selected -->
      <div v-if="!selectedServer" class="flex flex-col items-center justify-center h-full gap-4">
        <div class="flex items-center justify-center size-16 rounded-xl bg-primary/10">
          <UIcon name="i-lucide-plug" class="size-8 text-primary" />
        </div>
        <div class="text-center">
          <h2 class="text-lg font-semibold text-highlighted">Select an MCP server</h2>
          <p class="text-sm text-muted mt-1">Choose a server from the sidebar, or add a new one.</p>
        </div>
        <UButton label="New Server" icon="i-lucide-plus" size="lg" @click="openNewDialog" />
      </div>

      <!-- Server selected -->
      <div v-else class="flex flex-col gap-6 p-4 mcp-detail">
        <!-- Connection Form -->
        <McpConnectionForm
          :server="selectedServer"
          :connection-state="connectionState"
          :connection-id="connectionId"
          :oauth-state="oauthState"
          @connect="handleConnect"
          @disconnect="handleDisconnect"
          @save="handleSave"
          @start-o-auth="handleStartOAuth"
          @cancel-o-auth="handleCancelOAuth"
          @revoke-o-auth="handleRevokeOAuth"
        />

        <USeparator />

        <!-- Capability Tabs -->
        <div class="capability-tabs">
          <div class="tab-header flex border-b border-default">
            <button
              v-for="tab in tabs"
              :key="tab.value"
              class="tab-button"
              :class="{ active: activeTab === tab.value }"
              @click="activeTab = tab.value"
            >
              <UIcon :name="tab.icon" class="size-4" />
              {{ tab.label }}
            </button>
          </div>

          <div class="tab-content">
            <!-- Not connected state -->
            <div v-if="connectionState !== 'connected'" class="flex flex-col items-center justify-center gap-3 py-8">
              <UIcon name="i-lucide-plug-zap" class="size-8 text-muted" />
              <p class="text-sm text-muted text-center">
                Connect to an MCP server to browse its capabilities.
              </p>
            </div>

            <!-- Panels: all mounted when connected, v-show for state persistence across tab switches -->
            <template v-else>
              <McpToolPanel
                v-show="activeTab === 'tools'"
                :connection-id="connectionId!"
                :active="activeTab === 'tools'"
              />
              <McpResourcePanel
                v-show="activeTab === 'resources'"
                :connection-id="connectionId!"
                :active="activeTab === 'resources'"
              />
              <McpPromptPanel
                v-show="activeTab === 'prompts'"
                :connection-id="connectionId!"
                :active="activeTab === 'prompts'"
              />
            </template>
          </div>

          <McpRpcHistory v-if="connectionState === 'connected'" />
        </div>
      </div>
    </template>
  </UDashboardPanel>

  <!-- New Server Modal -->
  <UModal v-model:open="showNewDialog" title="New MCP Server" description="Add a new MCP server configuration.">
    <template #body>
      <div class="space-y-4">
        <UFormField label="Server Name" required>
          <UInput
            v-model="newForm.name"
            placeholder="My MCP Server"
            autofocus
            @keydown.enter="createServer"
          />
        </UFormField>

        <UFormField label="Transport Type">
          <div class="flex gap-3">
            <label class="transport-option" :class="{ active: newForm.transportType === 'stdio' }">
              <input
                type="radio"
                name="newTransportType"
                value="stdio"
                :checked="newForm.transportType === 'stdio'"
                class="sr-only"
                @change="newForm.transportType = 'stdio'"
              >
              <UIcon name="i-lucide-terminal" class="size-4" />
              <span>stdio</span>
            </label>
            <label class="transport-option" :class="{ active: newForm.transportType === 'streamable-http' }">
              <input
                type="radio"
                name="newTransportType"
                value="streamable-http"
                :checked="newForm.transportType === 'streamable-http'"
                class="sr-only"
                @change="newForm.transportType = 'streamable-http'"
              >
              <UIcon name="i-lucide-globe" class="size-4" />
              <span>Streamable HTTP</span>
            </label>
          </div>
        </UFormField>

        <template v-if="newForm.transportType === 'stdio'">
          <UFormField label="Command">
            <UInput v-model="newForm.command" placeholder="npx @modelcontextprotocol/server-sqlite" />
          </UFormField>
          <UFormField label="Arguments">
            <UInput v-model="newForm.args" placeholder="db.sqlite" />
          </UFormField>
        </template>

        <template v-if="newForm.transportType === 'streamable-http'">
          <UFormField label="URL">
            <UInput v-model="newForm.url" placeholder="http://localhost:3000/mcp" />
          </UFormField>
        </template>
      </div>
    </template>
    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton variant="ghost" color="neutral" label="Cancel" @click="showNewDialog = false" />
        <UButton label="Create" icon="i-lucide-plus" :disabled="!newForm.name.trim()" @click="createServer" />
      </div>
    </template>
  </UModal>
</template>
