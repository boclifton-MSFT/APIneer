# Components

125+ Vue components powered by Tailwind CSS and Reka UI. For any component's theme slots, read the generated theme file (Nuxt: `.nuxt/ui/<component>.ts`, Vue: `node_modules/.nuxt-ui/ui/<component>.ts`).

## Layout

Core structural components for organizing your application's layout.

| Component | Purpose |
|---|---|
| `UApp` | **Required** root wrapper for toasts, tooltips, overlays |
| `UHeader` | Responsive header with mobile menu (`#title`, `#default`, `#right`, `#body`) |
| `UFooter` | Footer (`#left`, `#default`, `#right`, `#top`, `#bottom`) |
| `UFooterColumns` | Multi-column footer with link groups |
| `UMain` | Main content area (respects `--ui-header-height`) |
| `UContainer` | Centered max-width container (`--ui-container`) |

## Element

Essential UI building blocks.

| Component | Key props |
|---|---|
| `UButton` | `label`, `icon`, `color`, `variant`, `size`, `loading`, `disabled`, `to` |
| `UBadge` | `label`, `color`, `variant`, `size` |
| `UAvatar` | `src`, `alt`, `icon`, `text`, `size` |
| `UAvatarGroup` | `max`, `size` — wraps multiple `UAvatar` |
| `UIcon` | `name`, `size` |
| `UCard` | `variant` — slots: `#header`, `#default`, `#footer` |
| `UAlert` | `title`, `description`, `icon`, `color`, `variant`, `close` |
| `UBanner` | `title`, `icon`, `close` — sticky top banner |
| `UChip` | `color`, `size`, `position` — notification dot on children |
| `UKbd` | `value` — keyboard key display |
| `USeparator` | `label`, `icon`, `orientation`, `type` |
| `USkeleton` | `class` — loading placeholder |
| `UProgress` | `value`, `max`, `color`, `size` |
| `UCalendar` | `v-model`, `range` (boolean), `multiple` (boolean) |
| `UCollapsible` | `v-model:open` — animated expand/collapse |
| `UFieldGroup` | Groups form inputs horizontally/vertically |

## Form

| Component | Key props |
|---|---|
| `UInput` | `v-model`, `type`, `placeholder`, `icon`, `loading` |
| `UTextarea` | `v-model`, `rows`, `autoresize`, `maxrows` |
| `USelect` | `v-model`, `items` (flat `T[]` or grouped `T[][]`), `placeholder` |
| `USelectMenu` | `v-model`, `items` (flat `T[]` or grouped `T[][]`), `searchable`, `multiple` |
| `UInputMenu` | `v-model`, `items` (flat `T[]` or grouped `T[][]`), `searchable` — autocomplete |
| `UInputNumber` | `v-model`, `min`, `max`, `step` |
| `UInputDate` | `v-model`, `range` (boolean for range selection), `locale` |
| `UInputTime` | `v-model`, `hour-cycle` (12/24), `granularity` |
| `UInputTags` | `v-model`, `max`, `placeholder` |
| `UPinInput` | `v-model`, `length`, `type`, `mask` |
| `UCheckbox` | `v-model`, `label`, `description` |
| `UCheckboxGroup` | `v-model`, `items`, `orientation` |
| `URadioGroup` | `v-model`, `items`, `orientation` |
| `USwitch` | `v-model`, `label`, `on-icon`, `off-icon` |
| `USlider` | `v-model`, `min`, `max`, `step` |
| `UColorPicker` | `v-model`, `format` (hex/rgb/hsl/cmyk/lab), `size` |
| `UFileUpload` | `v-model`, `accept`, `multiple`, `variant` (area/button) |
| `UForm` | `schema`, `state`, `@submit` — validation wrapper |
| `UFormField` | `name`, `label`, `description`, `hint`, `required` |

### Form validation

Uses Standard Schema — works with Zod, Valibot, Yup, or Joi.

```vue
<script setup lang="ts">
import { z } from 'zod'

const schema = z.object({
  email: z.string().email('Invalid email'),
  password: z.string().min(8, 'Min 8 characters')
})

type Schema = z.output<typeof schema>
const state = reactive<Partial<Schema>>({ email: '', password: '' })
const form = ref()

async function onSubmit() {
  await form.value.validate()
}
</script>

<template>
  <UForm ref="form" :schema="schema" :state="state" @submit="onSubmit">
    <UFormField name="email" label="Email" required>
      <UInput v-model="state.email" type="email" />
    </UFormField>

    <UFormField name="password" label="Password" required>
      <UInput v-model="state.password" type="password" />
    </UFormField>

    <UButton type="submit">Submit</UButton>
  </UForm>
</template>
```

## Data

| Component | Key props |
|---|---|
| `UTable` | `data`, `columns`, `loading`, `sticky` |
| `UAccordion` | `items`, `type` (single/multiple), `collapsible` |
| `UCarousel` | `items`, `orientation`, `arrows`, `dots` |
| `UTimeline` | `items` — vertical timeline |
| `UTree` | `items` — hierarchical tree |
| `UUser` | `name`, `description`, `avatar` — user display |
| `UEmpty` | `icon`, `title`, `description` — empty state |
| `UScrollArea` | Custom scrollbar wrapper |

## Navigation

| Component | Key props |
|---|---|
| `UNavigationMenu` | `items` (flat `T[]` or grouped `T[][]`), `orientation` (horizontal/vertical) |
| `UBreadcrumb` | `items` |
| `UTabs` | `items`, `orientation`, `variant` |
| `UStepper` | `items`, `orientation`, `color` |
| `UPagination` | `v-model`, `total`, `items-per-page` |
| `ULink` | `to`, `active`, `inactive` — styled NuxtLink |
| `UCommandPalette` | `v-model:open`, `groups` (`{ id, label, items }[]`), `placeholder` |

## Overlay

| Component | Key props |
|---|---|
| `UModal` | `v-model:open`, `title`, `description`, `fullscreen`, `scrollable` |
| `USlideover` | `v-model:open`, `title`, `side` (left/right/top/bottom) |
| `UDrawer` | `v-model:open`, `title`, `handle` |
| `UPopover` | `arrow`, `content: { side, align }`, `openDelay`, `closeDelay` |
| `UTooltip` | `text`, `content: { side }`, `delayDuration` |
| `UDropdownMenu` | `items` (flat `T[]` or grouped `T[][]` with separators, supports nested `children`) |
| `UContextMenu` | `items` (flat `T[]` or grouped `T[][]`) — right-click menu |
| `UToast` | Used via `useToast()` composable |

## Dashboard

| Component | Purpose |
|---|---|
| `UDashboardGroup` | Root wrapper — manages sidebar state |
| `UDashboardSidebar` | Resizable/collapsible sidebar (`#header`, `#default`, `#footer`) |
| `UDashboardPanel` | Content panel (`#header`, `#body`, `#footer`) |
| `UDashboardNavbar` | Panel navbar (`#left`, `#default`, `#right`) |
| `UDashboardToolbar` | Toolbar for filters/actions |
| `UDashboardSearch` | Command palette for dashboards |
| `UDashboardSearchButton` | Search trigger button |
| `UDashboardSidebarToggle` | Mobile sidebar toggle |
| `UDashboardSidebarCollapse` | Desktop collapse button |
| `UDashboardResizeHandle` | Custom resize handle |

## Color Mode

| Component | Purpose |
|---|---|
| `UColorModeButton` | Toggle light/dark button |
| `UColorModeSwitch` | Toggle light/dark switch |
| `UColorModeSelect` | Dropdown selector |
