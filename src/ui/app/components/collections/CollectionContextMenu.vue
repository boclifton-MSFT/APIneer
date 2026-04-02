<script setup lang="ts">
defineOptions({ name: 'CollectionContextMenu' })

interface MenuItem {
  label: string
  key: string
}

const props = defineProps<{
  visible: boolean
  item: { id: string; name: string; type: 'collection' | 'folder' | 'request'; [key: string]: unknown }
  position: { x: number; y: number }
}>()

const emit = defineEmits<{
  action: [payload: { action: string; item: typeof props.item }]
  close: []
}>()

const collectionActions: MenuItem[] = [
  { label: 'Rename', key: 'rename' },
  { label: 'Delete', key: 'delete' },
  { label: 'Duplicate', key: 'duplicate' },
  { label: 'New Folder', key: 'new-folder' },
  { label: 'New Request', key: 'new-request' },
  { label: 'Export', key: 'export' }
]

const folderActions: MenuItem[] = [
  { label: 'Rename', key: 'rename' },
  { label: 'Delete', key: 'delete' },
  { label: 'New Sub-folder', key: 'new-sub-folder' },
  { label: 'New Request', key: 'new-request' }
]

const requestActions: MenuItem[] = [
  { label: 'Rename', key: 'rename' },
  { label: 'Delete', key: 'delete' },
  { label: 'Duplicate', key: 'duplicate' },
  { label: 'Move to...', key: 'move-to' }
]

const actionsMap: Record<string, MenuItem[]> = {
  collection: collectionActions,
  folder: folderActions,
  request: requestActions
}

function getActions() {
  return actionsMap[props.item.type] || []
}

function handleAction(action: MenuItem) {
  emit('action', { action: action.key, item: props.item })
  emit('close')
}

function handleOverlayClick() {
  emit('close')
}
</script>

<template>
  <div v-if="visible">
    <div
      data-testid="context-menu-overlay"
      class="fixed inset-0 z-40 cursor-pointer"
      @click="handleOverlayClick"
    />
    <div
      data-testid="context-menu"
      class="fixed z-50 min-w-48 rounded-md border border-default bg-default shadow-lg py-1"
      :style="{ left: `${position.x}px`, top: `${position.y}px` }"
    >
      <button
        v-for="action in getActions()"
        :key="action.key"
        :data-testid="`menu-action-${action.key}`"
        class="w-full text-left px-3 py-1.5 text-sm hover:bg-elevated cursor-pointer"
        @click="handleAction(action)"
      >
        {{ action.label }}
      </button>
    </div>
  </div>
</template>
