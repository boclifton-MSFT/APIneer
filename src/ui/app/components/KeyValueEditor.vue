<script setup lang="ts">
defineOptions({ name: 'KeyValueEditor' })

const props = withDefaults(defineProps<{
  keyPlaceholder?: string
  valuePlaceholder?: string
}>(), {
  keyPlaceholder: 'Key',
  valuePlaceholder: 'Value'
})

const model = defineModel<{ key: string; value: string }[]>({
  default: () => [{ key: '', value: '' }]
})

function addRow() {
  model.value.push({ key: '', value: '' })
}

function removeRow(index: number) {
  model.value.splice(index, 1)
  if (model.value.length === 0) {
    model.value.push({ key: '', value: '' })
  }
}
</script>

<template>
  <table class="env-table w-full">
    <thead>
      <tr>
        <th class="text-left text-xs uppercase text-muted font-semibold py-1.5 px-2">Key</th>
        <th class="text-left text-xs uppercase text-muted font-semibold py-1.5 px-2">Value</th>
        <th class="w-8 text-right py-1.5 pr-1">
          <UButton
            icon="i-lucide-plus"
            size="xs"
            variant="ghost"
            color="neutral"
            @click="addRow"
          />
        </th>
      </tr>
    </thead>
    <tbody>
      <tr v-for="(entry, index) in model" :key="index">
        <td class="py-1 px-2">
          <UInput v-model="entry.key" :placeholder="props.keyPlaceholder" size="sm" />
        </td>
        <td class="py-1 px-2">
          <UInput v-model="entry.value" :placeholder="props.valuePlaceholder" size="sm" />
        </td>
        <td class="py-1 px-1">
          <UButton
            icon="i-lucide-x"
            size="xs"
            variant="ghost"
            color="neutral"
            @click="removeRow(index)"
          />
        </td>
      </tr>
    </tbody>
  </table>
</template>
