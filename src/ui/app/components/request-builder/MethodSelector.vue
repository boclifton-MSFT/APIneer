<script setup lang="ts">
import { METHOD_CSS_COLORS, methodCssColor, type HttpMethod } from '~/composables/useHttpColors'

defineOptions({ name: 'MethodSelector' })

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'] as const

const model = defineModel<string>({ default: 'GET' })

const currentColor = computed(() => methodCssColor(model.value))

function onChange(event: Event) {
  const target = event.target as HTMLSelectElement
  model.value = target.value
}
</script>

<template>
  <div
    data-testid="method-selector"
    :class="['method-selector', `method-${currentColor}`]"
  >
    <select :value="model" @change="onChange">
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
