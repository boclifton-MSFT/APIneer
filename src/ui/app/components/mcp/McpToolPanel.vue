<script setup lang="ts">
import type { McpTool } from '~/composables/useApi'

defineOptions({ name: 'McpToolPanel' })

const props = defineProps<{
  connectionId: string
  active: boolean
}>()

const api = useApi()
const { addRpcEntry } = useMcpRpcHistory()

const tools = ref<McpTool[]>([])
const selectedTool = ref<McpTool | null>(null)
const toolArgs = ref<Record<string, any>>({})
const toolResult = ref<Record<string, any> | null>(null)
const loading = ref(false)
const calling = ref(false)
const showRaw = ref(false)

async function fetchTools() {
  loading.value = true
  selectedTool.value = null
  toolResult.value = null
  try {
    const result = await api.mcpListTools(props.connectionId)
    tools.value = result.tools || []
    addRpcEntry('tools/list', { connectionId: props.connectionId }, result, false)
  } catch (err: unknown) {
    tools.value = []
    const msg = err instanceof Error ? err.message : String(err)
    addRpcEntry('tools/list', { connectionId: props.connectionId }, { error: msg }, true)
  } finally {
    loading.value = false
  }
}

// Fetch when panel becomes active for the first time or when connectionId changes
watch([() => props.connectionId, () => props.active], ([, isActive]) => {
  if (isActive && props.connectionId && tools.value.length === 0) {
    fetchTools()
  }
}, { immediate: true })

function selectTool(tool: McpTool) {
  selectedTool.value = tool
  toolResult.value = null
  showRaw.value = false
  const args: Record<string, any> = {}
  if (tool.inputSchema?.properties) {
    for (const [key, schema] of Object.entries(tool.inputSchema.properties) as [string, any][]) {
      args[key] = schema.type === 'boolean' ? false : ''
    }
  }
  toolArgs.value = args
}

async function callTool() {
  if (!selectedTool.value) return
  calling.value = true
  toolResult.value = null
  try {
    const processedArgs: Record<string, any> = {}
    if (selectedTool.value.inputSchema?.properties) {
      for (const [key, schema] of Object.entries(selectedTool.value.inputSchema.properties) as [string, any][]) {
        const val = toolArgs.value[key]
        if (val === '' || val === undefined || val === null) continue
        if (schema.type === 'number' || schema.type === 'integer') {
          processedArgs[key] = Number(val)
        } else if (schema.type === 'boolean') {
          processedArgs[key] = Boolean(val)
        } else if (schema.type === 'object' || schema.type === 'array') {
          try { processedArgs[key] = JSON.parse(String(val)) } catch { processedArgs[key] = val }
        } else {
          processedArgs[key] = val
        }
      }
    }

    const req = { method: 'tools/call', name: selectedTool.value.name, arguments: processedArgs }
    const result = await api.mcpCallTool(props.connectionId, selectedTool.value.name, processedArgs)
    toolResult.value = result
    addRpcEntry('tools/call', req, result, result?.isError === true)
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : 'Tool call failed'
    toolResult.value = { isError: true, content: [{ type: 'text', text: msg }] }
    addRpcEntry('tools/call', { name: selectedTool.value?.name }, { error: msg }, true)
  } finally {
    calling.value = false
  }
}

function isRequired(tool: McpTool, fieldName: string): boolean {
  return Array.isArray(tool?.inputSchema?.required) && tool.inputSchema.required.includes(fieldName)
}
</script>

