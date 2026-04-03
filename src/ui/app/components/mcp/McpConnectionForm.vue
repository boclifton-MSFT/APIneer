<script setup lang="ts">
import type { McpServerConfig } from '~/composables/useApi'
import type { ConnectionState, McpFormData, OAuthUIState } from '~/composables/useMcpHelpers'
import { parseKeyValueJson } from '~/composables/useMcpHelpers'

defineOptions({ name: 'McpConnectionForm' })

const props = defineProps<{
  server: McpServerConfig
  connectionState: ConnectionState
  connectionId: string | null
  oauthState: OAuthUIState
}>()

const emit = defineEmits<{
  connect: [formData: McpFormData]
  disconnect: []
  save: [formData: McpFormData]
  startOAuth: [clientId: string, scopes: string]
  cancelOAuth: []
  revokeOAuth: []
}>()

const form = reactive<McpFormData>({
  name: '',
  transportType: 'stdio',
  command: '',
  args: '',
  url: '',
  envVars: [{ key: '', value: '' }],
  customHeaders: [{ key: '', value: '' }],
  authMethod: 'none',
  oauthClientId: '',
  oauthScopes: 'repo'
})

watch(() => props.server, (server) => {
  form.name = server.name
  form.transportType = server.transportType
  form.command = server.command || ''
  form.args = server.args || ''
  form.url = server.url || ''
  form.envVars = parseKeyValueJson(server.environmentVariables)
  form.customHeaders = parseKeyValueJson(server.headers)
  form.authMethod = 'none'
  form.oauthClientId = ''
  form.oauthScopes = 'repo'
}, { immediate: true })

const statusDot = computed(() => {
  switch (props.connectionState) {
    case 'connected': return { color: 'bg-green-500', label: 'Connected' }
    case 'connecting': return { color: 'bg-yellow-500 animate-pulse', label: 'Connecting...' }
    default: return { color: 'bg-red-500', label: 'Disconnected' }
  }
})

const canConnect = computed(() =>
  props.connectionState === 'disconnected' && (
    (form.transportType === 'stdio' && !!form.command) ||
    (form.transportType === 'streamable-http' && !!form.url)
  )
)

// Countdown clock for OAuth pending state
const now = ref(Date.now())
let clockTimer: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  clockTimer = setInterval(() => { now.value = Date.now() }, 1000)
})

onUnmounted(() => {
  if (clockTimer) clearInterval(clockTimer)
})

const timeRemaining = computed(() => {
  const { expiresAt, status } = props.oauthState
  if (status !== 'pending' || !expiresAt) return ''
  const ms = expiresAt - now.value
  if (ms <= 0) return 'expired'
  const mins = Math.floor(ms / 60000)
  const secs = Math.floor((ms % 60000) / 1000)
  return `${mins}m ${secs}s`
})

const copied = ref(false)
function copyUserCode() {
  const code = props.oauthState.userCode
  if (!code) return
  navigator.clipboard.writeText(code).then(() => {
    copied.value = true
    setTimeout(() => { copied.value = false }, 2000)
  })
}

function openVerificationUrl() {
  if (props.oauthState.verificationUri) {
    window.open(props.oauthState.verificationUri, '_blank')
  }
}

const oauthErrorMessage = computed(() => {
  const s = props.oauthState
  if (s.errorMessage) return s.errorMessage
  if (s.status === 'expired') return 'Authorization window expired. Please try again.'
  if (s.status === 'denied') return 'Authorization was denied.'
  return 'An error occurred. Please try again.'
})
</script>

