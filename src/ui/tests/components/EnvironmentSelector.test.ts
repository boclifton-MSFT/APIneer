import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import EnvironmentSelector from '~/components/environments/EnvironmentSelector.vue'

const mockEnvironments = [
  { id: '1', name: 'Development', isActive: true, workspaceId: 'ws-1' },
  { id: '2', name: 'Staging', isActive: false, workspaceId: 'ws-1' },
  { id: '3', name: 'Production', isActive: false, workspaceId: 'ws-1' },
]

describe('EnvironmentSelector', () => {
  it('renders a dropdown with available environments', () => {
    const wrapper = mount(EnvironmentSelector, {
      props: { environments: mockEnvironments, modelValue: '1' },
    })
    const select = wrapper.find('[data-testid="environment-selector"]')
    expect(select.exists()).toBe(true)

    const options = wrapper.findAll('option')
    expect(options.length).toBeGreaterThanOrEqual(mockEnvironments.length)
  })

  it('lists all environment names as options', () => {
    const wrapper = mount(EnvironmentSelector, {
      props: { environments: mockEnvironments, modelValue: '1' },
    })
    const options = wrapper.findAll('option')
    const texts = options.map((o) => o.text())
    expect(texts).toContain('Development')
    expect(texts).toContain('Staging')
    expect(texts).toContain('Production')
  })

  it('shows the active environment name as selected', () => {
    const wrapper = mount(EnvironmentSelector, {
      props: { environments: mockEnvironments, modelValue: '1' },
    })
    const select = wrapper.find('[data-testid="environment-selector"]')
    expect((select.element as HTMLSelectElement).value).toBe('1')
  })

  it('highlights or indicates the active environment', () => {
    const wrapper = mount(EnvironmentSelector, {
      props: { environments: mockEnvironments, modelValue: '1' },
    })
    const activeIndicator = wrapper.find('[data-testid="active-indicator"]')
    expect(activeIndicator.exists()).toBe(true)
    expect(activeIndicator.text()).toContain('Development')
  })

  it('emits update:modelValue when a different environment is selected', async () => {
    const wrapper = mount(EnvironmentSelector, {
      props: { environments: mockEnvironments, modelValue: '1' },
    })
    const select = wrapper.find('[data-testid="environment-selector"]')
    await select.setValue('2')

    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
    expect(wrapper.emitted('update:modelValue')![0]).toEqual(['2'])
  })

  it('emits activate event when environment is changed', async () => {
    const wrapper = mount(EnvironmentSelector, {
      props: { environments: mockEnvironments, modelValue: '1' },
    })
    const select = wrapper.find('[data-testid="environment-selector"]')
    await select.setValue('3')

    expect(wrapper.emitted('activate')).toBeTruthy()
    expect(wrapper.emitted('activate')![0]).toEqual(['3'])
  })

  it('renders empty state when no environments exist', () => {
    const wrapper = mount(EnvironmentSelector, {
      props: { environments: [], modelValue: '' },
    })
    const emptyState = wrapper.find('[data-testid="no-environments"]')
    expect(emptyState.exists()).toBe(true)
  })

  it('includes a "No Environment" option to deactivate', () => {
    const wrapper = mount(EnvironmentSelector, {
      props: { environments: mockEnvironments, modelValue: '1' },
    })
    const options = wrapper.findAll('option')
    const values = options.map((o) => o.element.value)
    expect(values).toContain('')
  })
})