<template>
  <div class="capability-content">
    <div class="cap-header">
      <span class="cap-count">{{ tools.length }} tool{{ tools.length !== 1 ? 's' : '' }}</span>
      <UButton icon="i-lucide-refresh-cw" size="xs" variant="ghost" color="neutral" :loading="loading" @click="fetchTools" />
    </div>

    <div v-if="loading" class="cap-loading">
      <UIcon name="i-lucide-loader-2" class="size-5 animate-spin text-muted" />
    </div>

    <div v-else-if="tools.length === 0" class="cap-empty">
      <p class="text-sm text-muted">This server does not expose any tools.</p>
    </div>

    <template v-else>
      <div class="cap-list">
        <button
          v-for="tool in tools"
          :key="tool.name"
          class="cap-list-item"
          :class="{ selected: selectedTool?.name === tool.name }"
          @click="selectTool(tool)"
        >
          <div class="cap-item-name">
            <UIcon name="i-lucide-wrench" class="size-3.5 text-muted shrink-0" />
            <span class="font-semibold text-sm truncate">{{ tool.name }}</span>
          </div>
          <p v-if="tool.description" class="cap-item-desc">{{ tool.description }}</p>
        </button>
      </div>

      <div v-if="selectedTool" class="cap-form">
        <h4 class="cap-form-title">
          <UIcon name="i-lucide-terminal" class="size-4" />
          {{ selectedTool.name }}
        </h4>

        <div v-if="selectedTool.inputSchema?.properties && Object.keys(selectedTool.inputSchema.properties).length > 0" class="cap-fields">
          <div
            v-for="(schema, key) in (selectedTool.inputSchema.properties as Record<string, any>)"
            :key="key"
            class="cap-field"
          >
            <label class="cap-field-label">
              {{ key }}
              <span v-if="isRequired(selectedTool, key as string)" class="required-star">*</span>
            </label>
            <label v-if="schema.type === 'boolean'" class="cap-checkbox">
              <input type="checkbox" v-model="toolArgs[key as string]" />
              <span class="text-sm text-muted">{{ schema.description || 'Toggle' }}</span>
            </label>
            <input
              v-else-if="schema.type === 'number' || schema.type === 'integer'"
              type="number"
              v-model="toolArgs[key as string]"
              :placeholder="schema.description || key as string"
              class="cap-input"
            />
            <textarea
              v-else-if="schema.type === 'object' || schema.type === 'array'"
              v-model="toolArgs[key as string]"
              :placeholder="schema.description || `Enter JSON ${schema.type}`"
              class="cap-input cap-textarea"
              rows="3"
            />
            <input
              v-else
              type="text"
              v-model="toolArgs[key as string]"
              :placeholder="schema.description || key as string"
              class="cap-input"
            />
          </div>
        </div>
        <p v-else class="text-xs text-muted">This tool takes no arguments.</p>

        <button class="call-button" :disabled="calling" @click="callTool">
          <UIcon v-if="calling" name="i-lucide-loader-2" class="size-4 animate-spin" />
          <UIcon v-else name="i-lucide-play" class="size-4" />
          {{ calling ? 'Calling...' : 'Call Tool' }}
        </button>
      </div>

      <div v-if="toolResult" class="cap-result" :class="{ 'cap-result-error': toolResult.isError }">
        <div class="cap-result-header">
          <span class="text-xs font-semibold uppercase" :class="toolResult.isError ? 'text-red-500' : 'text-green-600 dark:text-green-400'">
            {{ toolResult.isError ? 'Error' : 'Result' }}
          </span>
          <button class="raw-toggle" @click="showRaw = !showRaw">
            {{ showRaw ? 'Formatted' : 'Raw' }}
          </button>
        </div>

        <pre v-if="showRaw" class="cap-code">{{ JSON.stringify(toolResult, null, 2) }}</pre>

        <template v-else>
          <template v-if="toolResult.content">
            <div v-for="(item, idx) in (toolResult.content as any[])" :key="idx" class="cap-result-item">
              <pre v-if="item.type === 'text'" class="cap-code">{{ item.text }}</pre>
              <img v-else-if="item.type === 'image'" :src="`data:${item.mimeType || 'image/png'};base64,${item.data}`" class="cap-image" />
              <div v-else-if="item.type === 'resource'" class="cap-resource-result">
                <span class="text-xs text-muted">{{ item.resource?.uri }}</span>
                <pre class="cap-code">{{ typeof item.resource?.text === 'string' ? item.resource.text : JSON.stringify(item.resource, null, 2) }}</pre>
              </div>
              <pre v-else class="cap-code">{{ JSON.stringify(item, null, 2) }}</pre>
            </div>
          </template>
          <pre v-else class="cap-code">{{ JSON.stringify(toolResult, null, 2) }}</pre>
        </template>
      </div>
    </template>
  </div>
</template>
