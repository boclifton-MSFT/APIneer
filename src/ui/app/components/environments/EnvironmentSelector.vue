<script setup lang="ts">
defineOptions({ name: 'EnvironmentSelector' })

interface Environment {
  id: string
  name: string
  isActive: boolean
  workspaceId: string
}

const props = defineProps<{
  environments: Environment[]
  modelValue: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
  'activate': [id: string]
  'manage': []
}>()

const activeEnvironment = computed(() =>
  props.environments.find((e) => e.id === props.modelValue)
)

function onChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  emit('update:modelValue', value)
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
        :value="modelValue"
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
