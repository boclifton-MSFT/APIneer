<script setup lang="ts">
import type { McpServerConfig } from '~/composables/useApi'
import type { ConnectionState, McpFormData } from '~/composables/useMcpHelpers'
import { parseKeyValueJson } from '~/composables/useMcpHelpers'

defineOptions({ name: 'McpConnectionForm' })

const props = defineProps<{
  server: McpServerConfig
  connectionState: ConnectionState
  connectionId: string | null
}>()

const emit = defineEmits<{
  connect: [formData: McpFormData]
  disconnect: []
  save: [formData: McpFormData]
}>()

const form = reactive<McpFormData>({
  name: '',
  transportType: 'stdio',
  command: '',
  args: '',
  url: '',
  envVars: [{ key: '', value: '' }],
  customHeaders: [{ key: '', value: '' }]
})

watch(() => props.server, (server) => {
  form.name = server.name
  form.transportType = server.transportType
  form.command = server.command || ''
  form.args = server.args || ''
  form.url = server.url || ''
  form.envVars = parseKeyValueJson(server.environmentVariables)
  form.customHeaders = parseKeyValueJson(server.headers)
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

      <div class="env-vars-section">
        <label class="text-sm font-medium text-highlighted block mb-1">Custom Headers</label>
        <p class="text-xs text-muted mb-2">Add authentication headers (e.g., Authorization: Bearer &lt;token&gt;)</p>
        <KeyValueEditor
          v-model="form.customHeaders"
          key-placeholder="Authorization"
          value-placeholder="Bearer <token>"
        />
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
