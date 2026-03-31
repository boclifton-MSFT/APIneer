import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import RequestBuilder from '~/components/request-builder/RequestBuilder.vue'

describe('RequestBuilder', () => {
  it('renders the method selector', () => {
    const wrapper = mount(RequestBuilder)
    const methodSelector = wrapper.findComponent({ name: 'MethodSelector' })
    expect(methodSelector.exists()).toBe(true)
  })

  it('renders the URL input', () => {
    const wrapper = mount(RequestBuilder)
    const urlInput = wrapper.findComponent({ name: 'UrlInput' })
    expect(urlInput.exists()).toBe(true)
  })

  it('renders tab sections for Params, Headers, Body, and Auth', () => {
    const wrapper = mount(RequestBuilder)
    const tabs = wrapper.findAll('[data-testid="request-tab"]')
    const tabLabels = tabs.map((t) => t.text())
    expect(tabLabels).toEqual(expect.arrayContaining(['Params', 'Headers', 'Body', 'Auth']))
  })

  it('has a Send button', () => {
    const wrapper = mount(RequestBuilder)
    const sendButton = wrapper.find('[data-testid="send-button"]')
    expect(sendButton.exists()).toBe(true)
    expect(sendButton.text()).toContain('Send')
  })

  it('Send button is clickable and emits send event', async () => {
    const wrapper = mount(RequestBuilder)
    const sendButton = wrapper.find('[data-testid="send-button"]')
    await sendButton.trigger('click')
    expect(wrapper.emitted('send')).toBeTruthy()
  })

  it('shows loading state on Send button when request is in flight', async () => {
    const wrapper = mount(RequestBuilder, {
      props: { loading: true }
    })
    const sendButton = wrapper.find('[data-testid="send-button"]')
    expect(sendButton.classes()).toContain('loading')
    expect(sendButton.attributes('disabled')).toBeDefined()
  })

  it('Ctrl+Enter keyboard shortcut triggers send', async () => {
    const wrapper = mount(RequestBuilder)
    await wrapper.trigger('keydown', { key: 'Enter', ctrlKey: true })
    expect(wrapper.emitted('send')).toBeTruthy()
  })
})
