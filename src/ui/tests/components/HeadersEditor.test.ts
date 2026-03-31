import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import HeadersEditor from '~/components/request-builder/HeadersEditor.vue'

describe('HeadersEditor', () => {
  it('renders a key-value table', () => {
    const wrapper = mount(HeadersEditor)
    const table = wrapper.find('table')
    expect(table.exists()).toBe(true)
  })

  it('has columns for key, value, and actions', () => {
    const wrapper = mount(HeadersEditor)
    const headers = wrapper.findAll('th')
    const headerTexts = headers.map((h) => h.text().toLowerCase())
    expect(headerTexts).toContain('key')
    expect(headerTexts).toContain('value')
  })

  it('starts with one empty row by default', () => {
    const wrapper = mount(HeadersEditor)
    const rows = wrapper.findAll('tbody tr')
    expect(rows).toHaveLength(1)
    const inputs = rows[0].findAll('input')
    expect(inputs.length).toBeGreaterThanOrEqual(2)
    expect((inputs[0].element as HTMLInputElement).value).toBe('')
    expect((inputs[1].element as HTMLInputElement).value).toBe('')
  })

  it('renders provided headers', () => {
    const wrapper = mount(HeadersEditor, {
      props: {
        modelValue: [
          { key: 'Content-Type', value: 'application/json' },
          { key: 'Authorization', value: 'Bearer token123' }
        ]
      }
    })
    const rows = wrapper.findAll('tbody tr')
    expect(rows).toHaveLength(2)
  })

  it('can add a new header row', async () => {
    const wrapper = mount(HeadersEditor)
    const addButton = wrapper.find('[data-testid="add-header"]')
    expect(addButton.exists()).toBe(true)
    await addButton.trigger('click')
    const rows = wrapper.findAll('tbody tr')
    expect(rows).toHaveLength(2)
  })

  it('can remove a header row', async () => {
    const wrapper = mount(HeadersEditor, {
      props: {
        modelValue: [
          { key: 'Content-Type', value: 'application/json' },
          { key: 'Accept', value: 'text/html' }
        ]
      }
    })
    const removeButtons = wrapper.findAll('[data-testid="remove-header"]')
    expect(removeButtons.length).toBeGreaterThan(0)
    await removeButtons[0].trigger('click')
    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
    const emitted = wrapper.emitted('update:modelValue')!
    const lastEmit = emitted[emitted.length - 1][0] as Array<{ key: string; value: string }>
    expect(lastEmit).toHaveLength(1)
  })

  it('emits update:modelValue when a header key changes', async () => {
    const wrapper = mount(HeadersEditor)
    const keyInput = wrapper.find('tbody tr input')
    await keyInput.setValue('X-Custom-Header')
    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
    const emitted = wrapper.emitted('update:modelValue')!
    const lastEmit = emitted[emitted.length - 1][0] as Array<{ key: string; value: string }>
    expect(lastEmit[0].key).toBe('X-Custom-Header')
  })
})
