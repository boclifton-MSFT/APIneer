<script setup lang="ts">
defineOptions({ name: 'BodyEditor' })

const BODY_MODES = [
  { label: 'None', value: 'none' },
  { label: 'Raw', value: 'raw' },
  { label: 'JSON', value: 'json' },
  { label: 'Form Data', value: 'form-data' }
] as const

const props = withDefaults(defineProps<{
  modelValue?: string
  bodyType?: string
}>(), {
  modelValue: '',
  bodyType: 'none'
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
  'update:bodyType': [value: string]
}>()

const jsonError = computed(() => {
  if (props.bodyType !== 'json' || !props.modelValue) return null
  try {
    JSON.parse(props.modelValue)
    return null
  } catch (e) {
    return (e as Error).message
  }
})

const showTextarea = computed(() => props.bodyType === 'raw' || props.bodyType === 'json')

function selectMode(mode: string) {
  emit('update:bodyType', mode)
}

function onBodyInput(event: Event) {
  const target = event.target as HTMLTextAreaElement
  emit('update:modelValue', target.value)
}
</script>

<template>
  <div class="body-editor">
    <div class="body-mode-tabs">
      <button
        v-for="mode in BODY_MODES"
        :key="mode.value"
        data-testid="body-mode-tab"
        type="button"
        :class="['body-mode-tab', { active: bodyType === mode.value }]"
        @click="selectMode(mode.value)"
      >
        {{ mode.label }}
      </button>
    </div>

    <textarea
      v-if="showTextarea"
      :value="modelValue"
      :placeholder="bodyType === 'json' ? '{\n  \n}' : 'Enter request body'"
      class="body-textarea"
      rows="8"
      @input="onBodyInput"
    />

    <div v-if="jsonError" data-testid="json-error" class="json-error">
      {{ jsonError }}
    </div>
  </div>
</template>

<style scoped>
.body-mode-tabs {
  display: flex;
  gap: 0.25rem;
  margin-bottom: 0.5rem;
}

.body-mode-tab {
  padding: 0.375rem 0.75rem;
  border: 1px solid transparent;
  border-radius: 0.375rem;
  background: none;
  cursor: pointer;
  font-size: 0.875rem;
  color: var(--ui-text-muted, #6b7280);
}

.body-mode-tab.active {
  background: var(--ui-bg-elevated, #f3f4f6);
  color: var(--ui-text, #111827);
  border-color: var(--ui-border, #e5e7eb);
}

.body-textarea {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid var(--ui-border, #e5e7eb);
  border-radius: 0.375rem;
  font-family: 'Fira Code', 'Cascadia Code', monospace;
  font-size: 0.875rem;
  resize: vertical;
}

.json-error {
  margin-top: 0.25rem;
  color: #dc2626;
  font-size: 0.75rem;
}
</style>
