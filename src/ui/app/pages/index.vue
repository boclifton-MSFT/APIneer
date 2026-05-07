<script setup lang="ts">
import type { ApiRequest as ApiRequestType, ApiResponse, Collection } from '~/composables/useApi'
import RequestBuilder from '~/components/request-builder/RequestBuilder.vue'
import ResponseViewer from '~/components/response/ResponseViewer.vue'
import CollectionSidebar from '~/components/collections/CollectionSidebar.vue'

definePageMeta({
  layout: 'dashboard'
})

const api = useApi()
const toast = useToast()
const router = useRouter()
const route = useRoute()

const requests = ref<ApiRequestType[]>([])
const collections = ref<Collection[]>([])
const selectedRequestId = ref<string | null>(null)
const selectedCollectionId = ref<string | null>(null)
const selectedRequest = ref<ApiRequestType | null>(null)
const response = ref<ApiResponse | null>(null)
const loading = ref(true)
const loadingCollections = ref(true)
const sending = ref(false)

// Create collection modal
const showCreateCollectionModal = ref(false)
const newCollectionName = ref('')

// Delete request confirmation modal
const showDeleteRequestModal = ref(false)
const deleteTarget = ref<{ requestId: string, requestName: string } | null>(null)

function onDeleteRequest(payload: { requestId: string, requestName: string }) {
  deleteTarget.value = payload
  showDeleteRequestModal.value = true
}

async function confirmDeleteRequest() {
  if (!deleteTarget.value) return
  const { requestId } = deleteTarget.value
  showDeleteRequestModal.value = false
  try {
    await api.deleteRequest(requestId)
    if (selectedRequestId.value === requestId) {
      selectedRequest.value = null
      selectedRequestId.value = null
      response.value = null
    }
    await loadCollections()
    await loadRequests()
    toast.add({ title: 'Request deleted', color: 'success', icon: 'i-lucide-check' })
  } catch {
    toast.add({ title: 'Failed to delete request', color: 'error' })
  } finally {
    deleteTarget.value = null
  }
}

function cancelDeleteRequest() {
  showDeleteRequestModal.value = false
  deleteTarget.value = null
}

// Load request list
async function loadRequests() {
  loading.value = true
  try {
    requests.value = await api.getRequests()
  } catch {
    requests.value = []
  } finally {
    loading.value = false
  }
}

// Load collections
async function loadCollections() {
  loadingCollections.value = true
  try {
    collections.value = await api.getCollections()
    if (collections.value.length > 0 && !selectedCollectionId.value) {
      selectedCollectionId.value = collections.value[0].id
    }
  } catch {
    collections.value = []
  } finally {
    loadingCollections.value = false
  }
}

// Select a request
async function selectRequest(id: string) {
  selectedRequestId.value = id
  try {
    selectedRequest.value = await api.getRequest(id)
    response.value = null
  } catch {
    toast.add({ title: 'Failed to load request', color: 'error' })
  }
}

// Create new request in a collection
async function createNewRequest(payload?: { collectionId: string }) {
  try {
    const collectionId = payload?.collectionId || selectedCollectionId.value || undefined
    const newReq = await api.createRequest({
      name: 'Untitled Request',
      method: 'GET',
      url: '',
      collectionId: collectionId || undefined
    })
    await loadCollections()
    await loadRequests()
    await selectRequest(newReq.id)
    toast.add({ title: 'Request created', color: 'success', icon: 'i-lucide-check' })
  } catch {
    toast.add({ title: 'Failed to create request', color: 'error' })
  }
}

// Create a new collection
async function createCollection() {
  if (!newCollectionName.value.trim()) return
  try {
    const col = await api.createCollection({ name: newCollectionName.value.trim() })
    selectedCollectionId.value = col.id
    newCollectionName.value = ''
    showCreateCollectionModal.value = false
    await loadCollections()
    toast.add({ title: 'Collection created', color: 'success', icon: 'i-lucide-check' })
  } catch {
    toast.add({ title: 'Failed to create collection', color: 'error' })
  }
}

// Send the selected request
async function handleSend(formData: { method: string; url: string; headers: { key: string; value: string }[]; body: string; bodyType: string; authConfig: string }) {
  if (!selectedRequest.value) return
  sending.value = true
  response.value = null
  try {
    // Serialize headers array to JSON string for the API
    const apiPayload = {
      ...formData,
      headers: formData.headers?.length ? JSON.stringify(formData.headers) : null,
      body: formData.body || null,
      bodyType: formData.bodyType === 'none' ? null : formData.bodyType,
      authConfig: formData.authConfig || null
    }
    // Save the current form state first (uses the actual user input, not stale API data)
    await api.updateRequest(selectedRequest.value.id, apiPayload)
    // Then send
    response.value = await api.sendRequest(selectedRequest.value.id)
  } catch (err: any) {
    toast.add({
      title: 'Request failed',
      description: err?.data?.message || err?.message || 'Unknown error',
      color: 'error'
    })
  } finally {
    sending.value = false
  }
}

// Rename a request
async function renameRequest(payload: { requestId: string, name: string }) {
  try {
    await api.updateRequest(payload.requestId, { name: payload.name })
    await loadCollections()
    await loadRequests()
    if (selectedRequestId.value === payload.requestId) {
      selectedRequest.value = await api.getRequest(payload.requestId)
    }
    toast.add({ title: 'Request renamed', color: 'success', icon: 'i-lucide-check' })
  } catch {
    toast.add({ title: 'Failed to rename request', color: 'error' })
  }
}

