<script setup lang="ts">
import type { CollectionFolder, CollectionRequest } from '~/composables/useApi'
import CollectionTreeFolder from '~/components/collections/CollectionTreeFolder.vue'
import { useCollectionDragDrop } from '~/composables/useCollectionDragDrop'

defineOptions({ name: 'CollectionTreeFolder' })

const props = withDefaults(defineProps<{
  folder: CollectionFolder
  activeRequestId?: string
  depth: number
  collectionId?: string
}>(), {
  collectionId: ''
})

const emit = defineEmits<{
  'select-request': [requestId: string]
  'move-request': [payload: { requestId: string, targetCollectionId: string, targetFolderId: string | null }]
  'reorder': [payload: { collectionId: string, folderId: string | null, requestIds: string[] }]
  'delete-request': [payload: { requestId: string, requestName: string }]
}>()

const { currentDrag, startDrag, endDrag } = useCollectionDragDrop()

const isExpanded = ref(true)
const isDragOver = ref(false)
const dropIndicator = ref<{ requestId: string, position: 'above' | 'below' } | null>(null)

function toggleFolder() {
  isExpanded.value = !isExpanded.value
}

function selectRequest(requestId: string) {
  emit('select-request', requestId)
}

function onRequestDragStart(event: DragEvent, request: CollectionRequest) {
  startDrag(event, {
    type: 'request',
    requestId: request.id,
    sourceCollectionId: props.collectionId,
    sourceFolderId: props.folder.id
  })
}

function onFolderDragOver(event: DragEvent) {
  if (!currentDrag.value) return
  event.preventDefault()
  if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
  isDragOver.value = true
}

function onFolderDragLeave(event: DragEvent) {
  const el = event.currentTarget as HTMLElement
  if (!el.contains(event.relatedTarget as Node)) {
    isDragOver.value = false
  }
}

function onFolderDrop(event: DragEvent) {
  event.preventDefault()
  isDragOver.value = false
  const drag = currentDrag.value
  if (!drag) return

  if (drag.sourceFolderId !== props.folder.id || drag.sourceCollectionId !== props.collectionId) {
    emit('move-request', {
      requestId: drag.requestId,
      targetCollectionId: props.collectionId,
      targetFolderId: props.folder.id
    })
  }
}

function onRequestDragOver(event: DragEvent, requestId: string) {
  if (!currentDrag.value) return
  event.preventDefault()
  event.stopPropagation()
  if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
  const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
  const midY = rect.top + rect.height / 2
  dropIndicator.value = {
    requestId,
    position: event.clientY < midY ? 'above' : 'below'
  }
}

function onRequestDragLeave(event: DragEvent) {
  const el = event.currentTarget as HTMLElement
  if (!el.contains(event.relatedTarget as Node)) {
    dropIndicator.value = null
  }
}

function onRequestDrop(event: DragEvent, targetRequestId: string) {
  event.preventDefault()
  event.stopPropagation()
  const position = dropIndicator.value?.position || 'below'
  dropIndicator.value = null

  const drag = currentDrag.value
  if (!drag) return

  const isSameContainer = drag.sourceFolderId === props.folder.id && drag.sourceCollectionId === props.collectionId

  if (isSameContainer) {
    const requestIds = props.folder.requests.map(r => r.id)
    const fromIndex = requestIds.indexOf(drag.requestId)
    if (fromIndex === -1) return
    requestIds.splice(fromIndex, 1)
    let toIndex = requestIds.indexOf(targetRequestId)
    if (toIndex === -1) return
    if (position === 'below') toIndex++
    requestIds.splice(toIndex, 0, drag.requestId)

    emit('reorder', {
      collectionId: props.collectionId,
      folderId: props.folder.id,
      requestIds
    })
  } else {
    emit('move-request', {
      requestId: drag.requestId,
      targetCollectionId: props.collectionId,
      targetFolderId: props.folder.id
    })
  }
}

function onChildMoveRequest(payload: { requestId: string, targetCollectionId: string, targetFolderId: string | null }) {
  emit('move-request', payload)
}

function onChildReorder(payload: { collectionId: string, folderId: string | null, requestIds: string[] }) {
  emit('reorder', payload)
}
</script>

