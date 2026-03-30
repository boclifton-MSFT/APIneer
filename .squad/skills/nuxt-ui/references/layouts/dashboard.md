# Dashboard Layout

Build admin interfaces with resizable sidebars, multi-panel layouts, and toolbars.

## Component tree

```
UApp
└── NuxtLayout (dashboard)
    └── UDashboardGroup
        ├── UDashboardSidebar
        │   ├── #header (logo, search button)
        │   ├── #default (navigation) — receives { collapsed } slot prop
        │   └── #footer (user menu)
        └── NuxtPage
            └── UDashboardPanel
                ├── #header → UDashboardNavbar + UDashboardToolbar
                ├── #body (scrollable content)
                └── #footer (optional)
```

## Layout

```vue [layouts/dashboard.vue]
<script setup lang="ts">
import type { NavigationMenuItem } from '@nuxt/ui'

const items = computed<NavigationMenuItem[]>(() => [{
  label: 'Home',
  icon: 'i-lucide-house',
  to: '/dashboard'
}, {
  label: 'Inbox',
  icon: 'i-lucide-inbox',
  to: '/dashboard/inbox'
}, {
  label: 'Users',
  icon: 'i-lucide-users',
  to: '/dashboard/users'
}, {
  label: 'Settings',
  icon: 'i-lucide-settings',
  to: '/dashboard/settings'
}])
</script>

<template>
  <UDashboardGroup>
    <UDashboardSidebar collapsible resizable>
      <template #header="{ collapsed }">
        <UDashboardSearchButton :collapsed="collapsed" />
      </template>

      <template #default="{ collapsed }">
        <UNavigationMenu
          :items="items"
          orientation="vertical"
          :ui="{ link: collapsed ? 'justify-center' : undefined }"
        />
      </template>

      <template #footer="{ collapsed }">
        <UButton
          :icon="collapsed ? 'i-lucide-log-out' : undefined"
          :label="collapsed ? undefined : 'Sign out'"
          color="neutral"
          variant="ghost"
          block
        />
      </template>
    </UDashboardSidebar>

    <slot />
  </UDashboardGroup>
</template>
```

## Key components

### DashboardGroup
Root layout wrapper. Props: `storage` ('cookie'|'localStorage'|false), `storage-key`, `unit` ('percentages'|'pixels').

### DashboardSidebar
Props: `resizable`, `collapsible`, `side` ('left'|'right'), `mode` ('modal'|'slideover'|'drawer'). Slots receive `{ collapsed }`.

### DashboardPanel
Content panel with `#header`, `#body` (scrollable), `#footer`, and `#default` (raw) slots. Props: `id` (required for multi-panel), `resizable`.

### DashboardNavbar / DashboardToolbar
Navbar has `#left`, `#default`, `#right` slots and a `title` prop.

## Multi-panel (list-detail)

```vue
<UDashboardPanel id="inbox-list" resizable>
  <template #header><UDashboardNavbar title="Inbox" /></template>
  <template #body><!-- list --></template>
</UDashboardPanel>

<UDashboardPanel id="inbox-detail" class="hidden lg:flex">
  <template #header><UDashboardNavbar title="Message" /></template>
  <template #body><!-- detail --></template>
</UDashboardPanel>
```