// Rename a collection
async function renameCollection(payload: { collectionId: string, name: string }) {
  try {
    await api.updateCollection(payload.collectionId, { name: payload.name })
    await loadCollections()
    toast.add({ title: 'Collection renamed', color: 'success', icon: 'i-lucide-check' })
  } catch {
    toast.add({ title: 'Failed to rename collection', color: 'error' })
  }
}

// Reorder requests within a collection/folder
async function handleReorder(payload: { collectionId: string, folderId: string | null, requestIds: string[] }) {
  try {
    await api.reorderCollection(payload.collectionId, payload.requestIds)
    await loadCollections()
  } catch {
    toast.add({ title: 'Failed to reorder requests', color: 'error' })
  }
}

// Move a request to a different collection/folder
async function handleMoveRequest(payload: { requestId: string, targetCollectionId: string, targetFolderId: string | null }) {
  try {
    await api.moveRequest(payload.requestId, {
      collectionId: payload.targetCollectionId,
      folderId: payload.targetFolderId
    })
    await loadCollections()
  } catch {
    toast.add({ title: 'Failed to move request', color: 'error' })
  }
}

const hasRequests = computed(() => requests.value.length > 0)

// Load on mount
onMounted(() => {
  loadRequests()
  loadCollections()
})
</script>

<template>
  <UDashboardPanel id="request-list"
                   :resizable="true"
                   class="min-w-64 max-w-sm">
    <template #header>
      <UDashboardNavbar title="Requests">
        <template #right>
          <UButton icon="i-lucide-plus"
                   size="xs"
                   variant="ghost"
                   color="neutral"
                   aria-label="New Request"
                   @click="createNewRequest()" />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <div v-if="loading || loadingCollections"
           class="flex flex-col gap-3 p-4"
           role="status"
           aria-label="Loading requests">
        <div class="flex items-center gap-2 text-sm text-muted">
          <UIcon name="i-lucide-loader-2"
                 class="size-4 animate-spin" />
          <span>Loading requests…</span>
        </div>
        <div v-for="i in 4"
             :key="i"
             class="flex flex-col gap-2">
          <USkeleton class="h-5 w-3/4" />
          <USkeleton class="h-4 w-5/6 ml-4" />
          <USkeleton class="h-4 w-2/3 ml-4" />
        </div>
      </div>
      <CollectionSidebar v-else
                         :collections="collections"
                         :active-request-id="selectedRequestId || undefined"
                         :selected-collection-id="selectedCollectionId || undefined"
                         @select-request="selectRequest"
                         @new-request="createNewRequest"
                         @new-collection="showCreateCollectionModal = true"
                         @rename-request="renameRequest"
                         @rename-collection="renameCollection"
                         @delete-request="onDeleteRequest"
                         @reorder="handleReorder"
                         @move-request="handleMoveRequest" />
    </template>
  </UDashboardPanel>

  <UDashboardPanel id="request-detail"
                   class="hidden lg:flex">
    <template #header>
      <UDashboardNavbar :title="selectedRequest?.name || 'API Request'">
        <template #right>
          <div class="flex items-center gap-2">
            <UKbd value="meta" />
            <UKbd value="K"
                  class="ml-0.5" />
          </div>
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <div v-if="!selectedRequest"
           class="flex flex-col items-center justify-center h-full gap-4">
        <div
             class="flex items-center justify-center size-16 rounded-xl bg-primary/10">
          <UIcon name="i-lucide-send"
                 class="size-8 text-primary" />
        </div>
        <div class="text-center">
          <h2 class="text-lg font-semibold text-highlighted">Select or create a
            request</h2>
          <p class="text-sm text-muted mt-1">Choose a request from the sidebar,
            or create a new one.</p>
        </div>
        <UButton label="New Request"
                 icon="i-lucide-plus"
                 size="lg"
                 @click="createNewRequest" />
      </div>

      <div v-else
           class="flex flex-col gap-6 p-4">
        <RequestBuilder :request="selectedRequest"
                        :loading="sending"
                        @send="handleSend" />

        <USeparator />

        <ResponseViewer :response="response" />
      </div>
    </template>
  </UDashboardPanel>

  <!-- Create Collection Modal -->
  <UModal v-model:open="showCreateCollectionModal"
          title="New Collection"
          description="Create a new collection to organize your requests.">
    <template #body>
      <UInput v-model="newCollectionName"
              placeholder="Collection name"
              autofocus />
    </template>
    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton variant="ghost"
                 label="Cancel"
                 @click="showCreateCollectionModal = false" />
        <UButton label="Create"
                 :disabled="!newCollectionName.trim()"
                 @click="createCollection" />
      </div>
    </template>
  </UModal>

  <!-- Delete Request Confirmation Modal -->
  <UModal v-model:open="showDeleteRequestModal"
          title="Delete Request">
    <template #body>
      <p class="text-sm">Delete '{{ deleteTarget?.requestName }}'?</p>
      <p class="text-sm text-muted mt-1">This action cannot be undone.</p>
    </template>
    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton variant="ghost"
                 label="Cancel"
                 @click="cancelDeleteRequest" />
        <UButton color="error"
                 label="Delete"
                 @click="confirmDeleteRequest" />
      </div>
    </template>
  </UModal>
</template>
