# Editor Layout

Build a rich text editor with toolbars, slash commands, mentions, and drag-and-drop.

## Key components

- `UEditor` — Rich text editor (`v-model` accepts JSON, HTML, or markdown via `content-type` prop)
- `UEditorToolbar` — Toolbar with `layout`: `'fixed'` (default), `'bubble'` (on selection), `'floating'` (on empty lines)
- `UEditorDragHandle` — Block drag-and-drop handle
- `UEditorSuggestionMenu` — Slash command menu
- `UEditorMentionMenu` — @ mention menu
- `UEditorEmojiMenu` — Emoji picker

## Toolbar modes

```vue
<!-- Fixed (default) -->
<UEditor v-model="content"><UEditorToolbar /></UEditor>

<!-- Bubble (appears on text selection) -->
<UEditor v-model="content"><UEditorToolbar layout="bubble" /></UEditor>

<!-- Floating (appears on empty lines) -->
<UEditor v-model="content"><UEditorToolbar layout="floating" /></UEditor>
```

## Content types

```vue
<UEditor v-model="jsonContent" />
<UEditor v-model="htmlContent" content-type="html" />
<UEditor v-model="markdownContent" content-type="markdown" />
```
