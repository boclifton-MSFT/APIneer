<script setup lang="ts">
defineOptions({ name: 'RequestBuilder' })

import MethodSelector from './MethodSelector.vue'
import UrlInput from './UrlInput.vue'
import HeadersEditor from './HeadersEditor.vue'
import BodyEditor from './BodyEditor.vue'

const REQUEST_TABS = ['Params', 'Headers', 'Body', 'Auth'] as const

const props = withDefaults(defineProps<{
  loading?: boolean
}>(), {
  loading: false
})

const emit = defineEmits<{
  send: []
}>()

const method = ref('GET')
const url = ref('')
const headers = ref([{ key: '', value: '' }])
const bodyContent = ref('')
const bodyType = ref('none')
const activeTab = ref<string>('Params')

function onSend() {
  if (!props.loading) {
    emit('send')
  }
}

function onKeydown(event: KeyboardEvent) {
  if (event.ctrlKey && event.key === 'Enter') {
    event.preventDefault()
    onSend()
  }
}
</script>

<template>
  <div class="request-builder" @keydown="onKeydown">
    <div class="request-url-bar">
      <MethodSelector v-model="method" />
      <UrlInput v-model="url" />
      <button
        data-testid="send-button"
        type="button"
        :class="['send-button', { loading: loading }]"
        :disabled="loading || undefined"
        @click="onSend"
      >
        Send
      </button>
    </div>

    <div class="request-tabs">
      <button
        v-for="tab in REQUEST_TABS"
        :key="tab"
        data-testid="request-tab"
        type="button"
        :class="['request-tab', { active: activeTab === tab }]"
        @click="activeTab = tab"
      >
        {{ tab }}
      </button>
    </div>

    <div class="request-tab-content">
      <div v-if="activeTab === 'Params'" class="tab-panel">
        <p class="text-muted text-sm">Query parameters editor coming soon.</p>
      </div>

      <div v-if="activeTab === 'Headers'" class="tab-panel">
        <HeadersEditor v-model="headers" />
      </div>

      <div v-if="activeTab === 'Body'" class="tab-panel">
        <BodyEditor v-model="bodyContent" v-model:body-type="bodyType" />
      </div>

      <div v-if="activeTab === 'Auth'" class="tab-panel">
        <p class="text-muted text-sm">Authentication editor coming soon.</p>
      </div>
    </div>
  </div>
</template>

<style scoped>
.request-builder {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.request-url-bar {
  display: flex;
  gap: 0.5rem;
  align-items: center;
}

.send-button {
  padding: 0.375rem 1rem;
  background: var(--ui-primary, #2563eb);
  color: white;
  border: none;
  border-radius: 0.375rem;
  font-weight: 600;
  cursor: pointer;
  white-space: nowrap;
}

.send-button:hover:not(:disabled) {
  opacity: 0.9;
}

.send-button:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.send-button.loading {
  opacity: 0.6;
}

.request-tabs {
  display: flex;
  gap: 0.25rem;
  border-bottom: 1px solid var(--ui-border, #e5e7eb);
}

.request-tab {
  padding: 0.375rem 0.75rem;
  border: none;
  background: none;
  cursor: pointer;
  font-size: 0.875rem;
  color: var(--ui-text-muted, #6b7280);
  border-bottom: 2px solid transparent;
}

.request-tab.active {
  color: var(--ui-text, #111827);
  border-bottom-color: var(--ui-primary, #2563eb);
}

.tab-panel {
  padding: 0.75rem 0;
}
</style>
