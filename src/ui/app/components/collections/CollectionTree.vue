<script setup lang="ts">
import type { Collection, CollectionFolder, CollectionRequest } from '~/composables/useApi'
import CollectionTreeFolder from '~/components/collections/CollectionTreeFolder.vue'
import { useCollectionDragDrop } from '~/composables/useCollectionDragDrop'

defineOptions({ name: 'CollectionTree' })

const props = withDefaults(defineProps<{
  collections?: Collection[]
  activeRequestId?: string
}>(), {
  collections: () => []
})

const emit = defineEmits<{
  'select-request': [requestId: string]
  'move-request': [payload: { requestId: string, targetCollectionId: string, targetFolderId: string | null }]
  'reorder': [payload: { collectionId: string, folderId: string | null, requestIds: string[] }]
}>()

const { currentDrag, startDrag, endDrag } = useCollectionDragDrop()

const dragOverCollectionId = ref<string | null>(null)
const dropIndicator = ref<{ requestId: string, position: 'above' | 'below' } | null>(null)

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
  <div class="collection-tree">
    <div
      v-if="!collections || collections.length === 0"
      data-testid="empty-collections"
      class="empty-state"
    >
      <p>No collections yet</p>
    </div>

    <template v-else>
      <div
        v-for="collection in collections"
        :key="collection.id"
        class="collection-node"
      >
        <div
          class="collection-header"
          :class="{ 'drag-over': dragOverCollectionId === collection.id }"
          @dragover="onCollectionDragOver($event, collection.id)"
          @dragleave="onCollectionDragLeave"
          @drop="onCollectionDrop($event, collection.id)"
        >
          <span class="collection-name">{{ collection.name }}</span>
        </div>

        <!-- Collection-level folders -->
        <template v-for="folder in collection.folders" :key="folder.id">
          <CollectionTreeFolder
            :folder="folder"
            :active-request-id="activeRequestId"
            :depth="1"
            :collection-id="collection.id"
            @select-request="selectRequest"
            @move-request="onChildMoveRequest"
            @reorder="onChildReorder"
          />
        </template>

        <!-- Collection-level requests (root-level, not in any folder) -->
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
          <span data-testid="method-badge" class="method-badge">{{ request.method }}</span>
          <span class="request-name">{{ request.name }}</span>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
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
