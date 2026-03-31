import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ResponseViewer from '~/components/response/ResponseViewer.vue'

describe('ResponseViewer', () => {
  const sampleResponse = {
    statusCode: 200,
    statusText: 'OK',
    body: '{"message":"Hello"}',
    contentType: 'application/json',
    headers: [
      { name: 'Content-Type', value: 'application/json' },
      { name: 'X-Request-Id', value: 'abc-123' }
    ],
    timeMs: 245,
    sizeBytes: 1024
  }

  describe('tabs', () => {
    it('renders Body, Headers, and Timing tabs', async () => {
      const wrapper = await mountSuspended(ResponseViewer, {
        props: { response: sampleResponse }
      })
      const tabs = wrapper.findAll('[data-testid="response-tab"]')
      const tabTexts = tabs.map(t => t.text())
      expect(tabTexts).toContain('Body')
      expect(tabTexts).toContain('Headers')
      expect(tabTexts).toContain('Timing')
    })

    it('defaults to Body tab', async () => {
      const wrapper = await mountSuspended(ResponseViewer, {
        props: { response: sampleResponse }
      })
      const activeTab = wrapper.find('[data-testid="response-tab"].active')
      expect(activeTab.text()).toBe('Body')
    })
  })

  it('shows StatusBadge with status code', async () => {
    const wrapper = await mountSuspended(ResponseViewer, {
      props: { response: sampleResponse }
    })
    const badge = wrapper.find('[data-testid="status-badge"]')
    expect(badge.exists()).toBe(true)
    expect(badge.text()).toContain('200')
    expect(badge.text()).toContain('OK')
  })

  it('shows empty state when no response data', async () => {
    const wrapper = await mountSuspended(ResponseViewer, {
      props: {}
    })
    expect(wrapper.text()).toContain('Send a request to see the response')
  })

  it('shows empty state when response is null', async () => {
    const wrapper = await mountSuspended(ResponseViewer, {
      props: { response: null }
    })
    expect(wrapper.text()).toContain('Send a request to see the response')
  })

  describe('child component data passing', () => {
    it('passes body and contentType to ResponseBody', async () => {
      const wrapper = await mountSuspended(ResponseViewer, {
        props: { response: sampleResponse }
      })
      // Body tab is default — ResponseBody should render with body content
      expect(wrapper.text()).toContain('Hello')
    })

    it('passes headers to ResponseHeaders when Headers tab is clicked', async () => {
      const wrapper = await mountSuspended(ResponseViewer, {
        props: { response: sampleResponse }
      })
      const headersTab = wrapper.findAll('[data-testid="response-tab"]').find(t => t.text() === 'Headers')
      await headersTab!.trigger('click')
      expect(wrapper.text()).toContain('Content-Type')
      expect(wrapper.text()).toContain('application/json')
    })

    it('passes timing data to ResponseTiming when Timing tab is clicked', async () => {
      const wrapper = await mountSuspended(ResponseViewer, {
        props: { response: sampleResponse }
      })
      const timingTab = wrapper.findAll('[data-testid="response-tab"]').find(t => t.text() === 'Timing')
      await timingTab!.trigger('click')
      expect(wrapper.text()).toContain('245 ms')
    })
  })
})
