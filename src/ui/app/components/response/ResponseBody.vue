<script setup lang="ts">
const props = defineProps<{
  body?: string
  contentType: string
}>()

const viewMode = ref<'Pretty' | 'Raw'>('Pretty')

const isJson = computed(() => props.contentType?.includes('application/json'))

const prettyBody = computed(() => {
  if (!props.body) return ''
  if (!isJson.value) return props.body
  try {
    return JSON.stringify(JSON.parse(props.body), null, 2)
  } catch {
    return props.body
  }
})

const displayBody = computed(() => {
  if (viewMode.value === 'Raw') return props.body ?? ''
  return prettyBody.value
})

const isEmpty = computed(() => !props.body)

function setViewMode(mode: 'Pretty' | 'Raw') {
  viewMode.value = mode
}

async function copyToClipboard() {
  if (props.body) {
    await navigator.clipboard.writeText(props.body)
  }
}
</script>

<template>
  <div>
    <template v-if="isEmpty">
      <div class="p-4 text-center text-muted">No response</div>
    </template>
    <template v-else>
      <div class="flex items-center gap-2 mb-2">
        <button
          v-for="mode in (['Pretty', 'Raw'] as const)"
          :key="mode"
          data-testid="view-mode-tab"
          :class="{ active: viewMode === mode }"
          class="px-3 py-1 text-sm rounded"
          @click="setViewMode(mode)"
        >
          {{ mode }}
        </button>
        <button
          data-testid="copy-button"
          class="ml-auto px-3 py-1 text-sm rounded"
          @click="copyToClipboard"
        >
          Copy
        </button>
      </div>
      <pre data-testid="body-content" class="whitespace-pre-wrap break-all p-4 text-sm">{{ displayBody }}</pre>
    </template>
  </div>
</template>
