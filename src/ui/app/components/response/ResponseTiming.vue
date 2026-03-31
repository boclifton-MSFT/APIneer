<script setup lang="ts">
const props = defineProps<{
  timeMs: number
  sizeBytes: number
}>()

const formattedSize = computed(() => {
  const bytes = props.sizeBytes
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
})
</script>

<template>
  <div data-testid="timing-display" class="flex items-center gap-4 p-4 text-sm">
    <span>{{ timeMs }} ms</span>
    <span>{{ formattedSize }}</span>
  </div>
</template>
