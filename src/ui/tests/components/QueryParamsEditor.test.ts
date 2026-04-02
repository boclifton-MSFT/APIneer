import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, ref } from 'vue'
import QueryParamsEditor from '~/components/request-builder/QueryParamsEditor.vue'

// Wrapper for v-model testing — mirrors the defineComponent pattern used in HeadersEditor tests
function mountWithUrl(initialUrl: string) {
  const Wrapper = defineComponent({
    components: { QueryParamsEditor },
    setup() {
      const url = ref(initialUrl)
      return { url }
    },
    template: `<QueryParamsEditor v-model:url="url" />`
  })
  const wrapper = mount(Wrapper)
  return {
    wrapper,
    getUrl: () => wrapper.vm.url as string,
    editor: () => wrapper.findComponent(QueryParamsEditor)
  }
}

describe('QueryParamsEditor', () => {
  // 1. Renders empty state when URL has no query string
  it('renders empty param table when URL has no query string', () => {
    const { editor } = mountWithUrl('http://example.com')
    const rows = editor().findAll('tbody tr')
    expect(rows).toHaveLength(0)
  })

  // 2. Parses existing query params from URL
  it('parses existing query params from URL into table rows', () => {
    const { editor } = mountWithUrl('http://example.com?foo=bar&baz=qux')
    const rows = editor().findAll('tbody tr')
    expect(rows).toHaveLength(2)

    const keyInputs = editor().findAll('[data-testid="param-key-input"]')
    const valueInputs = editor().findAll('[data-testid="param-value-input"]')

    expect((keyInputs[0].element as HTMLInputElement).value).toBe('foo')
    expect((valueInputs[0].element as HTMLInputElement).value).toBe('bar')
    expect((keyInputs[1].element as HTMLInputElement).value).toBe('baz')
    expect((valueInputs[1].element as HTMLInputElement).value).toBe('qux')
  })

  // 3. Adding a param and typing key/value updates the emitted URL
  it('emits updated URL when a new param key and value are typed', async () => {
    const { wrapper, getUrl, editor } = mountWithUrl('http://example.com')

    const addBtn = editor().find('[data-testid="add-param"]')
    expect(addBtn.exists()).toBe(true)
    await addBtn.trigger('click')

    const keyInput = editor().find('[data-testid="param-key-input"]')
    const valueInput = editor().find('[data-testid="param-value-input"]')

    await keyInput.setValue('token')
    await valueInput.setValue('abc123')

    const emitted = getUrl()
    expect(emitted).toContain('token=abc123')
    expect(emitted).toContain('?')
  })

  // 4. Removing a param emits updated URL without that param
  it('emits URL without the removed param when remove is clicked', async () => {
    const { getUrl, editor } = mountWithUrl('http://example.com?keep=yes&drop=me')

    const removeButtons = editor().findAll('[data-testid="remove-param"]')
    expect(removeButtons.length).toBeGreaterThanOrEqual(2)

    // Remove "drop=me" (second row)
    await removeButtons[1].trigger('click')

    const url = getUrl()
    expect(url).not.toContain('drop=me')
    expect(url).toContain('keep=yes')
  })

  // 5. Toggling the enable checkbox disables/re-enables a param
  it('excludes disabled params from the emitted URL', async () => {
    const { getUrl, editor } = mountWithUrl('http://example.com?active=1&inactive=0')

    const checkboxes = editor().findAll('[data-testid="param-enabled"]')
    expect(checkboxes.length).toBeGreaterThanOrEqual(2)

    // Uncheck second param
    await checkboxes[1].setValue(false)

    const url = getUrl()
    expect(url).toContain('active=1')
    expect(url).not.toContain('inactive=0')
  })

  it('re-enables a param when checkbox is checked again', async () => {
    const { getUrl, editor } = mountWithUrl('http://example.com?a=1&b=2')

    const checkboxes = editor().findAll('[data-testid="param-enabled"]')

    // Disable then re-enable first param
    await checkboxes[0].setValue(false)
    expect(getUrl()).not.toContain('a=1')

    await checkboxes[0].setValue(true)
    expect(getUrl()).toContain('a=1')
  })

  // 6. Handles URL with `?` but no params
  it('renders empty param table when URL has ? but no params', () => {
    const { editor } = mountWithUrl('http://example.com?')
    const rows = editor().findAll('tbody tr')
    expect(rows).toHaveLength(0)
  })

  // 7. Handles encoded values in query string
  it('decodes percent-encoded values when parsing URL params', () => {
    const { editor } = mountWithUrl('http://example.com?q=hello%20world&tag=C%2B%2B')
    const valueInputs = editor().findAll('[data-testid="param-value-input"]')
    expect((valueInputs[0].element as HTMLInputElement).value).toBe('hello world')
    expect((valueInputs[1].element as HTMLInputElement).value).toBe('C++')
  })

  it('handles + as space in query string values', () => {
    const { editor } = mountWithUrl('http://example.com?q=hello+world')
    const valueInputs = editor().findAll('[data-testid="param-value-input"]')
    // URLSearchParams decodes + as space
    expect((valueInputs[0].element as HTMLInputElement).value).toBe('hello world')
  })

  // 8. Handles duplicate keys
  it('renders all rows when URL has duplicate param keys', () => {
    const { editor } = mountWithUrl('http://example.com?tag=a&tag=b&tag=c')
    const rows = editor().findAll('tbody tr')
    expect(rows).toHaveLength(3)

    const keyInputs = editor().findAll('[data-testid="param-key-input"]')
    keyInputs.forEach((input) => {
      expect((input.element as HTMLInputElement).value).toBe('tag')
    })

    const valueInputs = editor().findAll('[data-testid="param-value-input"]')
    const values = valueInputs.map((i) => (i.element as HTMLInputElement).value)
    expect(values).toContain('a')
    expect(values).toContain('b')
    expect(values).toContain('c')
  })

  // 9. Adding first param to URL with no query string adds `?`
  it('adds ? to the URL when adding the first param to a bare URL', async () => {
    const { getUrl, editor } = mountWithUrl('http://example.com')

    await editor().find('[data-testid="add-param"]').trigger('click')

    const keyInput = editor().find('[data-testid="param-key-input"]')
    const valueInput = editor().find('[data-testid="param-value-input"]')

    await keyInput.setValue('page')
    await valueInput.setValue('1')

    const url = getUrl()
    expect(url).toContain('?')
    expect(url).toContain('page=1')
    expect(url.startsWith('http://example.com?')).toBe(true)
  })

  // 10. Multiple operations: add, edit, remove in sequence
  it('handles add, edit, and remove operations in sequence', async () => {
    const { getUrl, editor } = mountWithUrl('http://example.com?existing=value')

    // Add a second param
    await editor().find('[data-testid="add-param"]').trigger('click')
    let rows = editor().findAll('tbody tr')
    expect(rows).toHaveLength(2)

    // Edit the new param's key and value
    const keyInputs = editor().findAll('[data-testid="param-key-input"]')
    const valueInputs = editor().findAll('[data-testid="param-value-input"]')
    await keyInputs[1].setValue('newKey')
    await valueInputs[1].setValue('newVal')
    expect(getUrl()).toContain('newKey=newVal')
    expect(getUrl()).toContain('existing=value')

    // Remove the first param
    const removeButtons = editor().findAll('[data-testid="remove-param"]')
    await removeButtons[0].trigger('click')

    const finalUrl = getUrl()
    expect(finalUrl).not.toContain('existing=value')
    expect(finalUrl).toContain('newKey=newVal')

    rows = editor().findAll('tbody tr')
    expect(rows).toHaveLength(1)
  })

  // Table structure checks
  it('renders Key, Value, Enable columns and an action column', () => {
    const { editor } = mountWithUrl('http://example.com')
    const headers = editor().findAll('th')
    const headerTexts = headers.map((h) => h.text().toLowerCase())
    expect(headerTexts).toContain('key')
    expect(headerTexts).toContain('value')
    expect(headerTexts.some((t) => t.includes('enable') || t.includes('enabled'))).toBe(true)
  })

  it('renders an "Add Parameter" button', () => {
    const { editor } = mountWithUrl('http://example.com')
    const addBtn = editor().find('[data-testid="add-param"]')
    expect(addBtn.exists()).toBe(true)
    expect(addBtn.text().toLowerCase()).toContain('add')
  })

  it('each param row has a remove button', () => {
    const { editor } = mountWithUrl('http://example.com?a=1&b=2')
    const removeButtons = editor().findAll('[data-testid="remove-param"]')
    expect(removeButtons).toHaveLength(2)
  })

  it('each param row has an enable checkbox', () => {
    const { editor } = mountWithUrl('http://example.com?x=1&y=2')
    const checkboxes = editor().findAll('[data-testid="param-enabled"]')
    expect(checkboxes).toHaveLength(2)
    checkboxes.forEach((cb) => {
      expect((cb.element as HTMLInputElement).checked).toBe(true)
    })
  })

  // Bidirectional sync: changing the url prop updates the table
  it('updates param rows when the url prop changes externally', async () => {
    const Wrapper = defineComponent({
      components: { QueryParamsEditor },
      setup() {
        const url = ref('http://example.com?initial=yes')
        return { url }
      },
      template: `<QueryParamsEditor v-model:url="url" />`
    })
    const wrapper = mount(Wrapper)
    const editorCmp = wrapper.findComponent(QueryParamsEditor)

    let rows = editorCmp.findAll('tbody tr')
    expect(rows).toHaveLength(1)

    // Externally update the URL
    wrapper.vm.url = 'http://example.com?a=1&b=2&c=3'
    await wrapper.vm.$nextTick()

    rows = editorCmp.findAll('tbody tr')
    expect(rows).toHaveLength(3)
  })

  // Preserves base URL (origin + path) when editing params
  it('preserves the base URL origin and path when params are modified', async () => {
    const { getUrl, editor } = mountWithUrl('http://api.example.com/v1/users?limit=10')

    const valueInput = editor().find('[data-testid="param-value-input"]')
    await valueInput.setValue('25')

    const url = getUrl()
    expect(url.startsWith('http://api.example.com/v1/users')).toBe(true)
    expect(url).toContain('limit=25')
  })
})
