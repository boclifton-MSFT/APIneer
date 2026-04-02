<script setup lang="ts">
defineOptions({ name: 'QueryParamsEditor' })

interface ParamEntry {
  key: string
  value: string
  enabled: boolean
}

const props = withDefaults(defineProps<{
  url?: string
}>(), {
  url: ''
})

const emit = defineEmits<{
  'update:url': [value: string]
}>()

const internalParams = ref<ParamEntry[]>([])
let suppressUrlSync = false
let suppressParamSync = false

// Decode a query-string segment, treating + as space per application/x-www-form-urlencoded
function decodePart(s: string): string {
  return decodeURIComponent(s.replace(/\+/g, ' '))
}

function parseParamsFromUrl(fullUrl: string): ParamEntry[] {
  const qIndex = fullUrl.indexOf('?')
  if (qIndex === -1) return []

  const queryString = fullUrl.slice(qIndex + 1)
  if (!queryString) return []

  return queryString.split('&').filter(Boolean).map(pair => {
    const eqIndex = pair.indexOf('=')
    if (eqIndex === -1) {
      return { key: decodePart(pair), value: '', enabled: true }
    }
    return {
      key: decodePart(pair.slice(0, eqIndex)),
      value: decodePart(pair.slice(eqIndex + 1)),
      enabled: true
    }
  })
}

function buildUrlFromParams(baseUrl: string, params: ParamEntry[]): string {
  const enabledParams = params.filter(p => p.enabled && (p.key || p.value))
  if (enabledParams.length === 0) return baseUrl

  const queryString = enabledParams
    .map(p => {
      const encodedKey = encodeURIComponent(p.key)
      const encodedValue = encodeURIComponent(p.value)
      return p.value ? `${encodedKey}=${encodedValue}` : encodedKey
    })
    .join('&')

  return `${baseUrl}?${queryString}`
}

function getBaseUrl(fullUrl: string): string {
  const qIndex = fullUrl.indexOf('?')
  return qIndex === -1 ? fullUrl : fullUrl.slice(0, qIndex)
}

// Parse URL into params when url prop changes
watch(() => props.url, (newUrl) => {
  if (suppressUrlSync) return
  const parsed = parseParamsFromUrl(newUrl)
  if (parsed.length > 0) {
    internalParams.value = parsed
  } else {
    // Keep existing disabled params, clear enabled ones from URL
    const hasDisabled = internalParams.value.some(p => !p.enabled)
    if (!hasDisabled) {
      internalParams.value = []
    } else {
      internalParams.value = internalParams.value.filter(p => !p.enabled)
    }
  }
}, { immediate: true })

function emitUrlUpdate() {
  suppressUrlSync = true
  const base = getBaseUrl(props.url)
  const newUrl = buildUrlFromParams(base, internalParams.value)
  emit('update:url', newUrl)
  nextTick(() => {
    suppressUrlSync = false
  })
}

function addParam() {
  internalParams.value.push({ key: '', value: '', enabled: true })
}

function removeParam(index: number) {
  internalParams.value.splice(index, 1)
  emitUrlUpdate()
}

function updateKey(index: number, event: Event) {
  const target = event.target as HTMLInputElement
  internalParams.value[index].key = target.value
  emitUrlUpdate()
}

function updateValue(index: number, event: Event) {
  const target = event.target as HTMLInputElement
  internalParams.value[index].value = target.value
  emitUrlUpdate()
}

function toggleEnabled(index: number, event: Event) {
  const target = event.target as HTMLInputElement
  internalParams.value[index].enabled = target.checked
  emitUrlUpdate()
}
</script>

<template>
  <div class="params-editor">
    <table>
      <thead>
        <tr>
          <th class="col-enabled">Enabled</th>
          <th>Key</th>
          <th>Value</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(param, index) in internalParams" :key="index">
          <td class="col-enabled">
            <input
              type="checkbox"
              :checked="param.enabled"
              data-testid="param-enabled"
              @change="toggleEnabled(index, $event)"
            />
          </td>
          <td>
            <input
              type="text"
              :value="param.key"
              placeholder="Parameter name"
              data-testid="param-key-input"
              :class="{ disabled: !param.enabled }"
              @input="updateKey(index, $event)"
            />
          </td>
          <td>
            <input
              type="text"
              :value="param.value"
              placeholder="Parameter value"
              data-testid="param-value-input"
              :class="{ disabled: !param.enabled }"
              @input="updateValue(index, $event)"
            />
          </td>
          <td>
            <button
              data-testid="remove-param"
              type="button"
              @click="removeParam(index)"
            >
              ✕
            </button>
          </td>
        </tr>
      </tbody>
    </table>
    <button
      data-testid="add-param"
      type="button"
      @click="addParam"
    >
      + Add Parameter
    </button>
  </div>
</template>

<style scoped>
.params-editor table {
  width: 100%;
  border-collapse: collapse;
}

.params-editor th {
  text-align: left;
  padding: 0.375rem 0.5rem;
  font-weight: 600;
  font-size: 0.75rem;
  text-transform: uppercase;
  color: var(--ui-text-muted, #6b7280);
}

.params-editor td {
  padding: 0.25rem 0.5rem;
}

.params-editor input[type="text"] {
  width: 100%;
  padding: 0.375rem 0.5rem;
  border: 1px solid var(--ui-border, #e5e7eb);
  border-radius: 0.375rem;
  font-size: inherit;
}

.params-editor input[type="text"].disabled {
  opacity: 0.5;
}

.col-enabled {
  width: 2rem;
  text-align: center;
}

.params-editor input[type="checkbox"] {
  cursor: pointer;
}

[data-testid="add-param"] {
  margin-top: 0.5rem;
  padding: 0.25rem 0.75rem;
  font-size: 0.875rem;
  cursor: pointer;
}

[data-testid="remove-param"] {
  cursor: pointer;
  color: #dc2626;
  background: none;
  border: none;
  font-size: 1rem;
}
</style>
