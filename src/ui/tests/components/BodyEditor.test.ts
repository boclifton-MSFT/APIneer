import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import BodyEditor from '~/components/request-builder/BodyEditor.vue'

const BODY_MODES = ['None', 'Raw', 'JSON', 'Form Data'] as const

describe('BodyEditor', () => {
  it('renders mode tabs', () => {
    const wrapper = mount(BodyEditor)
    const tabs = wrapper.findAll('[data-testid="body-mode-tab"]')
    const tabLabels = tabs.map((t) => t.text())
    expect(tabLabels).toEqual(expect.arrayContaining([...BODY_MODES]))
  })

  it('defaults to "None" mode', () => {
    const wrapper = mount(BodyEditor)
    const activeTab = wrapper.find('[data-testid="body-mode-tab"].active')
    expect(activeTab.exists()).toBe(true)
    expect(activeTab.text()).toBe('None')
  })

  it('shows no editor area in "None" mode', () => {
    const wrapper = mount(BodyEditor)
    const textarea = wrapper.find('textarea')
    expect(textarea.exists()).toBe(false)
  })

  it('shows a textarea in "Raw" mode', async () => {
    const wrapper = mount(BodyEditor, {
      props: { bodyType: 'raw' }
    })
    const textarea = wrapper.find('textarea')
    expect(textarea.exists()).toBe(true)
  })

  it('shows a textarea in "JSON" mode', async () => {
    const wrapper = mount(BodyEditor, {
      props: { bodyType: 'json' }
    })
    const textarea = wrapper.find('textarea')
    expect(textarea.exists()).toBe(true)
  })

  it('validates JSON content in JSON mode and shows error for invalid JSON', async () => {
    const wrapper = mount(BodyEditor, {
      props: { bodyType: 'json', modelValue: '{ invalid json' }
    })
    const error = wrapper.find('[data-testid="json-error"]')
    expect(error.exists()).toBe(true)
  })

  it('does not show error for valid JSON in JSON mode', () => {
    const wrapper = mount(BodyEditor, {
      props: { bodyType: 'json', modelValue: '{"key": "value"}' }
    })
    const error = wrapper.find('[data-testid="json-error"]')
    expect(error.exists()).toBe(false)
  })

  it('emits update:modelValue when body content changes in Raw mode', async () => {
    const wrapper = mount(BodyEditor, {
      props: { bodyType: 'raw' }
    })
    const textarea = wrapper.find('textarea')
    await textarea.setValue('Hello World')
    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
    expect(wrapper.emitted('update:modelValue')![0]).toEqual(['Hello World'])
  })

  it('emits update:bodyType when mode tab changes', async () => {
    const wrapper = mount(BodyEditor)
    const rawTab = wrapper.findAll('[data-testid="body-mode-tab"]').find((t) => t.text() === 'Raw')
    expect(rawTab).toBeDefined()
    await rawTab!.trigger('click')
    expect(wrapper.emitted('update:bodyType')).toBeTruthy()
    expect(wrapper.emitted('update:bodyType')![0]).toEqual(['raw'])
  })
})

