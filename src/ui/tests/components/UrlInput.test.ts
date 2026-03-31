import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import UrlInput from '~/components/request-builder/UrlInput.vue'

describe('UrlInput', () => {
  it('renders an input field', () => {
    const wrapper = mount(UrlInput)
    const input = wrapper.find('input')
    expect(input.exists()).toBe(true)
  })

  it('shows placeholder text "Enter request URL"', () => {
    const wrapper = mount(UrlInput)
    const input = wrapper.find('input')
    expect(input.attributes('placeholder')).toBe('Enter request URL')
  })

  it('binds modelValue to the input', () => {
    const wrapper = mount(UrlInput, {
      props: { modelValue: 'https://api.example.com/users' }
    })
    const input = wrapper.find('input')
    expect((input.element as HTMLInputElement).value).toBe('https://api.example.com/users')
  })

  it('emits update:modelValue on input', async () => {
    const wrapper = mount(UrlInput)
    const input = wrapper.find('input')
    await input.setValue('https://api.example.com')
    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
    expect(wrapper.emitted('update:modelValue')![0]).toEqual(['https://api.example.com'])
  })

  it('highlights {{variable}} patterns with a special class', () => {
    const wrapper = mount(UrlInput, {
      props: { modelValue: 'https://api.example.com/{{userId}}/posts' }
    })
    const highlights = wrapper.findAll('.url-variable')
    expect(highlights.length).toBeGreaterThan(0)
    expect(highlights[0].text()).toContain('{{userId}}')
  })

  it('highlights multiple {{variable}} patterns', () => {
    const wrapper = mount(UrlInput, {
      props: { modelValue: 'https://{{host}}/api/{{version}}/users' }
    })
    const highlights = wrapper.findAll('.url-variable')
    expect(highlights).toHaveLength(2)
  })
})
