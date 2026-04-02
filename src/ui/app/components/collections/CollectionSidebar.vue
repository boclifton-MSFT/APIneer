<script setup lang="ts">
import type { Collection, CollectionFolder, CollectionRequest } from '~/composables/useApi'
import CollectionTreeFolder from '~/components/collections/CollectionTreeFolder.vue'
import InlineRename from '~/components/collections/InlineRename.vue'
import { useCollectionDragDrop } from '~/composables/useCollectionDragDrop'

defineOptions({ name: 'CollectionSidebar' })

const props = withDefaults(defineProps<{
  collections?: Collection[]
  activeRequestId?: string
  selectedCollectionId?: string
}>(), {
  collections: () => []
})

const emit = defineEmits<{
  'new-request': [payload: { collectionId: string }]
  'new-collection': []
  'select-request': [requestId: string]
  'move-request': [payload: { requestId: string, targetCollectionId: string, targetFolderId: string | null }]
  'reorder': [payload: { collectionId: string, folderId: string | null, requestIds: string[] }]
  'rename-request': [payload: { requestId: string, name: string }]
  'rename-collection': [payload: { collectionId: string, name: string }]
  'delete-request': [payload: { requestId: string, requestName: string }]
}>()

const { currentDrag, startDrag, endDrag } = useCollectionDragDrop()

const searchQuery = ref('')
const dragOverCollectionId = ref<string | null>(null)
const dropIndicator = ref<{ requestId: string, position: 'above' | 'below' } | null>(null)

// Collapse/expand state — tracks which collections are collapsed (all start expanded)
const collapsedCollections = ref(new Set<string>())

function toggleCollapse(collectionId: string) {
  const next = new Set(collapsedCollections.value)
  if (next.has(collectionId)) {
    next.delete(collectionId)
  } else {
    next.add(collectionId)
  }
  collapsedCollections.value = next
}

function isExpanded(collectionId: string): boolean {
  return !collapsedCollections.value.has(collectionId)
}

function renameRequest(requestId: string, name: string) {
  emit('rename-request', { requestId, name })
}

function countAllRequests(collection: Collection): number {
  let count = collection.requests.length
  function countInFolders(folders: CollectionFolder[]) {
    for (const folder of folders) {
      count += folder.requests.length
      countInFolders(folder.subFolders)
    }
  }
  countInFolders(collection.folders)
  return count
}

const collectionCounts = computed(() => {
  const counts: Record<string, number> = {}
  for (const col of props.collections) {
    counts[col.id] = countAllRequests(col)
  }
  return counts
})

function filterFolders(folders: CollectionFolder[], query: string): CollectionFolder[] {
  return folders
    .map(folder => {
      const filteredRequests = folder.requests.filter(r =>
        r.name.toLowerCase().includes(query)
      )
      const filteredSubs = filterFolders(folder.subFolders, query)
      if (filteredRequests.length === 0 && filteredSubs.length === 0) return null
      return { ...folder, requests: filteredRequests, subFolders: filteredSubs }
    })
    .filter((f): f is CollectionFolder => f !== null)
}

const sortByName = (a: { name: string }, b: { name: string }) =>
  a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })

const filteredCollections = computed(() => {
  if (!searchQuery.value.trim()) return [...props.collections].sort(sortByName)
  const query = searchQuery.value.toLowerCase()
  return props.collections
    .map(col => {
      const filteredRootRequests = col.requests.filter(r =>
        r.name.toLowerCase().includes(query)
      )
      const filteredFolders = filterFolders(col.folders, query)
      if (filteredRootRequests.length === 0 && filteredFolders.length === 0) return null
      return { ...col, requests: filteredRootRequests, folders: filteredFolders }
    })
    .filter((c): c is Collection => c !== null)
    .sort(sortByName)
})

function selectRequest(requestId: string) {
  emit('select-request', requestId)
}

function onRequestDragStart(event: DragEvent, request: CollectionRequest, collectionId: string) {
  startDrag(event, {
    type: 'request',
    requestId: request.id,
    sourceCollectionId: collectionId,
    sourceFolderId: null
  })
}

function onCollectionDragOver(event: DragEvent, collectionId: string) {
  if (!currentDrag.value) return
  event.preventDefault()
  if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
  dragOverCollectionId.value = collectionId
}

function onCollectionDragLeave(event: DragEvent) {
  const el = event.currentTarget as HTMLElement
  if (!el.contains(event.relatedTarget as Node)) {
    dragOverCollectionId.value = null
  }
}

