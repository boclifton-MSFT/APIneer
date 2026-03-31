<script setup lang="ts">
defineOptions({ name: 'MethodSelector' })

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'] as const
type HttpMethod = (typeof HTTP_METHODS)[number]

const METHOD_COLORS: Record<HttpMethod, string> = {
  GET: 'green',
  POST: 'blue',
  PUT: 'orange',
  PATCH: 'yellow',
  DELETE: 'red',
  HEAD: 'purple',
  OPTIONS: 'gray'
}

const props = withDefaults(defineProps<{
  modelValue?: string
}>(), {
  modelValue: 'GET'
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const currentColor = computed(() => METHOD_COLORS[props.modelValue as HttpMethod] ?? 'gray')

function onChange(event: Event) {
  const target = event.target as HTMLSelectElement
  emit('update:modelValue', target.value)
}
</script>

<template>
  <div
    data-testid="method-selector"
    :class="['method-selector', `method-${currentColor}`]"
  >
    <select :value="modelValue" @change="onChange">
      <option v-for="method in HTTP_METHODS" :key="method" :value="method">
        {{ method }}
      </option>
    </select>
  </div>
</template>

<style scoped>
.method-selector select {
  font-weight: 600;
  padding: 0.375rem 0.5rem;
  border-radius: 0.375rem;
  border: 1px solid var(--ui-border);
  background: var(--ui-bg);
  cursor: pointer;
}

.method-green select { color: #16a34a; }
.method-blue select { color: #2563eb; }
.method-orange select { color: #ea580c; }
.method-yellow select { color: #ca8a04; }
.method-red select { color: #dc2626; }
.method-purple select { color: #9333ea; }
.method-gray select { color: #6b7280; }
</style>
