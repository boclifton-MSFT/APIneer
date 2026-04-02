<script setup lang="ts">
import type { McpServerConfig } from '~/composables/useApi'
import type { ConnectionState } from '~/composables/useMcpHelpers'

defineOptions({ name: 'McpServerList' })

const props = defineProps<{
  servers: McpServerConfig[]
  loading: boolean
  selectedServerId: string | null
  connectionState: ConnectionState
}>()

const emit = defineEmits<{
  select: [id: string]
  delete: [id: string]
  add: []
}>()

const statusDotClass = computed(() => {
  switch (props.connectionState) {
    case 'connected': return 'bg-green-500'
    case 'connecting': return 'bg-yellow-500 animate-pulse'
    default: return 'bg-red-500'
  }
})
</script>

<template>
  <UDashboardPanel id="mcp-server-list" :resizable="true" class="min-w-64 max-w-sm">
    <template #header>
      <UDashboardNavbar title="MCP Servers">
        <template #right>
          <UButton
            icon="i-lucide-plus"
            size="xs"
            variant="ghost"
            color="neutral"
            aria-label="New Server"
            @click="emit('add')"
          />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <div v-if="loading" class="flex items-center justify-center p-8">
        <UIcon name="i-lucide-loader-2" class="size-5 animate-spin text-muted" />
      </div>

      <div v-else-if="servers.length === 0" class="flex flex-col items-center justify-center h-full gap-4 p-8">
        <div class="flex items-center justify-center size-12 rounded-xl bg-primary/10">
          <UIcon name="i-lucide-plug" class="size-6 text-primary" />
        </div>
        <div class="text-center">
          <h3 class="text-sm font-semibold text-highlighted">No MCP servers configured</h3>
          <p class="text-xs text-muted mt-1">Add one to get started.</p>
        </div>
        <UButton label="New Server" icon="i-lucide-plus" size="sm" @click="emit('add')" />
      </div>

      <div v-else class="flex flex-col">
        <button
          v-for="server in servers"
          :key="server.id"
          class="server-item flex items-center justify-between px-3 py-2.5 text-left text-sm border-b border-default transition-colors"
          :class="{ 'bg-elevated': selectedServerId === server.id }"
          @click="emit('select', server.id)"
        >
          <div class="flex items-center gap-2 min-w-0">
            <UIcon name="i-lucide-server" class="size-4 text-muted shrink-0" />
            <span class="truncate">{{ server.name }}</span>
            <span
              class="transport-badge shrink-0"
              :class="server.transportType === 'stdio' ? 'transport-stdio' : 'transport-http'"
            >
              {{ server.transportType === 'stdio' ? 'stdio' : 'HTTP' }}
            </span>
          </div>
          <div class="flex items-center gap-1 shrink-0">
            <div
              v-if="selectedServerId === server.id"
              class="size-2 rounded-full shrink-0"
              :class="statusDotClass"
            />
            <UButton
              icon="i-lucide-trash-2"
              size="xs"
              variant="ghost"
              color="error"
              class="delete-icon"
              @click.stop="emit('delete', server.id)"
            />
          </div>
        </button>
      </div>
    </template>
  </UDashboardPanel>
</template>
