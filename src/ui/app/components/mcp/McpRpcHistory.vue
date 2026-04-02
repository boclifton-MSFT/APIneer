<script setup lang="ts">
import type { RpcHistoryEntry } from '~/composables/useMcpRpcHistory'

defineOptions({ name: 'McpRpcHistory' })

const { rpcHistory } = useMcpRpcHistory()

const showHistory = ref(false)
const expandedHistoryId = ref<number | null>(null)

function toggleEntry(id: number) {
  expandedHistoryId.value = expandedHistoryId.value === id ? null : id
}

function formatTime(date: Date): string {
  return date.toLocaleTimeString('en-US', { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' })
}
</script>

<template>
  <div class="history-section">
    <button class="history-toggle" @click="showHistory = !showHistory">
      <UIcon :name="showHistory ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right'" class="size-4" />
      <UIcon name="i-lucide-scroll-text" class="size-4 text-muted" />
      <span>Request History</span>
      <span v-if="rpcHistory.length > 0" class="history-count">{{ rpcHistory.length }}</span>
    </button>

    <div v-if="showHistory" class="history-list">
      <div v-if="rpcHistory.length === 0" class="cap-empty">
        <p class="text-xs text-muted">No requests yet.</p>
      </div>
      <div v-for="entry in rpcHistory" :key="entry.id" class="history-entry">
        <button class="history-entry-header" @click="toggleEntry(entry.id)">
          <UIcon :name="expandedHistoryId === entry.id ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right'" class="size-3.5 shrink-0 text-muted" />
          <span class="history-time">{{ formatTime(entry.timestamp) }}</span>
          <span class="history-method">{{ entry.method }}</span>
          <span class="history-status" :class="entry.status === 'error' ? 'status-error' : 'status-success'">
            {{ entry.status }}
          </span>
        </button>
        <div v-if="expandedHistoryId === entry.id" class="history-detail">
          <div class="history-json-block">
            <span class="text-xs font-semibold text-muted uppercase">Request</span>
            <pre class="cap-code">{{ JSON.stringify(entry.request, null, 2) }}</pre>
          </div>
          <div class="history-json-block">
            <span class="text-xs font-semibold text-muted uppercase">Response</span>
            <pre class="cap-code">{{ JSON.stringify(entry.response, null, 2) }}</pre>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
