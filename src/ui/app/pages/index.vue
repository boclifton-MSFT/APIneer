<script setup lang="ts">
import type { ApiRequest as ApiRequestType, ApiResponse } from '~/composables/useApi'
import RequestBuilder from '~/components/request-builder/RequestBuilder.vue'
import ResponseViewer from '~/components/response/ResponseViewer.vue'

definePageMeta({
  layout: 'dashboard'
})

const api = useApi()
const toast = useToast()
const router = useRouter()
const route = useRoute()

const requests = ref<ApiRequestType[]>([])
const selectedRequestId = ref<string | null>(null)
const selectedRequest = ref<ApiRequestType | null>(null)
const response = ref<ApiResponse | null>(null)
const loading = ref(false)
const sending = ref(false)

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

// Create new request and navigate to it
async function createNewRequest() {
  try {
    const newReq = await api.createRequest({
      name: 'Untitled Request',
      method: 'GET',
      url: ''
    })
    await loadRequests()
    await selectRequest(newReq.id)
    toast.add({ title: 'Request created', color: 'success', icon: 'i-lucide-check' })
  } catch {
    toast.add({ title: 'Failed to create request', color: 'error' })
  }
}

// Send the selected request
async function handleSend(formData: { method: string; url: string; headers: { key: string; value: string }[]; body: string; bodyType: string }) {
  if (!selectedRequest.value) return
  sending.value = true
  response.value = null
  try {
    // Save the current form state first (uses the actual user input, not stale API data)
    await api.updateRequest(selectedRequest.value.id, formData)
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

// Delete a request
async function deleteRequest(id: string) {
  try {
    await api.deleteRequest(id)
    if (selectedRequestId.value === id) {
      selectedRequest.value = null
      selectedRequestId.value = null
      response.value = null
    }
    await loadRequests()
    toast.add({ title: 'Request deleted', color: 'success' })
  } catch {
    toast.add({ title: 'Failed to delete request', color: 'error' })
  }
}

const hasRequests = computed(() => requests.value.length > 0)

// Load on mount
onMounted(() => {
  loadRequests()
})
</script>

<template>
  <UDashboardPanel id="request-list" :resizable="true" class="min-w-64 max-w-sm">
    <template #header>
      <UDashboardNavbar title="Requests">
        <template #right>
          <UButton
            icon="i-lucide-plus"
            size="xs"
            variant="ghost"
            color="neutral"
            aria-label="New Request"
            @click="createNewRequest"
          />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <div v-if="loading" class="flex items-center justify-center p-8">
        <UIcon name="i-lucide-loader-2" class="size-5 animate-spin text-muted" />
      </div>
      <div v-else-if="!hasRequests" class="flex flex-col items-center justify-center h-full gap-4 p-8">
        <div class="flex items-center justify-center size-12 rounded-xl bg-primary/10">
          <UIcon name="i-lucide-send" class="size-6 text-primary" />
        </div>
        <div class="text-center">
          <h3 class="text-sm font-semibold text-highlighted">No requests yet</h3>
          <p class="text-xs text-muted mt-1">Create your first API request.</p>
        </div>
        <UButton
          label="New Request"
          icon="i-lucide-plus"
          size="sm"
          @click="createNewRequest"
        />
      </div>
      <div v-else class="flex flex-col">
        <button
          v-for="req in requests"
          :key="req.id"
          class="flex items-center gap-2 px-3 py-2 text-left text-sm hover:bg-elevated border-b border-default transition-colors"
          :class="{ 'bg-elevated': selectedRequestId === req.id }"
          @click="selectRequest(req.id)"
        >
          <UBadge
            :label="req.method"
            :color="req.method === 'GET' ? 'success' : req.method === 'POST' ? 'info' : req.method === 'DELETE' ? 'error' : 'warning'"
            variant="subtle"
            size="xs"
          />
          <span class="truncate flex-1">{{ req.name || req.url || 'Untitled' }}</span>
          <UButton
            icon="i-lucide-trash-2"
            size="2xs"
            variant="ghost"
            color="error"
            class="opacity-0 group-hover:opacity-100"
            @click.stop="deleteRequest(req.id)"
          />
        </button>
      </div>
    </template>
  </UDashboardPanel>

  <UDashboardPanel id="request-detail" class="hidden lg:flex">
    <template #header>
      <UDashboardNavbar :title="selectedRequest?.name || 'API Request'">
        <template #right>
          <div class="flex items-center gap-2">
            <UKbd value="meta" />
            <UKbd value="K" class="ml-0.5" />
          </div>
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <div v-if="!selectedRequest" class="flex flex-col items-center justify-center h-full gap-4">
        <div class="flex items-center justify-center size-16 rounded-xl bg-primary/10">
          <UIcon name="i-lucide-send" class="size-8 text-primary" />
        </div>
        <div class="text-center">
          <h2 class="text-lg font-semibold text-highlighted">Select or create a request</h2>
          <p class="text-sm text-muted mt-1">Choose a request from the sidebar, or create a new one.</p>
        </div>
        <UButton
          label="New Request"
          icon="i-lucide-plus"
          size="lg"
          @click="createNewRequest"
        />
      </div>

      <div v-else class="flex flex-col gap-6 p-4">
        <RequestBuilder
          :request="selectedRequest"
          :loading="sending"
          @send="handleSend"
        />

        <USeparator />

        <ResponseViewer :response="response" />
      </div>
    </template>
  </UDashboardPanel>
</template>