describe('BodyEditor — Form Data mode', () => {
  it('shows form-data table when bodyType is form-data (not textarea)', () => {
    const wrapper = mount(BodyEditor, {
      props: { bodyType: 'form-data' }
    })
    expect(wrapper.find('[data-testid="formdata-table"]').exists()).toBe(true)
    expect(wrapper.find('textarea').exists()).toBe(false)
  })

  it('does NOT show form-data table in raw or json modes', () => {
    const rawWrapper = mount(BodyEditor, { props: { bodyType: 'raw' } })
    expect(rawWrapper.find('[data-testid="formdata-table"]').exists()).toBe(false)

    const jsonWrapper = mount(BodyEditor, { props: { bodyType: 'json' } })
    expect(jsonWrapper.find('[data-testid="formdata-table"]').exists()).toBe(false)
  })

  it('renders Add Field button in form-data mode', () => {
    const wrapper = mount(BodyEditor, {
      props: { bodyType: 'form-data' }
    })
    expect(wrapper.find('[data-testid="add-formdata"]').exists()).toBe(true)
  })

  it('adding a field and typing key/value emits URL-encoded modelValue', async () => {
    const wrapper = mount(BodyEditor, {
      props: { bodyType: 'form-data', modelValue: '' }
    })
    await wrapper.find('[data-testid="add-formdata"]').trigger('click')
    await nextTick()

    const keyInput = wrapper.find('[data-testid="formdata-key-input"]')
    const valueInput = wrapper.find('[data-testid="formdata-value-input"]')
    await keyInput.setValue('username')
    await valueInput.setValue('Bo')

    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    const lastEmit = emitted![emitted!.length - 1][0] as string
    expect(lastEmit).toBe('username=Bo')
  })

  it('removing a field emits updated modelValue without that pair', async () => {
    const wrapper = mount(BodyEditor, {
      props: { bodyType: 'form-data', modelValue: 'name=Bo&role=dev' }
    })
    const removeButtons = wrapper.findAll('[data-testid="remove-formdata"]')
    expect(removeButtons.length).toBe(2)
    await removeButtons[0].trigger('click')

    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    const lastEmit = emitted![emitted!.length - 1][0] as string
    expect(lastEmit).toBe('role=dev')
  })

  it('parses existing URL-encoded modelValue into table rows', () => {
    const wrapper = mount(BodyEditor, {
      props: { bodyType: 'form-data', modelValue: 'name=Bo&role=dev' }
    })
    const keyInputs = wrapper.findAll('[data-testid="formdata-key-input"]')
    const valueInputs = wrapper.findAll('[data-testid="formdata-value-input"]')

    expect(keyInputs.length).toBe(2)
    expect((keyInputs[0].element as HTMLInputElement).value).toBe('name')
    expect((valueInputs[0].element as HTMLInputElement).value).toBe('Bo')
    expect((keyInputs[1].element as HTMLInputElement).value).toBe('role')
    expect((valueInputs[1].element as HTMLInputElement).value).toBe('dev')
  })

  it('handles empty modelValue — shows form-data table with no rows or one empty row', () => {
    const wrapper = mount(BodyEditor, {
      props: { bodyType: 'form-data', modelValue: '' }
    })
    expect(wrapper.find('[data-testid="formdata-table"]').exists()).toBe(true)
    const keyInputs = wrapper.findAll('[data-testid="formdata-key-input"]')
    // Either 0 rows (empty table) or 1 empty row — both acceptable
    expect(keyInputs.length).toBeLessThanOrEqual(1)
    if (keyInputs.length === 1) {
      expect((keyInputs[0].element as HTMLInputElement).value).toBe('')
    }
  })

  it('handles special characters in values via URL encoding', async () => {
    const wrapper = mount(BodyEditor, {
      props: { bodyType: 'form-data', modelValue: '' }
    })
    await wrapper.find('[data-testid="add-formdata"]').trigger('click')
    await nextTick()

    const keyInput = wrapper.find('[data-testid="formdata-key-input"]')
    const valueInput = wrapper.find('[data-testid="formdata-value-input"]')
    await keyInput.setValue('note')
    await valueInput.setValue('hello world & more')

    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    const lastEmit = emitted![emitted!.length - 1][0] as string
    // value must have spaces and & encoded
    expect(lastEmit).not.toContain(' ')
    expect(lastEmit).not.toMatch(/[^%\w=&].*&/)
    expect(decodeURIComponent(lastEmit.split('=')[1])).toBe('hello world & more')
  })

  it('multiple fields serialize correctly in order', async () => {
    const wrapper = mount(BodyEditor, {
      props: { bodyType: 'form-data', modelValue: 'a=1&b=2&c=3' }
    })
    const keyInputs = wrapper.findAll('[data-testid="formdata-key-input"]')
    expect(keyInputs.length).toBe(3)

    // Update the second field value
    const valueInputs = wrapper.findAll('[data-testid="formdata-value-input"]')
    await valueInputs[1].setValue('99')

    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    const lastEmit = emitted![emitted!.length - 1][0] as string
    expect(lastEmit).toBe('a=1&b=99&c=3')
  })
})
