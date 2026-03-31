<script setup lang="ts">
defineOptions({ name: 'ImportModal' })

const props = defineProps<{
  visible: boolean
}>()

const emit = defineEmits<{
  close: []
  import: [payload: { format: string; data: string }]
}>()

const selectedFormat = ref('postman')
const fileContent = ref('')
const curlInput = ref('')

const hasContent = computed(() => {
  if (selectedFormat.value === 'curl') {
    return curlInput.value.trim().length > 0
  }
  return fileContent.value.length > 0
})

const previewText = computed(() => {
  if (hasContent.value) {
    if (selectedFormat.value === 'curl') return curlInput.value
    return fileContent.value
  }
  return 'No file selected'
})

function handleFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  const reader = new FileReader()
  reader.onload = (e) => {
    fileContent.value = (e.target?.result as string) || ''
  }
  reader.readAsText(file)
}

function handleImport() {
  const data = selectedFormat.value === 'curl' ? curlInput.value : fileContent.value
  emit('import', { format: selectedFormat.value, data })
}

function handleClose() {
  emit('close')
}
</script>

<template>
  <div v-if="visible" data-testid="import-modal">
    <div class="flex items-center justify-between mb-4">
      <h2 class="text-lg font-semibold">Import</h2>
      <button data-testid="close-button" @click="handleClose">
        &times;
      </button>
    </div>

    <div
      data-testid="file-upload-area"
      class="border-2 border-dashed rounded-lg p-6 text-center cursor-pointer mb-4"
    >
      <p>Drag &amp; drop a file here, or click to browse</p>
      <input type="file" class="hidden" @change="handleFileChange" />
    </div>

    <div class="mb-4">
      <label class="block text-sm font-medium mb-1">Format</label>
      <select
        v-model="selectedFormat"
        data-testid="format-selector"
        class="w-full border rounded px-3 py-2"
      >
        <option value="postman">Postman</option>
        <option value="curl">cURL</option>
        <option value="har">HAR</option>
      </select>
    </div>

    <div v-if="selectedFormat === 'curl'" class="mb-4">
      <label class="block text-sm font-medium mb-1">Paste cURL command</label>
      <textarea
        v-model="curlInput"
        data-testid="curl-input"
        class="w-full border rounded px-3 py-2 font-mono text-sm"
        rows="4"
        placeholder="curl -X GET https://api.example.com"
      />
    </div>

    <div data-testid="import-preview" class="mb-4 p-3 bg-gray-50 rounded min-h-[60px]">
      {{ previewText }}
    </div>

    <button
      data-testid="import-button"
      :disabled="!hasContent"
      class="px-4 py-2 rounded font-medium"
      @click="handleImport"
    >
      Import
    </button>
  </div>
</template>
