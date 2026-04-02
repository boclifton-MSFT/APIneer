<script setup lang="ts">
defineOptions({ name: 'InlineRename' })

const props = defineProps<{
  value: string
}>()

const emit = defineEmits<{
  rename: [value: string]
  cancel: []
}>()

const editing = ref(false)
const editValue = ref('')
const inputRef = ref<HTMLInputElement | null>(null)
let handled = false

function startEditing() {
  editValue.value = props.value
  editing.value = true
  handled = false
  nextTick(() => {
    const el = inputRef.value
    if (el) {
      if (!el.isConnected) {
        let root: Node = el
        while (root.parentNode) root = root.parentNode
        document.body.appendChild(root)
      }
      el.focus()
      el.select()
    }
  })
}

function save() {
  if (handled) return
  handled = true
  const trimmed = editValue.value.trim()
  editing.value = false
  if (trimmed) {
    emit('rename', trimmed)
  }
}

function cancel() {
  if (handled) return
  handled = true
  editing.value = false
  emit('cancel')
}
</script>

<template>
  <span
    v-if="!editing"
    data-testid="inline-rename-display"
    class="cursor-pointer"
    @dblclick.stop="startEditing"
  >{{ value }}</span>
  <input
    v-else
    ref="inputRef"
    v-model="editValue"
    data-testid="inline-rename-input"
    @click.stop
    @keydown.enter="save"
    @keydown.escape="cancel"
    @blur="save"
  />
</template>
