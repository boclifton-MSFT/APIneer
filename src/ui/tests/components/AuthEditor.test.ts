import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import AuthEditor from '~/components/auth/AuthEditor.vue'

const AUTH_TYPES = ['None', 'API Key', 'Bearer Token', 'Basic Auth', 'OAuth 2.0'] as const

describe('AuthEditor', () => {
  // ── Type Selector ───────────────────────────────────────────

  it('renders an auth type selector', () => {
    const wrapper = mount(AuthEditor)
    const selector = wrapper.find('[data-testid="auth-type-selector"]')
    expect(selector.exists()).toBe(true)
  })

  it('lists all auth types as options', () => {
    const wrapper = mount(AuthEditor)
    const options = wrapper.findAll('[data-testid="auth-type-selector"] option')
    const labels = options.map((o) => o.text())
    for (const type of AUTH_TYPES) {
      expect(labels).toContain(type)
    }
    expect(options).toHaveLength(AUTH_TYPES.length)
  })

  it('defaults to None when no modelValue provided', () => {
    const wrapper = mount(AuthEditor)
    const selector = wrapper.find('[data-testid="auth-type-selector"]')
    expect((selector.element as HTMLSelectElement).value).toBe('none')
  })

  // ── Auth-type-specific fields ─────────────────────────────────

  it('shows API Key fields when api_key type is selected', async () => {
    const wrapper = mount(AuthEditor, {
      props: { modelValue: { type: 'api_key', keyName: '', keyValue: '', placement: 'header' } },
    })
    expect(wrapper.find('[data-testid="apikey-key-name"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="apikey-key-value"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="apikey-placement"]').exists()).toBe(true)
  })

  it('shows Bearer Token field when bearer type is selected', () => {
    const wrapper = mount(AuthEditor, {
      props: { modelValue: { type: 'bearer', token: '' } },
    })
    expect(wrapper.find('[data-testid="bearer-token"]').exists()).toBe(true)
  })

  it('shows Basic Auth fields when basic type is selected', () => {
    const wrapper = mount(AuthEditor, {
      props: { modelValue: { type: 'basic', username: '', password: '' } },
    })
    expect(wrapper.find('[data-testid="basic-username"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="basic-password"]').exists()).toBe(true)
  })

  it('shows OAuth 2.0 fields when oauth2 type is selected', () => {
    const wrapper = mount(AuthEditor, {
      props: {
        modelValue: {
          type: 'oauth2',
          tokenEndpoint: '',
          clientId: '',
          clientSecret: '',
          scope: '',
        },
      },
    })
    expect(wrapper.find('[data-testid="oauth2-token-endpoint"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="oauth2-client-id"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="oauth2-client-secret"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="oauth2-scope"]').exists()).toBe(true)
  })

  it('shows no auth-specific fields when None is selected', () => {
    const wrapper = mount(AuthEditor, {
      props: { modelValue: { type: 'none' } },
    })
    expect(wrapper.find('[data-testid="apikey-key-name"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="bearer-token"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="basic-username"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="oauth2-token-endpoint"]').exists()).toBe(false)
  })

  // ── Inherit toggle ────────────────────────────────────────────

  it('renders an "Inherit from collection" toggle', () => {
    const wrapper = mount(AuthEditor, {
      props: { showInherit: true },
    })
    const toggle = wrapper.find('[data-testid="auth-inherit-toggle"]')
    expect(toggle.exists()).toBe(true)
  })

  it('emits update:inherit when inherit toggle changes', async () => {
    const wrapper = mount(AuthEditor, {
      props: { showInherit: true, inherit: false },
    })
    const toggle = wrapper.find('[data-testid="auth-inherit-toggle"]')
    await toggle.setValue(true)

    expect(wrapper.emitted('update:inherit')).toBeTruthy()
    expect(wrapper.emitted('update:inherit')![0]).toEqual([true])
  })

  // ── Config emission ───────────────────────────────────────────

  it('emits update:modelValue when auth type changes', async () => {
    const wrapper = mount(AuthEditor, {
      props: { modelValue: { type: 'none' } },
    })
    const selector = wrapper.find('[data-testid="auth-type-selector"]')
    await selector.setValue('bearer')

    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
    const emitted = wrapper.emitted('update:modelValue')![0][0] as { type: string }
    expect(emitted.type).toBe('bearer')
  })

  it('emits update:modelValue when a field value changes', async () => {
    const wrapper = mount(AuthEditor, {
      props: { modelValue: { type: 'bearer', token: '' } },
    })
    const tokenInput = wrapper.find('[data-testid="bearer-token"]')
    await tokenInput.setValue('my-new-token')

    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
    const emitted = wrapper.emitted('update:modelValue')!.at(-1)![0] as { type: string; token: string }
    expect(emitted.token).toBe('my-new-token')
  })

  it('shows placement options as header and query for API Key', () => {
    const wrapper = mount(AuthEditor, {
      props: { modelValue: { type: 'api_key', keyName: '', keyValue: '', placement: 'header' } },
    })
    const placementSelector = wrapper.find('[data-testid="apikey-placement"]')
    const options = placementSelector.findAll('option')
    const values = options.map((o) => o.element.value)
    expect(values).toContain('header')
    expect(values).toContain('query')
  })
})
