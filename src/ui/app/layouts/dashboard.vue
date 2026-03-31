<script setup lang="ts">
import type { NavigationMenuItem } from '@nuxt/ui'

const items = computed<NavigationMenuItem[]>(() => [
  {
    label: 'Requests',
    icon: 'i-lucide-send',
    to: '/'
  },
  {
    label: 'Collections',
    icon: 'i-lucide-folder-open',
    to: '/collections'
  },
  {
    label: 'History',
    icon: 'i-lucide-history',
    to: '/history'
  },
  {
    label: 'Environments',
    icon: 'i-lucide-layers',
    to: '/environments'
  }
])
</script>

<template>
  <UDashboardGroup>
    <UDashboardSidebar collapsible resizable>
      <template #header="{ collapsed }">
        <div class="flex items-center gap-2 p-2">
          <div class="flex items-center justify-center size-8 rounded-md bg-primary/10">
            <UIcon name="i-lucide-zap" class="size-5 text-primary" />
          </div>
          <div v-if="!collapsed" class="flex flex-col">
            <span class="font-bold text-base tracking-tight text-highlighted">APIneer</span>
            <span class="text-xs text-dimmed">API Platform</span>
          </div>
        </div>
      </template>

      <template #default="{ collapsed }">
        <UNavigationMenu
          :items="items"
          orientation="vertical"
          :ui="{ link: collapsed ? 'justify-center' : undefined }"
        />
      </template>

      <template #footer="{ collapsed }">
        <div class="flex flex-col gap-1">
          <UColorModeButton
            :size="collapsed ? 'md' : 'md'"
            :variant="'ghost'"
            color="neutral"
            class="w-full"
            :class="collapsed ? 'justify-center' : ''"
          />
          <UButton
            :icon="collapsed ? 'i-lucide-settings' : undefined"
            :label="collapsed ? undefined : 'Settings'"
            color="neutral"
            variant="ghost"
            block
          />
        </div>
      </template>
    </UDashboardSidebar>

    <slot />
  </UDashboardGroup>
</template>
