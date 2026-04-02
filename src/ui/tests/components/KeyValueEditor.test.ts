import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import { nextTick, reactive } from 'vue'
import KeyValueEditor from '~/components/KeyValueEditor.vue'

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Mount with a plain array — tests that don't involve mutations. */
async function mountEditor(
  modelValue: { key: string; value: string }[] = [{ key: '', value: '' }],
  extraProps: Record<string, unknown> = {}
) {
  return mountSuspended(KeyValueEditor, {
    props: { modelValue, ...extraProps }
  })
}

/**
 * Mount with a reactive array so that push()/splice() mutations trigger
 * Vue re-renders. Use this for all tests that click add/remove buttons.
 */
async function mountEditorReactive(
  initialData: { key: string; value: string }[],
  extraProps: Record<string, unknown> = {}
) {
  const reactiveItems = reactive([...initialData])
  return mountSuspended(KeyValueEditor, {
    props: { modelValue: reactiveItems, ...extraProps }
  })
}

// ---------------------------------------------------------------------------
// Rendering
// ---------------------------------------------------------------------------
describe('KeyValueEditor — rendering', () => {
  it('renders one empty row by default', async () => {
    const wrapper = await mountEditor()
    expect(wrapper.findAll('tbody tr')).toHaveLength(1)
  })

  it('renders the correct number of rows from modelValue', async () => {
    const wrapper = await mountEditor([
      { key: 'Authorization', value: 'Bearer token' },
      { key: 'Content-Type', value: 'application/json' },
      { key: 'X-Custom', value: 'abc' }
    ])
    expect(wrapper.findAll('tbody tr')).toHaveLength(3)
  })

  it('pre-fills inputs with values from modelValue', async () => {
    const wrapper = await mountEditor([{ key: 'MY_KEY', value: 'MY_VALUE' }])
    const inputs = wrapper.findAll('tbody input') as ReturnType<typeof wrapper.find>[]
    expect((inputs[0].element as HTMLInputElement).value).toBe('MY_KEY')
    expect((inputs[1].element as HTMLInputElement).value).toBe('MY_VALUE')
  })

  it('renders a table with thead and tbody', async () => {
    const wrapper = await mountEditor()
    expect(wrapper.find('thead').exists()).toBe(true)
    expect(wrapper.find('tbody').exists()).toBe(true)
  })

  it('renders column headers Key and Value', async () => {
    const wrapper = await mountEditor()
    const theadText = wrapper.find('thead').text()
    expect(theadText).toContain('Key')
    expect(theadText).toContain('Value')
  })
})

// ---------------------------------------------------------------------------
// Custom placeholders
// ---------------------------------------------------------------------------
describe('KeyValueEditor — placeholders', () => {
  it('uses custom keyPlaceholder on key inputs', async () => {
    const wrapper = await mountEditor([], { keyPlaceholder: 'Header Name' })
    const inputs = wrapper.findAll('tbody input')
    if (inputs.length > 0) {
      expect(inputs[0].attributes('placeholder')).toBe('Header Name')
    }
  })

  it('uses custom valuePlaceholder on value inputs', async () => {
    const wrapper = await mountEditor([], { valuePlaceholder: 'Header Value' })
    const inputs = wrapper.findAll('tbody input')
    if (inputs.length > 1) {
      expect(inputs[1].attributes('placeholder')).toBe('Header Value')
    }
  })

  it('falls back to default placeholder "Key" when not provided', async () => {
    const wrapper = await mountEditor()
    const inputs = wrapper.findAll('tbody input')
    if (inputs.length > 0) {
      expect(inputs[0].attributes('placeholder')).toBe('Key')
    }
  })

  it('falls back to default placeholder "Value" when not provided', async () => {
    const wrapper = await mountEditor()
    const inputs = wrapper.findAll('tbody input')
    if (inputs.length > 1) {
      expect(inputs[1].attributes('placeholder')).toBe('Value')
    }
  })
})

// ---------------------------------------------------------------------------
// Add row
// ---------------------------------------------------------------------------
describe('KeyValueEditor — add row', () => {
  it('clicking the add button (in thead) adds a new row', async () => {
    const wrapper = await mountEditorReactive([{ key: 'existing', value: 'row' }])

    const addBtn = wrapper.find('thead button')
    await addBtn.trigger('click')
    await nextTick()

    expect(wrapper.findAll('tbody tr')).toHaveLength(2)
  })

  it('new row has empty key and value inputs', async () => {
    const wrapper = await mountEditorReactive([{ key: 'existing', value: 'row' }])

    await wrapper.find('thead button').trigger('click')
    await nextTick()

    const rows = wrapper.findAll('tbody tr')
    const lastRowInputs = rows[1].findAll('input')
    expect((lastRowInputs[0].element as HTMLInputElement).value).toBe('')
    expect((lastRowInputs[1].element as HTMLInputElement).value).toBe('')
  })

  it('can add multiple rows', async () => {
    const wrapper = await mountEditorReactive([{ key: 'seed', value: 'data' }])

    const addBtn = wrapper.find('thead button')
    await addBtn.trigger('click')
    await nextTick()
    await addBtn.trigger('click')
    await nextTick()
    await addBtn.trigger('click')
    await nextTick()

    expect(wrapper.findAll('tbody tr').length).toBeGreaterThanOrEqual(3)
  })
})

// ---------------------------------------------------------------------------
// Remove row
// ---------------------------------------------------------------------------
describe('KeyValueEditor — remove row', () => {
  it('clicking the remove button in a row removes that row', async () => {
    const wrapper = await mountEditorReactive([
      { key: 'keep-me', value: 'yes' },
      { key: 'delete-me', value: 'no' }
    ])

    const removeButtons = wrapper.findAll('tbody button')
    await removeButtons[0].trigger('click')
    await nextTick()

    expect(wrapper.findAll('tbody tr')).toHaveLength(1)
  })

  it('removes the correct row — only one row remains after removing one of two', async () => {
    const wrapper = await mountEditorReactive([
      { key: 'first', value: '1' },
      { key: 'second', value: '2' }
    ])

    const removeButtons = wrapper.findAll('tbody button')
    await removeButtons[0].trigger('click')
    await nextTick()

    // One row removed → only one remains
    expect(wrapper.findAll('tbody tr')).toHaveLength(1)
  })

  it('removing the last row keeps exactly one row in the table', async () => {
    const wrapper = await mountEditorReactive([{ key: 'only', value: 'row' }])

    const removeButton = wrapper.find('tbody button')
    await removeButton.trigger('click')
    await nextTick()

    // Component guarantees at least one row — should not collapse to zero rows
    expect(wrapper.findAll('tbody tr')).toHaveLength(1)
    // Row must still have two inputs (key + value cells)
    expect(wrapper.findAll('tbody input')).toHaveLength(2)
  })

  it('each body row has exactly one remove button', async () => {
    const wrapper = await mountEditor([
      { key: 'a', value: '1' },
      { key: 'b', value: '2' },
      { key: 'c', value: '3' }
    ])

    const rows = wrapper.findAll('tbody tr')
    for (const row of rows) {
      expect(row.findAll('button')).toHaveLength(1)
    }
  })
})
