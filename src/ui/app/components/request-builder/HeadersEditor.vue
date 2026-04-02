<script setup lang="ts">
defineOptions({ name: 'HeadersEditor' })

interface HeaderEntry {
  key: string
  value: string
}

const model = defineModel<HeaderEntry[]>({ default: () => [{ key: '', value: '' }] })

const internalHeaders = ref<HeaderEntry[]>([...model.value.map(h => ({ ...h }))])

watch(model, (newVal) => {
  internalHeaders.value = newVal.map(h => ({ ...h }))
}, { deep: true })

function emitUpdate() {
  model.value = internalHeaders.value.map(h => ({ ...h }))
}

function addHeader() {
  internalHeaders.value.push({ key: '', value: '' })
  emitUpdate()
}

function removeHeader(index: number) {
  internalHeaders.value.splice(index, 1)
  emitUpdate()
}

function updateKey(index: number, event: Event) {
  const target = event.target as HTMLInputElement
  internalHeaders.value[index].key = target.value
  emitUpdate()
}

function updateValue(index: number, event: Event) {
  const target = event.target as HTMLInputElement
  internalHeaders.value[index].value = target.value
  emitUpdate()
}
</script>

<template>
  <div class="headers-editor">
    <table>
      <thead>
        <tr>
          <th>Key</th>
          <th>Value</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(header, index) in internalHeaders" :key="index">
          <td>
            <input
              type="text"
              :value="header.key"
              placeholder="Header name"
              @input="updateKey(index, $event)"
            />
          </td>
          <td>
            <input
              type="text"
              :value="header.value"
              placeholder="Header value"
              @input="updateValue(index, $event)"
            />
          </td>
          <td>
            <button
              data-testid="remove-header"
              type="button"
              @click="removeHeader(index)"
            >
              ✕
            </button>
          </td>
        </tr>
      </tbody>
    </table>
    <button
      data-testid="add-header"
      type="button"
      @click="addHeader"
    >
      + Add Header
    </button>
  </div>
</template>

<style scoped>
.headers-editor table {
  width: 100%;
  border-collapse: collapse;
}

.headers-editor th {
  text-align: left;
  padding: 0.375rem 0.5rem;
  font-weight: 600;
  font-size: 0.75rem;
  text-transform: uppercase;
  color: var(--ui-text-muted, #6b7280);
}

.headers-editor td {
  padding: 0.25rem 0.5rem;
}

.headers-editor input {
  width: 100%;
  padding: 0.375rem 0.5rem;
  border: 1px solid var(--ui-border, #e5e7eb);
  border-radius: 0.375rem;
  font-size: inherit;
}

[data-testid="add-header"] {
  margin-top: 0.5rem;
  padding: 0.25rem 0.75rem;
  font-size: 0.875rem;
  cursor: pointer;
}

[data-testid="remove-header"] {
  cursor: pointer;
  color: #dc2626;
  background: none;
  border: none;
  font-size: 1rem;
}
</style>
