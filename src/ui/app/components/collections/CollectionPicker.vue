<script setup lang="ts">
import type { Collection, CollectionFolder } from '~/composables/useApi'

defineOptions({ name: 'CollectionPicker' })

const props = withDefaults(defineProps<{
  collections: Collection[]
  mode?: 'inline' | 'modal'
}>(), {
  mode: 'inline'
})

const emit = defineEmits<{
  select: [payload: { collectionId: string | null; folderId?: string }]
}>()

function handleSelectDefault() {
  emit('select', { collectionId: null, folderId: undefined })
}

function handleSelectCollection(collectionId: string) {
  emit('select', { collectionId, folderId: undefined })
}

function handleSelectFolder(collectionId: string, folderId: string) {
  emit('select', { collectionId, folderId })
}

interface FlatOption {
  id: string
  label: string
  depth: number
  type: 'collection' | 'folder'
  collectionId: string
}

function flattenFolders(folders: CollectionFolder[], collectionId: string, depth: number): FlatOption[] {
  const result: FlatOption[] = []
  for (const folder of folders) {
    result.push({
      id: folder.id,
      label: folder.name,
      depth,
      type: 'folder',
      collectionId
    })
    if (folder.subFolders && folder.subFolders.length > 0) {
      result.push(...flattenFolders(folder.subFolders, collectionId, depth + 1))
    }
  }
  return result
}

function getAllOptions(): FlatOption[] {
  const result: FlatOption[] = []
  for (const col of props.collections) {
    result.push({
      id: col.id,
      label: col.name,
      depth: 0,
      type: 'collection',
      collectionId: col.id
    })
    if (col.folders && col.folders.length > 0) {
      result.push(...flattenFolders(col.folders, col.id, 1))
    }
  }
  return result
}

const isDisabled = computed(() => props.collections.length === 0)
</script>

<template>
  <div
    v-if="mode === 'modal'"
    data-testid="collection-picker-modal"
  >
    <div
      data-testid="collection-picker"
      :class="['collection-picker']"
      :aria-disabled="isDisabled ? 'true' : undefined"
    >
      <template v-if="isDisabled">
        <div class="text-sm text-muted p-2">No collections</div>
      </template>
      <template v-else>
        <div
          data-testid="picker-option-default"
          class="px-3 py-1.5 text-sm cursor-pointer hover:bg-elevated"
          @click="handleSelectDefault"
        >
          Default
        </div>
        <div
          v-for="option in getAllOptions()"
          :key="option.id"
          :data-testid="`picker-option-${option.id}`"
          class="px-3 py-1.5 text-sm cursor-pointer hover:bg-elevated"
          :style="{ paddingLeft: `${(option.depth * 12) + 12}px` }"
          @click="option.type === 'collection' ? handleSelectCollection(option.id) : handleSelectFolder(option.collectionId, option.id)"
        >
          {{ '\u00A0'.repeat(option.depth * 2) }}{{ option.label }}
        </div>
      </template>
    </div>
  </div>

  <div
    v-else
    data-testid="collection-picker"
    :class="['collection-picker', { inline: mode === 'inline' }]"
    :aria-disabled="isDisabled ? 'true' : undefined"
  >
    <template v-if="isDisabled">
      <div class="text-sm text-muted p-2">No collections</div>
    </template>
    <template v-else>
      <div
        data-testid="picker-option-default"
        class="px-3 py-1.5 text-sm cursor-pointer hover:bg-elevated"
        @click="handleSelectDefault"
      >
        Default
      </div>
      <div
        v-for="option in getAllOptions()"
        :key="option.id"
        :data-testid="`picker-option-${option.id}`"
        class="px-3 py-1.5 text-sm cursor-pointer hover:bg-elevated"
        :style="{ paddingLeft: `${(option.depth * 12) + 12}px` }"
        @click="option.type === 'collection' ? handleSelectCollection(option.id) : handleSelectFolder(option.collectionId, option.id)"
      >
        {{ '\u00A0'.repeat(option.depth * 2) }}{{ option.label }}
      </div>
    </template>
  </div>
</template>