<template>
  <div class="space-y-4">
    <!-- Status indicator -->
    <div class="flex items-center gap-2">
      <div class="size-2.5 rounded-full" :class="statusDot.color" />
      <span class="text-sm font-medium" :class="{
        'text-green-600 dark:text-green-400': connectionState === 'connected',
        'text-yellow-600 dark:text-yellow-400': connectionState === 'connecting',
        'text-red-600 dark:text-red-400': connectionState === 'disconnected'
      }">{{ statusDot.label }}</span>
    </div>

    <!-- Server name -->
    <UFormField label="Server Name">
      <UInput v-model="form.name" placeholder="My MCP Server" />
    </UFormField>

    <!-- Transport type -->
    <UFormField label="Transport Type">
      <div class="flex gap-3">
        <label class="transport-option" :class="{ active: form.transportType === 'stdio' }">
          <input
            type="radio"
            name="transportType"
            value="stdio"
            :checked="form.transportType === 'stdio'"
            class="sr-only"
            @change="form.transportType = 'stdio'"
          >
          <UIcon name="i-lucide-terminal" class="size-4" />
          <span>stdio</span>
        </label>
        <label class="transport-option" :class="{ active: form.transportType === 'streamable-http' }">
          <input
            type="radio"
            name="transportType"
            value="streamable-http"
            :checked="form.transportType === 'streamable-http'"
            class="sr-only"
            @change="form.transportType = 'streamable-http'"
          >
          <UIcon name="i-lucide-globe" class="size-4" />
          <span>Streamable HTTP</span>
        </label>
      </div>
    </UFormField>

    <!-- stdio fields -->
    <template v-if="form.transportType === 'stdio'">
      <UFormField label="Command">
        <UInput v-model="form.command" placeholder="npx @modelcontextprotocol/server-sqlite" />
      </UFormField>
      <UFormField label="Arguments">
        <UInput v-model="form.args" placeholder="db.sqlite" />
      </UFormField>

      <div class="env-vars-section">
        <label class="text-sm font-medium text-highlighted block mb-2">Environment Variables</label>
        <KeyValueEditor
          v-model="form.envVars"
          key-placeholder="KEY"
          value-placeholder="value"
        />
      </div>
    </template>

    <!-- HTTP fields -->
    <template v-if="form.transportType === 'streamable-http'">
      <UFormField label="URL">
        <UInput v-model="form.url" placeholder="http://localhost:3000/mcp" />
      </UFormField>

      <!-- Auth Method selector -->
      <UFormField label="Auth Method">
        <div class="flex gap-3">
          <label class="transport-option" :class="{ active: form.authMethod === 'none' }">
            <input
              type="radio"
              name="authMethod"
              value="none"
              :checked="form.authMethod === 'none'"
              class="sr-only"
              @change="form.authMethod = 'none'"
            >
            <span>None</span>
          </label>
          <label class="transport-option" :class="{ active: form.authMethod === 'bearer-token' }">
            <input
              type="radio"
              name="authMethod"
              value="bearer-token"
              :checked="form.authMethod === 'bearer-token'"
              class="sr-only"
              @change="form.authMethod = 'bearer-token'"
            >
            <UIcon name="i-lucide-key" class="size-4" />
            <span>Bearer Token</span>
          </label>
          <label class="transport-option" :class="{ active: form.authMethod === 'github-oauth' }">
            <input
              type="radio"
              name="authMethod"
              value="github-oauth"
              :checked="form.authMethod === 'github-oauth'"
              class="sr-only"
              @change="form.authMethod = 'github-oauth'"
            >
            <UIcon name="i-lucide-github" class="size-4" />
            <span>GitHub OAuth</span>
          </label>
        </div>
      </UFormField>

      <!-- Bearer Token: show custom headers table -->
      <div v-if="form.authMethod === 'bearer-token'" class="env-vars-section">
        <label class="text-sm font-medium text-highlighted block mb-1">Custom Headers</label>
        <p class="text-xs text-muted mb-2">Add authentication headers (e.g., Authorization: Bearer &lt;token&gt;)</p>
        <KeyValueEditor
          v-model="form.customHeaders"
          key-placeholder="Authorization"
          value-placeholder="Bearer <token>"
        />
      </div>

      <!-- None: show subtle custom headers section for advanced use -->
      <div v-else-if="form.authMethod === 'none'" class="env-vars-section">
        <label class="text-sm font-medium text-highlighted block mb-1">Custom Headers</label>
        <p class="text-xs text-muted mb-2">Add authentication headers (e.g., Authorization: Bearer &lt;token&gt;)</p>
        <KeyValueEditor
          v-model="form.customHeaders"
          key-placeholder="Authorization"
          value-placeholder="Bearer <token>"
        />
      </div>

      <!-- GitHub OAuth UI -->
      <div v-else-if="form.authMethod === 'github-oauth'" class="oauth-panel rounded-lg border border-default bg-muted/30 p-4 space-y-3">

        <!-- Idle: login form -->
        <template v-if="oauthState.status === 'idle'">
          <div class="flex items-center gap-2 mb-1">
            <UIcon name="i-lucide-github" class="size-5 text-highlighted" />
            <span class="text-sm font-medium text-highlighted">Login with GitHub</span>
          </div>
          <UFormField label="Client ID">
            <UInput
              v-model="form.oauthClientId"
              placeholder="your-github-oauth-app-client-id"
            />
          </UFormField>
          <UFormField label="Scopes">
            <UInput
              v-model="form.oauthScopes"
              placeholder="repo,read:org"
            />
          </UFormField>
          <UButton
            icon="i-lucide-github"
            label="Login with GitHub"
            :disabled="!form.oauthClientId.trim()"
            @click="emit('startOAuth', form.oauthClientId, form.oauthScopes)"
          />
        </template>

        <!-- Pending: device code flow in progress -->
        <template v-else-if="oauthState.status === 'pending'">
          <div class="flex items-center gap-2 mb-1">
            <UIcon name="i-lucide-loader-2" class="size-4 animate-spin text-primary" />
            <span class="text-sm font-medium text-highlighted">Waiting for authorization...</span>
          </div>
          <p class="text-sm text-muted">
            Enter this code at
            <a
              :href="oauthState.verificationUri"
              target="_blank"
              class="text-primary underline"
            >github.com/login/device</a>
          </p>

          <!-- User code display -->
          <div class="flex items-center gap-2">
            <div class="font-mono text-2xl font-bold tracking-widest bg-default border border-default rounded-md px-4 py-2 select-all">
              {{ oauthState.userCode }}
            </div>
            <UButton
              :icon="copied ? 'i-lucide-check' : 'i-lucide-copy'"
              :label="copied ? 'Copied!' : 'Copy'"
              size="sm"
              variant="soft"
              color="neutral"
              @click="copyUserCode"
            />
          </div>

          <!-- Actions -->
          <div class="flex items-center gap-2">
            <UButton
              icon="i-lucide-external-link"
              label="Open GitHub"
              @click="openVerificationUrl"
            />
            <UButton
              variant="ghost"
              color="neutral"
              label="Cancel"
              @click="emit('cancelOAuth')"
            />
          </div>

          <!-- Countdown -->
          <p v-if="timeRemaining" class="text-xs text-muted">
            Expires in {{ timeRemaining }}
          </p>
        </template>

        <!-- Complete: authenticated -->
        <template v-else-if="oauthState.status === 'complete'">
          <div class="flex items-center gap-2">
            <UIcon name="i-lucide-check-circle" class="size-5 text-green-500" />
            <span class="text-sm font-semibold text-green-600 dark:text-green-400">Authenticated with GitHub</span>
          </div>
          <p v-if="form.oauthScopes" class="text-xs text-muted">
            Scopes: {{ form.oauthScopes }}
          </p>
          <UButton
            icon="i-lucide-log-out"
            label="Logout"
            size="sm"
            variant="soft"
            color="error"
            @click="emit('revokeOAuth')"
          />
        </template>

        <!-- Error / Expired / Denied -->
        <template v-else>
          <div class="flex items-center gap-2">
            <UIcon name="i-lucide-alert-circle" class="size-5 text-red-500" />
            <span class="text-sm text-red-600 dark:text-red-400">{{ oauthErrorMessage }}</span>
          </div>
          <UButton
            icon="i-lucide-refresh-cw"
            label="Try Again"
            size="sm"
            variant="soft"
            @click="emit('cancelOAuth')"
          />
        </template>
      </div>
    </template>

    <!-- Action buttons -->
    <div class="flex items-center gap-2 pt-2">
      <button
        v-if="connectionState === 'disconnected'"
        class="connect-button"
        :disabled="!canConnect"
        @click="emit('connect', { ...form, envVars: [...form.envVars], customHeaders: [...form.customHeaders] })"
      >
        <UIcon name="i-lucide-plug" class="size-4" />
        Connect
      </button>
      <button
        v-else-if="connectionState === 'connecting'"
        class="connect-button connecting"
        disabled
      >
        <UIcon name="i-lucide-loader-2" class="size-4 animate-spin" />
        Connecting...
      </button>
      <button
        v-else
        class="disconnect-button"
        @click="emit('disconnect')"
      >
        <UIcon name="i-lucide-unplug" class="size-4" />
        Disconnect
      </button>

      <UButton
        label="Save"
        icon="i-lucide-save"
        size="sm"
        variant="soft"
        color="neutral"
        :disabled="!form.name.trim()"
        @click="emit('save', { ...form, envVars: [...form.envVars], customHeaders: [...form.customHeaders] })"
      />
    </div>
  </div>
</template>
