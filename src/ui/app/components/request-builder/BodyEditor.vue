<script setup lang="ts">
defineOptions({ name: 'BodyEditor' })

interface FormDataEntry {
  key: string
  value: string
}

const BODY_MODES = [
  { label: 'None', value: 'none' },
  { label: 'Raw', value: 'raw' },
  { label: 'JSON', value: 'json' },
  { label: 'Form Data', value: 'form-data' }
] as const

const model = defineModel<string>({ default: '' })
const bodyType = defineModel<string>('bodyType', { default: 'none' })

const formDataEntries = ref<FormDataEntry[]>([{ key: '', value: '' }])
let suppressFormDataSync = false

function parseFormData(encoded: string): FormDataEntry[] {
  if (!encoded) return [{ key: '', value: '' }]
  const pairs = encoded.split('&').filter(Boolean)
  if (pairs.length === 0) return [{ key: '', value: '' }]
  return pairs.map(pair => {
    const eqIndex = pair.indexOf('=')
    if (eqIndex === -1) {
      return { key: decodeURIComponent(pair.replace(/\+/g, ' ')), value: '' }
    }
    return {
      key: decodeURIComponent(pair.slice(0, eqIndex).replace(/\+/g, ' ')),
      value: decodeURIComponent(pair.slice(eqIndex + 1).replace(/\+/g, ' '))
    }
  })
}

function serializeFormData(entries: FormDataEntry[]): string {
  return entries
    .filter(e => e.key || e.value)
    .map(e => {
      const k = encodeURIComponent(e.key)
      const v = encodeURIComponent(e.value)
      return `${k}=${v}`
    })
    .join('&')
}

// Sync modelValue → formDataEntries when in form-data mode
watch(model, (newVal) => {
  if (bodyType.value !== 'form-data' || suppressFormDataSync) return
  formDataEntries.value = parseFormData(newVal)
}, { immediate: true })

watch(bodyType, (newType) => {
  if (newType === 'form-data') {
    formDataEntries.value = parseFormData(model.value)
  }
})

function emitFormDataUpdate() {
  suppressFormDataSync = true
  model.value = serializeFormData(formDataEntries.value)
  nextTick(() => { suppressFormDataSync = false })
}

function addFormDataField() {
  formDataEntries.value.push({ key: '', value: '' })
  emitFormDataUpdate()
}

function removeFormDataField(index: number) {
  formDataEntries.value.splice(index, 1)
  emitFormDataUpdate()
}

function updateFormDataKey(index: number, event: Event) {
  const target = event.target as HTMLInputElement
  formDataEntries.value[index].key = target.value
  emitFormDataUpdate()
}

function updateFormDataValue(index: number, event: Event) {
  const target = event.target as HTMLInputElement
  formDataEntries.value[index].value = target.value
  emitFormDataUpdate()
}

const jsonError = computed(() => {
  if (bodyType.value !== 'json' || !model.value) return null
  try {
    JSON.parse(model.value)
    return null
  } catch (e) {
    return (e as Error).message
  }
})

const showTextarea = computed(() => bodyType.value === 'raw' || bodyType.value === 'json')

function selectMode(mode: string) {
  bodyType.value = mode
}

function onBodyInput(event: Event) {
  const target = event.target as HTMLTextAreaElement
  model.value = target.value
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
      :value="model"
      :placeholder="bodyType === 'json' ? '{\n  \n}' : 'Enter request body'"
      class="body-textarea"
      rows="8"
      @input="onBodyInput"
    />

    <div v-if="bodyType === 'form-data'" class="formdata-editor" data-testid="formdata-table">
      <table>
        <thead>
          <tr>
            <th>Key</th>
            <th>Value</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(entry, index) in formDataEntries" :key="index">
            <td>
              <input
                type="text"
                :value="entry.key"
                placeholder="Field name"
                data-testid="formdata-key-input"
                @input="updateFormDataKey(index, $event)"
              />
            </td>
            <td>
              <input
                type="text"
                :value="entry.value"
                placeholder="Field value"
                data-testid="formdata-value-input"
                @input="updateFormDataValue(index, $event)"
              />
            </td>
            <td>
              <button
                data-testid="remove-formdata"
                type="button"
                @click="removeFormDataField(index)"
              >
                ✕
              </button>
            </td>
          </tr>
        </tbody>
      </table>
      <button
        data-testid="add-formdata"
        type="button"
        @click="addFormDataField"
      >
        + Add Field
      </button>
    </div>

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

.formdata-editor table {
  width: 100%;
  border-collapse: collapse;
}

.formdata-editor th {
  text-align: left;
  padding: 0.375rem 0.5rem;
  font-weight: 600;
  font-size: 0.75rem;
  text-transform: uppercase;
  color: var(--ui-text-muted, #6b7280);
}

.formdata-editor td {
  padding: 0.25rem 0.5rem;
}

.formdata-editor input {
  width: 100%;
  padding: 0.375rem 0.5rem;
  border: 1px solid var(--ui-border, #e5e7eb);
  border-radius: 0.375rem;
  font-size: inherit;
}

[data-testid="add-formdata"] {
  margin-top: 0.5rem;
  padding: 0.25rem 0.75rem;
  font-size: 0.875rem;
}

[data-testid="remove-formdata"] {
  color: #dc2626;
  background: none;
  border: none;
  font-size: 1rem;
}
</style>
