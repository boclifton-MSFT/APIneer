# Chat Layout

Build AI chat interfaces with message streams, reasoning, tool calling, and Vercel AI SDK integration.

## Component tree

```
UApp
└── NuxtLayout (dashboard)
    └── UDashboardGroup
        ├── UDashboardSidebar (conversations)
        └── NuxtPage
            └── UDashboardPanel
                ├── #header → UDashboardNavbar
                ├── #body → UContainer → UChatMessages
                └── #footer → UContainer → UChatPrompt + UChatPromptSubmit
```

## Setup

```bash
pnpm add ai @ai-sdk/gateway @ai-sdk/vue
```

## Key components

- `UChatMessages` — Scrollable message list with auto-scroll. Props: `messages`, `status`
- `UChatMessage` — Individual message bubble. Props: `message`, `side`
- `UChatReasoning` — Collapsible reasoning block. Props: `text`, `streaming`
- `UChatTool` — Tool invocation status block. Props: `text`, `streaming`, `variant`
- `UChatPrompt` — Enhanced textarea. Props: `v-model`, `error`, `variant`
- `UChatPromptSubmit` — Submit button with status handling
- `UChatPalette` — Chat layout for overlays
