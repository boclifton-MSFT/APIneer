<script setup lang="ts">
import type { Environment } from '~/composables/useApi'

defineOptions({ name: 'EnvironmentSelector' })

const props = defineProps<{
  environments: Environment[]
}>()

const model = defineModel<string>({ default: '' })

const emit = defineEmits<{
  'activate': [id: string]
  'manage': []
}>()

const activeEnvironment = computed(() =>
  props.environments.find((e) => e.id === model.value)
)

function onChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  model.value = value
  emit('activate', value)
}
</script>

<template>
  <div class="environment-selector">
    <template v-if="environments.length === 0">
      <div data-testid="no-environments" class="text-muted text-sm">
        No environments
      </div>
    </template>
    <template v-else>
      <div v-if="activeEnvironment" data-testid="active-indicator" class="text-sm font-medium">
        {{ activeEnvironment.name }}
      </div>
      <select
        data-testid="environment-selector"
        :value="model"
        class="border rounded px-2 py-1 text-sm"
        @change="onChange"
      >
        <option value="">No Environment</option>
        <option
          v-for="env in environments"
          :key="env.id"
          :value="env.id"
        >
          {{ env.name }}
        </option>
      </select>
    </template>
  </div>
</template>