function onCollectionDrop(event: DragEvent, collectionId: string) {
  event.preventDefault()
  dragOverCollectionId.value = null
  const drag = currentDrag.value
  if (!drag) return

  if (drag.sourceCollectionId !== collectionId || drag.sourceFolderId !== null) {
    emit('move-request', {
      requestId: drag.requestId,
      targetCollectionId: collectionId,
      targetFolderId: null
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

function onRequestDrop(event: DragEvent, targetRequestId: string, collectionId: string) {
  event.preventDefault()
  event.stopPropagation()
  const position = dropIndicator.value?.position || 'below'
  dropIndicator.value = null

  const drag = currentDrag.value
  if (!drag) return

  const isSameContainer = drag.sourceCollectionId === collectionId && drag.sourceFolderId === null

  if (isSameContainer) {
    const collection = props.collections.find(c => c.id === collectionId)
    if (!collection) return
    const requestIds = collection.requests.map(r => r.id)
    const fromIndex = requestIds.indexOf(drag.requestId)
    if (fromIndex === -1) return
    requestIds.splice(fromIndex, 1)
    let toIndex = requestIds.indexOf(targetRequestId)
    if (toIndex === -1) return
    if (position === 'below') toIndex++
    requestIds.splice(toIndex, 0, drag.requestId)

    emit('reorder', {
      collectionId,
      folderId: null,
      requestIds
    })
  } else {
    emit('move-request', {
      requestId: drag.requestId,
      targetCollectionId: collectionId,
      targetFolderId: null
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
  <div class="collection-sidebar">
    <input
      v-model="searchQuery"
      data-testid="sidebar-search"
      type="text"
      placeholder="Search requests..."
      class="sidebar-search"
    />

    <div class="sidebar-actions flex gap-2">
      <UButton
        data-testid="new-request-button"
        label="New Request"
        icon="i-lucide-plus"
        size="xs"
        variant="soft"
        @click="emit('new-request', { collectionId: selectedCollectionId || '' })"
      />
      <UButton
        data-testid="new-collection-button"
        label="New Collection"
        icon="i-lucide-folder-plus"
        size="xs"
        variant="soft"
        @click="emit('new-collection')"
      />
    </div>

    <div v-if="collections.length === 0" data-testid="sidebar-empty-state" class="empty-state">
      <p>No collections</p>
    </div>

    <template v-else>
      <div v-for="collection in filteredCollections" :key="collection.id" class="collection-node">
        <div
          class="collection-header"
          :class="{ 'drag-over': dragOverCollectionId === collection.id }"
          @click="toggleCollapse(collection.id)"
          @dragover="onCollectionDragOver($event, collection.id)"
          @dragleave="onCollectionDragLeave"
          @drop="onCollectionDrop($event, collection.id)"
        >
          <UIcon name="i-lucide-chevron-right" class="collection-chevron" :class="{ 'chevron-expanded': isExpanded(collection.id) }" />
          <UIcon name="i-lucide-folder" class="collection-folder-icon" />
          <InlineRename
            :value="collection.name"
            class="collection-name"
            @rename="(name: string) => emit('rename-collection', { collectionId: collection.id, name })"
          />
          <span :data-testid="`collection-count-${collection.id}`" class="collection-count">
            {{ collectionCounts[collection.id] }}
          </span>
        </div>

        <div v-show="isExpanded(collection.id)" class="collection-content">
          <template v-for="folder in collection.folders" :key="folder.id">
            <CollectionTreeFolder
              :folder="folder"
              :active-request-id="activeRequestId"
              :depth="1"
              :collection-id="collection.id"
              @select-request="selectRequest"
              @move-request="onChildMoveRequest"
              @reorder="onChildReorder"
              @delete-request="(payload: { requestId: string, requestName: string }) => emit('delete-request', payload)"
            />
          </template>

          <div
            v-for="request in collection.requests"
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
            @dragstart="onRequestDragStart($event, request, collection.id)"
            @dragend="endDrag"
            @dragover="onRequestDragOver($event, request.id)"
            @dragleave="onRequestDragLeave"
            @drop="onRequestDrop($event, request.id, collection.id)"
          >
            <span class="method-badge" :class="'method-' + request.method.toLowerCase()">{{ request.method }}</span>
            <InlineRename
              :data-testid="`request-name-${request.id}`"
              :value="request.name"
              class="request-name"
              @rename="(name: string) => renameRequest(request.id, name)"
            />
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
  </div>
</template>

<style scoped>
/* ── Layout ── */
.collection-sidebar {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  height: 100%;
}

/* ── Search ── */
.sidebar-search {
  width: 100%;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--ui-border, #e2e8f0);
  border-radius: var(--ui-radius, 0.375rem);
  font-size: 0.8125rem;
  background: var(--ui-bg, #ffffff);
  color: var(--ui-text, #1e293b);
  outline: none;
  transition: border-color 0.15s, box-shadow 0.15s;
}
.sidebar-search::placeholder {
  color: var(--ui-text-dimmed, #94a3b8);
}
.sidebar-search:focus {
  border-color: var(--color-primary-500, #0ea5e9);
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15);
}

/* ── Action buttons row ── */
.sidebar-actions {
  margin-bottom: 0.25rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--ui-border, #e2e8f0);
}

/* ── Empty state ── */
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 2.5rem 1rem;
  color: var(--ui-text-dimmed, #94a3b8);
  font-size: 0.8125rem;
}

/* ── Collection blocks ── */
.collection-node {
  border-bottom: 1px solid var(--ui-border, #e2e8f0);
}
.collection-node:last-child {
  border-bottom: none;
}

/* ── Collection header ── */
.collection-header {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.5rem 0.75rem;
  cursor: pointer;
  border-radius: var(--ui-radius, 0.375rem);
  transition: background-color 0.15s;
  user-select: none;
}
.collection-header:hover {
  background-color: rgba(128, 128, 128, 0.08);
}

/* ── Chevron + folder icon ── */
.collection-chevron {
  width: 0.875rem;
  height: 0.875rem;
  flex-shrink: 0;
  color: var(--ui-text-dimmed, #94a3b8);
  transition: transform 0.2s ease;
}
.collection-chevron.chevron-expanded {
  transform: rotate(90deg);
}
.collection-folder-icon {
  width: 1rem;
  height: 1rem;
  flex-shrink: 0;
  color: var(--ui-text-muted, #64748b);
}

/* ── Collection name ── */
.collection-name {
  flex: 1;
  font-weight: 600;
  font-size: 0.8125rem;
  color: var(--ui-text, #1e293b);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ── Request count badge ── */
.collection-count {
  font-size: 0.6875rem;
  font-weight: 500;
  color: var(--ui-text-dimmed, #94a3b8);
  background: rgba(128, 128, 128, 0.1);
  padding: 0.0625rem 0.4375rem;
  border-radius: 9999px;
  flex-shrink: 0;
  line-height: 1.4;
}

/* ── Request rows ── */
.request-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.375rem 0.75rem 0.375rem 2.25rem;
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
