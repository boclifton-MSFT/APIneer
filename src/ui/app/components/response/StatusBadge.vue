<script setup lang="ts">
import { statusSeverity } from '~/composables/useHttpColors'

const props = defineProps<{
  statusCode: number
  statusText: string
}>()

const statusClass = computed(() => {
  return `status-${statusSeverity(props.statusCode)}`
})
</script>

<template>
  <span
    data-testid="status-badge"
    :class="statusClass"
    class="inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm font-semibold"
  >
    {{ statusCode }} {{ statusText }}
  </span>
</template>
