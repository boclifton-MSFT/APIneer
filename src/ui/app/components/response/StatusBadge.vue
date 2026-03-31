<script setup lang="ts">
const props = defineProps<{
  statusCode: number
  statusText: string
}>()

const statusClass = computed(() => {
  const code = props.statusCode
  if (code >= 200 && code < 300) return 'status-success'
  if (code >= 300 && code < 400) return 'status-info'
  if (code >= 400 && code < 500) return 'status-warning'
  return 'status-error'
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
