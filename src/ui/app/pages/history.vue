<script setup lang="ts">
import type { HistoryEntry } from '~/composables/useApi'
import { methodColor, statusSeverity } from '~/composables/useHttpColors'

definePageMeta({
  layout: 'dashboard'
})

const api = useApi()
const toast = useToast()
const router = useRouter()

const history = ref<HistoryEntry[]>([])
const loading = ref(false)
const selectedEntry = ref<HistoryEntry | null>(null)

const columns = [
  { accessorKey: 'method', header: 'Method' },
  { accessorKey: 'url', header: 'URL' },
  { accessorKey: 'responseStatus', header: 'Status' },
  { accessorKey: 'responseTimeMs', header: 'Time' },
  { accessorKey: 'executedAt', header: 'Date' }
]

async function loadHistory() {
  loading.value = true
  try {
    history.value = await api.getHistory()
  } catch {
    history.value = []
  } finally {
    loading.value = false
  }
}

async function clearHistory() {
  try {
    await api.clearHistory()
    history.value = []
    selectedEntry.value = null
    toast.add({ title: 'History cleared', color: 'success', icon: 'i-lucide-check' })
  } catch {
    toast.add({ title: 'Failed to clear history', color: 'error' })
  }
}

function selectEntry(entry: HistoryEntry) {
  selectedEntry.value = entry
}

function formatDate(dateStr: string) {
  try {
    return new Date(dateStr).toLocaleString()
  } catch {
    return dateStr
  }
}

function formatTime(ms: number) {
  if (ms < 1000) return `${ms}ms`
  return `${(ms / 1000).toFixed(2)}s`
}

onMounted(() => {
  loadHistory()
})
</script>

<template>
  <UDashboardPanel>
    <template #header>
      <UDashboardNavbar title="History">
        <template #right>
          <UButton
            v-if="history.length > 0"
            label="Clear History"
            icon="i-lucide-trash-2"
            color="error"
            variant="soft"
            size="sm"
            @click="clearHistory"
          />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <div v-if="loading" class="flex items-center justify-center p-12">
        <UIcon name="i-lucide-loader-2" class="size-6 animate-spin text-muted" />
      </div>

      <div v-else-if="history.length === 0" class="flex flex-col items-center justify-center h-full gap-4 p-8">
        <div class="flex items-center justify-center size-16 rounded-xl bg-primary/10">
          <UIcon name="i-lucide-history" class="size-8 text-primary" />
        </div>
        <div class="text-center">
          <h2 class="text-lg font-semibold text-highlighted">No history yet</h2>
          <p class="text-sm text-muted mt-1">Send an API request to see it here.</p>
        </div>
        <UButton
          label="Go to Requests"
          icon="i-lucide-send"
          size="lg"
          @click="router.push('/')"
        />
      </div>

      <div v-else class="p-4">
        <UTable :data="history" :columns="columns" :loading="loading" class="w-full">
          <template #method-cell="{ row }">
            <UBadge :label="row.original.method" :color="methodColor(row.original.method)" variant="subtle" size="sm" />
          </template>
          <template #url-cell="{ row }">
            <span class="font-mono text-xs truncate max-w-xs block">{{ row.original.url }}</span>
          </template>
          <template #responseStatus-cell="{ row }">
            <UBadge :label="String(row.original.responseStatus)" :color="statusSeverity(row.original.responseStatus)" variant="subtle" size="sm" />
          </template>
          <template #responseTimeMs-cell="{ row }">
            <span class="text-sm text-muted">{{ formatTime(row.original.responseTimeMs) }}</span>
          </template>
          <template #executedAt-cell="{ row }">
            <span class="text-sm text-muted">{{ formatDate(row.original.executedAt) }}</span>
          </template>
        </UTable>

        <!-- Detail panel -->
        <UModal v-if="selectedEntry" v-model:open="selectedEntry" title="Request Details">
          <template #body>
            <div class="space-y-3">
              <div class="flex items-center gap-2">
                <UBadge :label="selectedEntry.method" :color="methodColor(selectedEntry.method)" variant="subtle" />
                <span class="font-mono text-sm">{{ selectedEntry.url }}</span>
              </div>
              <div class="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <span class="text-muted">Status:</span>
                  <UBadge :label="`${selectedEntry.responseStatus} ${selectedEntry.statusText}`" :color="statusSeverity(selectedEntry.responseStatus)" variant="subtle" class="ml-2" />
                </div>
                <div>
                  <span class="text-muted">Time:</span>
                  <span class="ml-2">{{ formatTime(selectedEntry.responseTimeMs) }}</span>
                </div>
                <div>
                  <span class="text-muted">Size:</span>
                  <span class="ml-2">{{ selectedEntry.responseSizeBytes }} bytes</span>
                </div>
                <div>
                  <span class="text-muted">Date:</span>
                  <span class="ml-2">{{ formatDate(selectedEntry.executedAt) }}</span>
                </div>
              </div>
            </div>
          </template>
          <template #footer>
            <UButton variant="ghost" label="Close" @click="selectedEntry = null" />
          </template>
        </UModal>
      </div>
    </template>
  </UDashboardPanel>
</template>
