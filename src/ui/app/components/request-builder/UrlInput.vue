<script setup lang="ts">
defineOptions({ name: 'UrlInput' })

const props = withDefaults(defineProps<{
  modelValue?: string
}>(), {
  modelValue: ''
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const variableSegments = computed(() => {
  const url = props.modelValue
  if (!url) return []

  const segments: Array<{ text: string; isVariable: boolean }> = []
  const regex = /(\{\{[^}]+\}\})/g
  let lastIndex = 0
  let match: RegExpExecArray | null

  while ((match = regex.exec(url)) !== null) {
    if (match.index > lastIndex) {
      segments.push({ text: url.slice(lastIndex, match.index), isVariable: false })
    }
    segments.push({ text: match[1], isVariable: true })
    lastIndex = regex.lastIndex
  }

  if (lastIndex < url.length) {
    segments.push({ text: url.slice(lastIndex), isVariable: false })
  }

  return segments
})

function onInput(event: Event) {
  const target = event.target as HTMLInputElement
  emit('update:modelValue', target.value)
}
</script>

<template>
  <div class="url-input-wrapper">
    <div class="url-highlights" aria-hidden="true">
      <template v-for="(segment, i) in variableSegments" :key="i">
        <span v-if="segment.isVariable" class="url-variable">{{ segment.text }}</span>
        <span v-else class="url-plain">{{ segment.text }}</span>
      </template>
    </div>
    <input
      type="text"
      :value="modelValue"
      placeholder="Enter request URL"
      class="url-input"
      @input="onInput"
    />
  </div>
</template>

<style scoped>
.url-input-wrapper {
  position: relative;
  flex: 1;
}

.url-highlights {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  pointer-events: none;
  padding: 0.375rem 0.5rem;
  font-family: inherit;
  font-size: inherit;
  white-space: pre;
  overflow: hidden;
}

.url-plain {
  color: transparent;
}

.url-variable {
  color: #ea580c;
  background: rgba(234, 88, 12, 0.1);
  border-radius: 2px;
  padding: 0 2px;
}

.url-input {
  width: 100%;
  padding: 0.375rem 0.5rem;
  border: 1px solid var(--ui-border);
  border-radius: 0.375rem;
  background: transparent;
  font-family: inherit;
  font-size: inherit;
  position: relative;
}
</style>
