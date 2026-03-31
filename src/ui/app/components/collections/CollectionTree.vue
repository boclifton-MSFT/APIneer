<script setup lang="ts">
import CollectionTreeFolder from '~/components/collections/CollectionTreeFolder.vue'

defineOptions({ name: 'CollectionTree' })

interface CollectionRequest {
  id: string
  name: string
  method: string
  url: string
  sortOrder: number
}

interface CollectionFolder {
  id: string
  name: string
  sortOrder: number
  subFolders: CollectionFolder[]
  requests: CollectionRequest[]
}

interface Collection {
  id: string
  name: string
  folders: CollectionFolder[]
  requests: CollectionRequest[]
}

const props = withDefaults(defineProps<{
  collections?: Collection[]
  activeRequestId?: string
}>(), {
  collections: () => []
})

const emit = defineEmits<{
  'select-request': [requestId: string]
}>()

function selectRequest(requestId: string) {
  emit('select-request', requestId)
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
        <div class="collection-header">
          <span class="collection-name">{{ collection.name }}</span>
        </div>

        <!-- Collection-level folders -->
        <template v-for="folder in collection.folders" :key="folder.id">
          <CollectionTreeFolder
            :folder="folder"
            :active-request-id="activeRequestId"
            :depth="1"
            @select-request="selectRequest"
          />
        </template>

        <!-- Collection-level requests (root-level, not in any folder) -->
        <div
          v-for="request in collection.requests"
          :key="request.id"
          :data-testid="`request-item-${request.id}`"
          class="request-item"
          :class="{ active: activeRequestId === request.id }"
          @click="selectRequest(request.id)"
        >
          <span data-testid="method-badge" class="method-badge">{{ request.method }}</span>
          <span class="request-name">{{ request.name }}</span>
        </div>
      </div>
    </template>
  </div>
</template>
