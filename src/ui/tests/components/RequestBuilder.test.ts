import { describe, it, expect, vi } from 'vitest'
import { nextTick } from 'vue'
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

describe('Auth tab — AuthEditor integration', () => {
  async function clickAuthTab(wrapper: ReturnType<typeof mount>) {
    const tabs = wrapper.findAll('[data-testid="request-tab"]')
    const authTab = tabs.find((t) => t.text() === 'Auth')
    await authTab!.trigger('click')
  }

  it('renders AuthEditor in Auth tab instead of "coming soon" placeholder', async () => {
    const wrapper = mount(RequestBuilder)
    await clickAuthTab(wrapper)
    expect(wrapper.findComponent({ name: 'AuthEditor' }).exists()).toBe(true)
    expect(wrapper.html()).not.toContain('coming soon')
  })

  it('defaults auth config to type "none"', async () => {
    const wrapper = mount(RequestBuilder)
    await clickAuthTab(wrapper)
    const authEditor = wrapper.findComponent({ name: 'AuthEditor' })
    const modelValue = authEditor.props('modelValue') as { type: string }
    expect(modelValue.type).toBe('none')
  })

  it('updates internal auth config when AuthEditor emits update:modelValue', async () => {
    const wrapper = mount(RequestBuilder)
    await clickAuthTab(wrapper)
    const authEditor = wrapper.findComponent({ name: 'AuthEditor' })
    await authEditor.vm.$emit('update:modelValue', { type: 'bearer', token: 'test-token' })
    await nextTick()
    expect((authEditor.props('modelValue') as any).type).toBe('bearer')
    expect((authEditor.props('modelValue') as any).token).toBe('test-token')
  })

  it('includes authConfig in the send payload when Send is clicked', async () => {
    const wrapper = mount(RequestBuilder)
    await clickAuthTab(wrapper)
    const authEditor = wrapper.findComponent({ name: 'AuthEditor' })
    await authEditor.vm.$emit('update:modelValue', { type: 'bearer', token: 'my-token' })
    await nextTick()
    await wrapper.find('[data-testid="send-button"]').trigger('click')
    const payload = wrapper.emitted('send')![0][0] as any
    expect(payload).toHaveProperty('authConfig')
    // authConfig is serialized as JSON string in the send payload (matches RequestData storage contract)
    const authConfig = typeof payload.authConfig === 'string' ? JSON.parse(payload.authConfig) : payload.authConfig
    expect(authConfig).toMatchObject({ type: 'bearer', token: 'my-token' })
  })

  it('syncs authConfig from the request prop when request has authConfig', async () => {
    const wrapper = mount(RequestBuilder, {
      props: {
        request: {
          method: 'GET',
          url: 'https://api.example.com',
          authConfig: { type: 'api_key', keyName: 'X-API-Key', keyValue: 'secret', placement: 'header' },
        },
      },
    })
    await clickAuthTab(wrapper)
    const modelValue = wrapper.findComponent({ name: 'AuthEditor' }).props('modelValue') as any
    expect(modelValue.type).toBe('api_key')
    expect(modelValue.keyName).toBe('X-API-Key')
    expect(modelValue.keyValue).toBe('secret')
  })

  it('defaults authConfig to "none" when request has no authConfig', async () => {
    const wrapper = mount(RequestBuilder, {
      props: { request: { method: 'POST', url: 'https://api.example.com' } },
    })
    await clickAuthTab(wrapper)
    const modelValue = wrapper.findComponent({ name: 'AuthEditor' }).props('modelValue') as { type: string }
    expect(modelValue.type).toBe('none')
  })
})
