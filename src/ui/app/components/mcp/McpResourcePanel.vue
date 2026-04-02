<script setup lang="ts">
import type { McpResource } from '~/composables/useApi'

defineOptions({ name: 'McpResourcePanel' })

const props = defineProps<{
  connectionId: string
  active: boolean
}>()

const api = useApi()
const { addRpcEntry } = useMcpRpcHistory()

const resources = ref<McpResource[]>([])
const resourceContent = ref<Record<string, unknown> | null>(null)
const loading = ref(false)
const readingResource = ref<string | null>(null)
const showRaw = ref(false)

async function fetchResources() {
  loading.value = true
  resourceContent.value = null
  try {
    const result = await api.mcpListResources(props.connectionId)
    resources.value = result.resources || []
    addRpcEntry('resources/list', { connectionId: props.connectionId }, result, false)
  } catch (err: unknown) {
    resources.value = []
    const msg = err instanceof Error ? err.message : String(err)
    addRpcEntry('resources/list', { connectionId: props.connectionId }, { error: msg }, true)
  } finally {
    loading.value = false
  }
}

watch([() => props.connectionId, () => props.active], ([, isActive]) => {
  if (isActive && props.connectionId && resources.value.length === 0) {
    fetchResources()
  }
}, { immediate: true })

async function readResource(resource: McpResource) {
  if (!props.connectionId) return
  readingResource.value = resource.uri
  resourceContent.value = null
  showRaw.value = false
  try {
    const result = await api.mcpReadResource(props.connectionId, resource.uri)
    resourceContent.value = { resource, result }
    addRpcEntry('resources/read', { uri: resource.uri }, result, false)
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : 'Failed to read resource'
    resourceContent.value = { resource, error: msg }
    addRpcEntry('resources/read', { uri: resource.uri }, { error: msg }, true)
  } finally {
    readingResource.value = null
  }
}
</script>

<template>
  <div class="capability-content">
    <div class="cap-header">
      <span class="cap-count">{{ resources.length }} resource{{ resources.length !== 1 ? 's' : '' }}</span>
      <UButton icon="i-lucide-refresh-cw" size="xs" variant="ghost" color="neutral" :loading="loading" @click="fetchResources" />
    </div>

    <div v-if="loading" class="cap-loading">
      <UIcon name="i-lucide-loader-2" class="size-5 animate-spin text-muted" />
    </div>

    <div v-else-if="resources.length === 0" class="cap-empty">
      <p class="text-sm text-muted">This server does not expose any resources.</p>
    </div>

    <template v-else>
      <div class="cap-list">
        <div
          v-for="resource in resources"
          :key="resource.uri"
          class="cap-list-item resource-item"
        >
          <div class="flex items-start justify-between gap-2">
            <div class="min-w-0 flex-1">
              <div class="cap-item-name">
                <UIcon name="i-lucide-file-text" class="size-3.5 text-muted shrink-0" />
                <span class="font-semibold text-sm truncate">{{ resource.name }}</span>
                <span v-if="resource.mimeType" class="mime-badge">{{ resource.mimeType }}</span>
              </div>
              <p class="cap-item-desc font-mono text-xs">{{ resource.uri }}</p>
              <p v-if="resource.description" class="cap-item-desc">{{ resource.description }}</p>
            </div>
            <button
              class="read-button"
              :disabled="readingResource === resource.uri"
              @click="readResource(resource)"
            >
              <UIcon v-if="readingResource === resource.uri" name="i-lucide-loader-2" class="size-3.5 animate-spin" />
              <UIcon v-else name="i-lucide-eye" class="size-3.5" />
              Read
            </button>
          </div>
        </div>
      </div>

      <div v-if="resourceContent" class="cap-result" :class="{ 'cap-result-error': resourceContent.error }">
        <div class="cap-result-header">
          <span class="text-xs font-semibold">
            {{ resourceContent.error ? 'Error' : (resourceContent.resource as McpResource)?.name || 'Resource' }}
          </span>
          <button v-if="!resourceContent.error" class="raw-toggle" @click="showRaw = !showRaw">
            {{ showRaw ? 'Formatted' : 'Raw' }}
          </button>
        </div>

        <div v-if="resourceContent.error" class="text-sm text-red-500 p-3">{{ resourceContent.error as string }}</div>

        <template v-else-if="showRaw">
          <pre class="cap-code">{{ JSON.stringify((resourceContent as any).result, null, 2) }}</pre>
        </template>

        <template v-else>
          <template v-if="(resourceContent as any).result?.contents">
            <div v-for="(content, idx) in (resourceContent as any).result.contents" :key="idx" class="cap-result-item">
              <img
                v-if="content.mimeType?.startsWith('image/')"
                :src="`data:${content.mimeType};base64,${content.blob}`"
                class="cap-image"
              />
              <pre v-else-if="content.mimeType === 'application/json'" class="cap-code">{{ typeof content.text === 'string' ? (() => { try { return JSON.stringify(JSON.parse(content.text), null, 2) } catch { return content.text } })() : JSON.stringify(content, null, 2) }}</pre>
              <pre v-else class="cap-code">{{ content.text || JSON.stringify(content, null, 2) }}</pre>
            </div>
          </template>
          <pre v-else class="cap-code">{{ JSON.stringify((resourceContent as any).result, null, 2) }}</pre>
        </template>
      </div>
    </template>
  </div>
</template>
