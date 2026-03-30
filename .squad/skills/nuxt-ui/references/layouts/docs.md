# Docs Layout

Build documentation sites with sidebar navigation, table of contents, and surround links.

> Requires `@nuxt/content` module.

## Component tree

```
UApp
├── UHeader
├── UMain
│   └── NuxtLayout (docs)
│       └── UPage
│           ├── #left → UPageAside → UContentNavigation
│           └── NuxtPage
│               ├── UPageHeader
│               ├── UPageBody → ContentRenderer + UContentSurround
│               └── #right → UContentToc
└── UFooter
```

## Key components

- `UPage` — Multi-column grid layout with `#left`, `#default`, `#right` slots
- `UPageAside` — Sticky sidebar wrapper (visible from `lg` breakpoint)
- `UContentNavigation` — Sidebar navigation tree
- `UContentToc` — Table of contents
- `UContentSurround` — Prev/next links
- `UContentSearch` / `UContentSearchButton` — Search command palette
