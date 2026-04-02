<script setup lang="ts">
import type { McpPrompt } from '~/composables/useApi'

defineOptions({ name: 'McpPromptPanel' })

const props = defineProps<{
  connectionId: string
  active: boolean
}>()

const api = useApi()
const { addRpcEntry } = useMcpRpcHistory()

const prompts = ref<McpPrompt[]>([])
const selectedPrompt = ref<McpPrompt | null>(null)
const promptFormArgs = ref<Record<string, string>>({})
const promptResult = ref<Record<string, unknown> | null>(null)
const loading = ref(false)
const getting = ref(false)
const showRaw = ref(false)

async function fetchPrompts() {
  loading.value = true
  selectedPrompt.value = null
  promptResult.value = null
  try {
    const result = await api.mcpListPrompts(props.connectionId)
    prompts.value = result.prompts || []
    addRpcEntry('prompts/list', { connectionId: props.connectionId }, result, false)
  } catch (err: unknown) {
    prompts.value = []
    const msg = err instanceof Error ? err.message : String(err)
    addRpcEntry('prompts/list', { connectionId: props.connectionId }, { error: msg }, true)
  } finally {
    loading.value = false
  }
}

watch([() => props.connectionId, () => props.active], ([, isActive]) => {
  if (isActive && props.connectionId && prompts.value.length === 0) {
    fetchPrompts()
  }
}, { immediate: true })

function selectPrompt(prompt: McpPrompt) {
  selectedPrompt.value = prompt
  promptResult.value = null
  showRaw.value = false
  const args: Record<string, string> = {}
  if (prompt.arguments) {
    for (const arg of prompt.arguments) {
      args[arg.name] = ''
    }
  }
  promptFormArgs.value = args
}

async function getPromptResult() {
  if (!selectedPrompt.value) return
  getting.value = true
  promptResult.value = null
  try {
    const req = { method: 'prompts/get', name: selectedPrompt.value.name, arguments: { ...promptFormArgs.value } }
    const result = await api.mcpGetPrompt(props.connectionId, selectedPrompt.value.name, { ...promptFormArgs.value })
    promptResult.value = result
    addRpcEntry('prompts/get', req, result, false)
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : 'Failed to get prompt'
    promptResult.value = { error: msg }
    addRpcEntry('prompts/get', { name: selectedPrompt.value?.name }, { error: msg }, true)
  } finally {
    getting.value = false
  }
}
</script>

<template>
  <div class="capability-content">
    <div class="cap-header">
      <span class="cap-count">{{ prompts.length }} prompt{{ prompts.length !== 1 ? 's' : '' }}</span>
      <UButton icon="i-lucide-refresh-cw" size="xs" variant="ghost" color="neutral" :loading="loading" @click="fetchPrompts" />
    </div>

    <div v-if="loading" class="cap-loading">
      <UIcon name="i-lucide-loader-2" class="size-5 animate-spin text-muted" />
    </div>

    <div v-else-if="prompts.length === 0" class="cap-empty">
      <p class="text-sm text-muted">This server does not expose any prompts.</p>
    </div>

    <template v-else>
      <div class="cap-list">
        <button
          v-for="prompt in prompts"
          :key="prompt.name"
          class="cap-list-item"
          :class="{ selected: selectedPrompt?.name === prompt.name }"
          @click="selectPrompt(prompt)"
        >
          <div class="cap-item-name">
            <UIcon name="i-lucide-message-square" class="size-3.5 text-muted shrink-0" />
            <span class="font-semibold text-sm truncate">{{ prompt.name }}</span>
            <span v-if="prompt.arguments?.length" class="args-badge">
              {{ prompt.arguments.length }} arg{{ prompt.arguments.length !== 1 ? 's' : '' }}
            </span>
          </div>
          <p v-if="prompt.description" class="cap-item-desc">{{ prompt.description }}</p>
        </button>
      </div>

      <div v-if="selectedPrompt" class="cap-form">
        <h4 class="cap-form-title">
          <UIcon name="i-lucide-message-square" class="size-4" />
          {{ selectedPrompt.name }}
        </h4>

        <div v-if="selectedPrompt.arguments?.length" class="cap-fields">
          <div
            v-for="arg in selectedPrompt.arguments"
            :key="arg.name"
            class="cap-field"
          >
            <label class="cap-field-label">
              {{ arg.name }}
              <span v-if="arg.required" class="required-star">*</span>
            </label>
            <input
              type="text"
              v-model="promptFormArgs[arg.name]"
              :placeholder="arg.description || arg.name"
              class="cap-input"
            />
          </div>
        </div>
        <p v-else class="text-xs text-muted">This prompt takes no arguments.</p>

        <button class="call-button" :disabled="getting" @click="getPromptResult">
          <UIcon v-if="getting" name="i-lucide-loader-2" class="size-4 animate-spin" />
          <UIcon v-else name="i-lucide-send" class="size-4" />
          {{ getting ? 'Getting...' : 'Get Prompt' }}
        </button>
      </div>

      <div v-if="promptResult" class="cap-result" :class="{ 'cap-result-error': promptResult.error }">
        <div class="cap-result-header">
          <span class="text-xs font-semibold uppercase" :class="promptResult.error ? 'text-red-500' : 'text-green-600 dark:text-green-400'">
            {{ promptResult.error ? 'Error' : 'Prompt Messages' }}
          </span>
          <button v-if="!promptResult.error" class="raw-toggle" @click="showRaw = !showRaw">
            {{ showRaw ? 'Formatted' : 'Raw' }}
          </button>
        </div>

        <div v-if="promptResult.error" class="text-sm text-red-500 p-3">{{ promptResult.error as string }}</div>

        <template v-else-if="showRaw">
          <pre class="cap-code">{{ JSON.stringify(promptResult, null, 2) }}</pre>
        </template>

        <template v-else>
          <div v-if="(promptResult as any).messages" class="prompt-messages">
            <div v-for="(msg, idx) in (promptResult as any).messages" :key="idx" class="prompt-message">
              <span class="role-badge" :class="`role-${msg.role}`">{{ msg.role }}</span>
              <pre class="cap-code prompt-content">{{ typeof msg.content === 'string' ? msg.content : (msg.content?.text || JSON.stringify(msg.content, null, 2)) }}</pre>
            </div>
          </div>
          <pre v-else class="cap-code">{{ JSON.stringify(promptResult, null, 2) }}</pre>
        </template>
      </div>
    </template>
  </div>
</template>
