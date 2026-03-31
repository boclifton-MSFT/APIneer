<script setup lang="ts">
import CollectionTreeFolder from '~/components/collections/CollectionTreeFolder.vue'

defineOptions({ name: 'CollectionTreeFolder' })

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

const props = defineProps<{
  folder: CollectionFolder
  activeRequestId?: string
  depth: number
}>()

const emit = defineEmits<{
  'select-request': [requestId: string]
}>()

const isExpanded = ref(true)

function toggleFolder() {
  isExpanded.value = !isExpanded.value
}

function selectRequest(requestId: string) {
  emit('select-request', requestId)
}
</script>

<template>
  <div class="folder-node">
    <div
      :data-testid="`folder-toggle-${folder.id}`"
      class="folder-header"
      @click="toggleFolder()"
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
          @select-request="selectRequest"
        />
      </template>

      <!-- Requests within this folder -->
      <div
        v-for="request in folder.requests"
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
  </div>
</template>
