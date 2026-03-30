# Page Layout

Build public-facing pages — landing, blog, changelog, pricing.

## App shell

```vue [app.vue]
<template>
  <UApp>
    <UHeader>
      <template #title><Logo class="h-6 w-auto" /></template>
      <UNavigationMenu :items="items" />
      <template #right>
        <UColorModeButton />
        <UButton label="Sign in" color="neutral" variant="ghost" />
      </template>
    </UHeader>

    <UMain><NuxtPage /></UMain>

    <UFooter>
      <template #left>
        <p class="text-muted text-sm">Copyright © {{ new Date().getFullYear() }}</p>
      </template>
    </UFooter>
  </UApp>
</template>
```

## Key components

- `UPageHero` — Hero with title, description, links, and optional media
- `UPageSection` — Content section with headline, title, description, and features grid
- `UPageCTA` — Call to action block
- `UPageHeader` — Page title and description
- `UPageBody` — Main content area with prose styling
- `UPageGrid` / `UPageColumns` — Grid layouts
- `UPageCard` — Content card for grids
- `UBlogPosts` / `UBlogPost` — Blog listing
- `UPricingPlans` / `UPricingTable` — Pricing displays
