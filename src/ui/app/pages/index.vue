<script setup lang="ts">
definePageMeta({
  layout: 'dashboard'
})
</script>

<template>
  <UDashboardPanel>
    <template #header>
      <UDashboardNavbar title="API Requests">
        <template #right>
          <UKbd value="meta" />
          <UKbd value="K" class="ml-0.5" />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <div class="flex flex-col items-center justify-center h-full gap-4">
        <div class="flex items-center justify-center size-16 rounded-xl bg-primary/10">
          <UIcon name="i-lucide-send" class="size-8 text-primary" />
        </div>
        <div class="text-center">
          <h2 class="text-lg font-semibold text-highlighted">No requests yet</h2>
          <p class="text-sm text-muted mt-1">Create your first API request to get started.</p>
        </div>
        <UButton
          label="New Request"
          icon="i-lucide-plus"
          size="lg"
        />
      </div>
    </template>
  </UDashboardPanel>
</template>
