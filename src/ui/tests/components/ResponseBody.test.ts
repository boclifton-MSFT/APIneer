import { describe, it, expect, vi } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ResponseBody from '~/components/response/ResponseBody.vue'

describe('ResponseBody', () => {
  it('renders response body text', async () => {
    const wrapper = await mountSuspended(ResponseBody, {
      props: { body: 'Hello, World!', contentType: 'text/plain' }
    })
    expect(wrapper.text()).toContain('Hello, World!')
  })

  it('shows "No response" placeholder when body is empty', async () => {
    const wrapper = await mountSuspended(ResponseBody, {
      props: { body: '', contentType: '' }
    })
    expect(wrapper.text()).toContain('No response')
  })

  it('shows "No response" placeholder when body is undefined', async () => {
    const wrapper = await mountSuspended(ResponseBody, {
      props: { contentType: '' }
    })
    expect(wrapper.text()).toContain('No response')
  })

  describe('JSON formatting', () => {
    it('pretty-prints JSON responses with indentation', async () => {
      const json = '{"name":"Alice","age":30}'
      const wrapper = await mountSuspended(ResponseBody, {
        props: { body: json, contentType: 'application/json' }
      })
      const prettyJson = JSON.stringify(JSON.parse(json), null, 2)
      expect(wrapper.text()).toContain(prettyJson)
    })

    it('handles invalid JSON gracefully in pretty mode', async () => {
      const invalidJson = '{not valid json}'
      const wrapper = await mountSuspended(ResponseBody, {
        props: { body: invalidJson, contentType: 'application/json' }
      })
      // Should still render the raw body without crashing
      expect(wrapper.text()).toContain(invalidJson)
    })
  })

  describe('view mode tabs', () => {
    it('has Pretty and Raw view mode tabs', async () => {
      const wrapper = await mountSuspended(ResponseBody, {
        props: { body: '{"key":"value"}', contentType: 'application/json' }
      })
      const tabs = wrapper.findAll('[data-testid="view-mode-tab"]')
      const tabTexts = tabs.map(t => t.text())
      expect(tabTexts).toContain('Pretty')
      expect(tabTexts).toContain('Raw')
    })

    it('defaults to Pretty view mode', async () => {
      const wrapper = await mountSuspended(ResponseBody, {
        props: { body: '{"key":"value"}', contentType: 'application/json' }
      })
      const activeTab = wrapper.find('[data-testid="view-mode-tab"].active')
      expect(activeTab.text()).toBe('Pretty')
    })

    it('switches to Raw view when Raw tab is clicked', async () => {
      const json = '{"key":"value"}'
      const wrapper = await mountSuspended(ResponseBody, {
        props: { body: json, contentType: 'application/json' }
      })
      const rawTab = wrapper.findAll('[data-testid="view-mode-tab"]').find(t => t.text() === 'Raw')
      await rawTab!.trigger('click')
      const bodyContent = wrapper.find('[data-testid="body-content"]')
      expect(bodyContent.text()).toBe(json)
    })
  })

  describe('copy button', () => {
    it('has a Copy button', async () => {
      const wrapper = await mountSuspended(ResponseBody, {
        props: { body: 'some content', contentType: 'text/plain' }
      })
      const copyButton = wrapper.find('[data-testid="copy-button"]')
      expect(copyButton.exists()).toBe(true)
      expect(copyButton.text()).toContain('Copy')
    })

    it('copies body to clipboard when Copy is clicked', async () => {
      const writeText = vi.fn().mockResolvedValue(undefined)
      Object.assign(navigator, { clipboard: { writeText } })

      const body = 'content to copy'
      const wrapper = await mountSuspended(ResponseBody, {
        props: { body, contentType: 'text/plain' }
      })
      await wrapper.find('[data-testid="copy-button"]').trigger('click')
      expect(writeText).toHaveBeenCalledWith(body)
    })
  })

  describe('large body handling', () => {
    it('handles large bodies gracefully without crashing', async () => {
      const largeBody = 'x'.repeat(1_100_000) // ~1.1 MB
      const wrapper = await mountSuspended(ResponseBody, {
        props: { body: largeBody, contentType: 'text/plain' }
      })
      expect(wrapper.exists()).toBe(true)
    })
  })
})
