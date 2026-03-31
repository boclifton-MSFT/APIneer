<script setup lang="ts">
interface Header {
  name: string
  value: string
}

const props = defineProps<{
  headers?: Header[]
}>()

const sortedHeaders = computed(() => {
  if (!props.headers || props.headers.length === 0) return []
  return [...props.headers].sort((a, b) => a.name.localeCompare(b.name))
})

const isEmpty = computed(() => !props.headers || props.headers.length === 0)
</script>

<template>
  <div>
    <template v-if="isEmpty">
      <div class="p-4 text-center text-muted">No headers</div>
    </template>
    <template v-else>
      <div class="mb-2 text-sm text-muted">{{ headers!.length }} headers</div>
      <table class="w-full text-sm">
        <thead>
          <tr>
            <th class="text-left p-2">Name</th>
            <th class="text-left p-2">Value</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="header in sortedHeaders" :key="header.name">
            <td class="p-2 font-medium">{{ header.name }}</td>
            <td class="p-2">{{ header.value }}</td>
          </tr>
        </tbody>
      </table>
    </template>
  </div>
</template>
