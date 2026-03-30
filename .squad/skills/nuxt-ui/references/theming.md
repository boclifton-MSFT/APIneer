# Theming

## Semantic colors

| Color | Default | Purpose |
|---|---|---|
| `primary` | green | CTAs, active states, brand |
| `secondary` | blue | Secondary actions |
| `success` | green | Success messages |
| `info` | blue | Informational |
| `warning` | yellow | Warnings |
| `error` | red | Errors, destructive actions |
| `neutral` | slate | Text, borders, disabled |

## Configuring colors

```ts
// Nuxt — app.config.ts
export default defineAppConfig({
  ui: {
    colors: {
      primary: 'indigo',
      secondary: 'violet',
      success: 'emerald',
      error: 'rose',
      neutral: 'zinc'
    }
  }
})
```

## CSS utilities

### Text

| Class | Use |
|---|---|
| `text-default` | Body text |
| `text-muted` | Secondary text |
| `text-dimmed` | Placeholders, hints |
| `text-toned` | Subtitles |
| `text-highlighted` | Headings, emphasis |
| `text-inverted` | On dark/light backgrounds |

### Background

| Class | Use |
|---|---|
| `bg-default` | Page background |
| `bg-muted` | Subtle sections |
| `bg-elevated` | Cards, modals |
| `bg-accented` | Hover states |
| `bg-inverted` | Inverted sections |

### Border

| Class | Use |
|---|---|
| `border-default` | Default borders |
| `border-muted` | Subtle borders |
| `border-accented` | Emphasized borders |
| `border-inverted` | Inverted borders |

### CSS variables

```css
:root {
  --ui-radius: 0.25rem;
  --ui-container: 80rem;
  --ui-header-height: 4rem;
  --ui-primary: var(--ui-color-primary-500);
}

.dark {
  --ui-primary: var(--ui-color-primary-400);
}
```

## Component theme customization

### Override priority

**`ui` prop / `class` prop > global config > theme defaults**

### Understanding the generated theme

Every component's full resolved theme is generated at build time:
- **Nuxt**: `.nuxt/ui/<component>.ts`
- **Vue**: `node_modules/.nuxt-ui/ui/<component>.ts`

### Global config

```ts
// Nuxt — app.config.ts
export default defineAppConfig({
  ui: {
    button: {
      slots: {
        base: 'font-bold rounded-full'
      },
      defaultVariants: {
        color: 'neutral',
        variant: 'outline'
      }
    }
  }
})
```

### Per-instance (`ui` prop)

```vue
<UButton :ui="{ base: 'font-mono', trailingIcon: 'size-3 rotate-90' }" />
<UCard :ui="{ root: 'shadow-xl', body: 'p-8' }" />
```

## Adding custom colors

Define all 11 shades in CSS:

```css
@theme static {
  --color-brand-50: #fef2f2;
  /* ... all 11 shades (50–950) ... */
  --color-brand-950: #450a0a;
}
```

Then assign: `ui: { colors: { primary: 'brand' } }`

## Dark mode

```ts
const colorMode = useColorMode()
colorMode.preference = 'dark' // 'light', 'dark', 'system'
```

## Fonts

```css
@theme {
  --font-sans: 'Public Sans', system-ui, sans-serif;
  --font-mono: 'JetBrains Mono', monospace;
}
```
