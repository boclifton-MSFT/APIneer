<script setup lang="ts">
import type { Collection } from '~/composables/useApi'
import CollectionTree from '~/components/collections/CollectionTree.vue'

definePageMeta({
  layout: 'dashboard'
})

const api = useApi()
const toast = useToast()
const router = useRouter()

const collections = ref<Collection[]>([])
const loading = ref(false)
const showNewDialog = ref(false)
const newCollectionName = ref('')

async function loadCollections() {
  loading.value = true
  try {
    collections.value = await api.getCollections()
  } catch {
    collections.value = []
  } finally {
    loading.value = false
  }
}

async function createCollection() {
  if (!newCollectionName.value.trim()) return
  try {
    await api.createCollection({ name: newCollectionName.value.trim() })
    newCollectionName.value = ''
    showNewDialog.value = false
    await loadCollections()
    toast.add({ title: 'Collection created', color: 'success', icon: 'i-lucide-check' })
  } catch {
    toast.add({ title: 'Failed to create collection', color: 'error' })
  }
}

async function deleteCollection(id: string) {
  try {
    await api.deleteCollection(id)
    await loadCollections()
    toast.add({ title: 'Collection deleted', color: 'success' })
  } catch {
    toast.add({ title: 'Failed to delete collection', color: 'error' })
  }
}

function onSelectRequest(requestId: string) {
  router.push({ path: '/', query: { request: requestId } })
}

onMounted(() => {
  loadCollections()
})
</script>

<template>
  <UDashboardPanel>
    <template #header>
      <UDashboardNavbar title="Collections">
        <template #right>
          <UButton
            icon="i-lucide-plus"
            label="New Collection"
            size="sm"
            @click="showNewDialog = true"
          />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <div v-if="loading" class="flex items-center justify-center p-12">
        <UIcon name="i-lucide-loader-2" class="size-6 animate-spin text-muted" />
      </div>

      <div v-else-if="collections.length === 0" class="flex flex-col items-center justify-center h-full gap-4 p-8">
        <div class="flex items-center justify-center size-16 rounded-xl bg-primary/10">
          <UIcon name="i-lucide-folder-open" class="size-8 text-primary" />
        </div>
        <div class="text-center">
          <h2 class="text-lg font-semibold text-highlighted">No collections yet</h2>
          <p class="text-sm text-muted mt-1">Organize your API requests into collections.</p>
        </div>
        <UButton
          label="Create Collection"
          icon="i-lucide-plus"
          size="lg"
          @click="showNewDialog = true"
        />
      </div>

      <div v-else class="p-4">
        <CollectionTree
          :collections="collections"
          @select-request="onSelectRequest"
        />
      </div>
    </template>
  </UDashboardPanel>

  <!-- New Collection Modal -->
  <UModal v-model:open="showNewDialog" title="New Collection" description="Create a new collection to organize your requests.">
    <template #body>
      <UFormField label="Collection Name" required>
        <UInput
          v-model="newCollectionName"
          placeholder="My API Collection"
          autofocus
          @keydown.enter="createCollection"
        />
      </UFormField>
    </template>
    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton variant="ghost" color="neutral" label="Cancel" @click="showNewDialog = false" />
        <UButton label="Create" icon="i-lucide-plus" :disabled="!newCollectionName.trim()" @click="createCollection" />
      </div>
    </template>
  </UModal>
</template>