<template>
  <div class="folder-node">
    <div
      :data-testid="`folder-toggle-${folder.id}`"
      class="folder-header cursor-pointer"
      :class="{ 'drag-over': isDragOver }"
      @click="toggleFolder()"
      @dragover="onFolderDragOver"
      @dragleave="onFolderDragLeave"
      @drop="onFolderDrop"
    >
      <span :data-testid="`folder-icon-${folder.id}`" class="folder-icon">
        {{ isExpanded ? '▼' : '▶' }}
      </span>
      <span class="folder-name">{{ folder.name }}</span>
    </div>

    <div
      v-show="isExpanded"
      :hidden="!isExpanded || undefined"
      :data-testid="`folder-content-${folder.id}`"
      class="folder-content"
    >
      <!-- Sub-folders (recursive) -->
      <template v-for="subFolder in folder.subFolders" :key="subFolder.id">
        <CollectionTreeFolder
          :folder="subFolder"
          :active-request-id="activeRequestId"
          :depth="depth + 1"
          :collection-id="collectionId"
          @select-request="selectRequest"
          @move-request="onChildMoveRequest"
          @reorder="onChildReorder"
          @delete-request="(payload: { requestId: string, requestName: string }) => emit('delete-request', payload)"
        />
      </template>

      <!-- Requests within this folder -->
      <div
        v-for="request in folder.requests"
        :key="request.id"
        :data-testid="`request-item-${request.id}`"
        class="request-item"
        :class="{
          active: activeRequestId === request.id,
          dragging: currentDrag?.requestId === request.id,
          'drop-above': dropIndicator?.requestId === request.id && dropIndicator?.position === 'above',
          'drop-below': dropIndicator?.requestId === request.id && dropIndicator?.position === 'below'
        }"
        draggable="true"
        @click="selectRequest(request.id)"
        @dragstart="onRequestDragStart($event, request)"
        @dragend="endDrag"
        @dragover="onRequestDragOver($event, request.id)"
        @dragleave="onRequestDragLeave"
        @drop="onRequestDrop($event, request.id)"
      >
        <span data-testid="method-badge" class="method-badge" :class="'method-' + request.method.toLowerCase()">{{ request.method }}</span>
        <span class="request-name">{{ request.name }}</span>
        <button
          :data-testid="`delete-request-${request.id}`"
          class="delete-icon"
          aria-label="Delete request"
          @click.stop="emit('delete-request', { requestId: request.id, requestName: request.name })"
        >
          <UIcon name="i-lucide-trash-2" class="size-3.5" />
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
/* ── Folder node ── */
.folder-node {
  margin-top: 1px;
}

/* ── Folder header ── */
.folder-header {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.375rem 0.75rem 0.375rem 1.25rem;
  border-radius: var(--ui-radius, 0.375rem);
  transition: background-color 0.15s;
  user-select: none;
}
.folder-header:hover {
  background-color: rgba(128, 128, 128, 0.08);
}

/* ── Folder icon (expand/collapse indicator) ── */
.folder-icon {
  font-size: 0.5rem;
  width: 0.75rem;
  flex-shrink: 0;
  color: var(--ui-text-dimmed, #94a3b8);
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

/* ── Folder name ── */
.folder-name {
  font-weight: 600;
  font-size: 0.8125rem;
  color: var(--ui-text, #1e293b);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ── Folder content (indented children) ── */
.folder-content {
  padding-left: 0.5rem;
}

/* ── Request rows (matches CollectionSidebar) ── */
.request-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.375rem 0.75rem 0.375rem 1.75rem;
  margin: 1px 0.25rem;
  border-radius: var(--ui-radius, 0.375rem);
  transition: background-color 0.15s;
  border-left: 3px solid transparent;
}
.request-item:hover {
  background-color: rgba(128, 128, 128, 0.08);
}
.request-item.active {
  background-color: rgba(14, 165, 233, 0.1);
  border-left-color: var(--color-primary-500, #0ea5e9);
}

/* ── Method badge ── */
.method-badge {
  font-family: var(--font-mono, ui-monospace, monospace);
  font-size: 0.625rem;
  font-weight: 700;
  text-transform: uppercase;
  min-width: 3rem;
  flex-shrink: 0;
  letter-spacing: 0.025em;
}
.method-get { color: #22c55e; }
.method-post { color: #3b82f6; }
.method-put { color: #f59e0b; }
.method-patch { color: #a855f7; }
.method-delete { color: #ef4444; }
.method-head { color: #6b7280; }
.method-options { color: #6b7280; }

/* ── Request name ── */
.request-name {
  flex: 1;
  font-size: 0.8125rem;
  color: var(--ui-text-muted, #64748b);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ── Delete icon (hover-reveal) ── */
.delete-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  padding: 0.125rem;
  border: none;
  background: none;
  color: var(--ui-text-dimmed, #94a3b8);
  cursor: pointer;
  opacity: 0;
  transition: opacity 0.15s, color 0.15s;
  border-radius: var(--ui-radius, 0.375rem);
}
.request-item:hover .delete-icon {
  opacity: 1;
}
.delete-icon:hover {
  color: #ef4444;
}

/* ── Drag-drop states (preserved) ── */
.dragging {
  opacity: 0.4;
}
.drag-over {
  outline: 2px dashed var(--color-primary-500, #0ea5e9);
  outline-offset: -2px;
  background-color: rgba(14, 165, 233, 0.05);
}
.drop-above {
  border-top: 2px solid var(--color-primary-500, #0ea5e9);
}
.drop-below {
  border-bottom: 2px solid var(--color-primary-500, #0ea5e9);
}
.request-item[draggable="true"] {
  cursor: grab;
}
.request-item[draggable="true"]:active {
  cursor: grabbing;
}
</style>
