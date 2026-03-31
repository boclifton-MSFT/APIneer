import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import MethodSelector from '~/components/request-builder/MethodSelector.vue'

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'] as const

describe('MethodSelector', () => {
  it('renders a dropdown/select element', () => {
    const wrapper = mount(MethodSelector)
    const select = wrapper.find('select')
    expect(select.exists()).toBe(true)
  })

  it('lists all HTTP methods as options', () => {
    const wrapper = mount(MethodSelector)
    const options = wrapper.findAll('option')
    const values = options.map((o) => o.element.value)
    expect(values).toEqual(expect.arrayContaining(HTTP_METHODS))
    expect(options).toHaveLength(HTTP_METHODS.length)
  })

  it('defaults to GET when no modelValue is provided', () => {
    const wrapper = mount(MethodSelector)
    const select = wrapper.find('select')
    expect((select.element as HTMLSelectElement).value).toBe('GET')
  })

  it('reflects the provided modelValue', () => {
    const wrapper = mount(MethodSelector, {
      props: { modelValue: 'POST' }
    })
    const select = wrapper.find('select')
    expect((select.element as HTMLSelectElement).value).toBe('POST')
  })

  it('emits update:modelValue when selection changes', async () => {
    const wrapper = mount(MethodSelector)
    const select = wrapper.find('select')
    await select.setValue('DELETE')
    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
    expect(wrapper.emitted('update:modelValue')![0]).toEqual(['DELETE'])
  })

  it.each([
    ['GET', 'green'],
    ['POST', 'blue'],
    ['PUT', 'orange'],
    ['PATCH', 'yellow'],
    ['DELETE', 'red'],
    ['HEAD', 'purple'],
    ['OPTIONS', 'gray']
  ])('applies %s color class for %s method', (method, color) => {
    const wrapper = mount(MethodSelector, {
      props: { modelValue: method }
    })
    const el = wrapper.find('[data-testid="method-selector"]')
    expect(el.classes()).toContain(`method-${color}`)
  })
})
