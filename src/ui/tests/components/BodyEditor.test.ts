import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
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
