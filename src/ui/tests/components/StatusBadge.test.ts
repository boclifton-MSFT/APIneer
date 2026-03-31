import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import StatusBadge from '~/components/response/StatusBadge.vue'

describe('StatusBadge', () => {
  it('renders the HTTP status code and text', async () => {
    const wrapper = await mountSuspended(StatusBadge, {
      props: { statusCode: 200, statusText: 'OK' }
    })
    expect(wrapper.text()).toContain('200')
    expect(wrapper.text()).toContain('OK')
  })

  it('displays "200 OK" format', async () => {
    const wrapper = await mountSuspended(StatusBadge, {
      props: { statusCode: 200, statusText: 'OK' }
    })
    expect(wrapper.text()).toContain('200 OK')
  })

  it('displays "404 Not Found" format', async () => {
    const wrapper = await mountSuspended(StatusBadge, {
      props: { statusCode: 404, statusText: 'Not Found' }
    })
    expect(wrapper.text()).toContain('404 Not Found')
  })

  describe('color coding by status class', () => {
    it('applies success style for 2xx status codes', async () => {
      const wrapper = await mountSuspended(StatusBadge, {
        props: { statusCode: 200, statusText: 'OK' }
      })
      const badge = wrapper.find('[data-testid="status-badge"]')
      expect(badge.classes()).toContain('status-success')
    })

    it('applies success style for 201 Created', async () => {
      const wrapper = await mountSuspended(StatusBadge, {
        props: { statusCode: 201, statusText: 'Created' }
      })
      const badge = wrapper.find('[data-testid="status-badge"]')
      expect(badge.classes()).toContain('status-success')
    })

    it('applies info style for 3xx status codes', async () => {
      const wrapper = await mountSuspended(StatusBadge, {
        props: { statusCode: 301, statusText: 'Moved Permanently' }
      })
      const badge = wrapper.find('[data-testid="status-badge"]')
      expect(badge.classes()).toContain('status-info')
    })

    it('applies warning style for 4xx status codes', async () => {
      const wrapper = await mountSuspended(StatusBadge, {
        props: { statusCode: 404, statusText: 'Not Found' }
      })
      const badge = wrapper.find('[data-testid="status-badge"]')
      expect(badge.classes()).toContain('status-warning')
    })

    it('applies error style for 5xx status codes', async () => {
      const wrapper = await mountSuspended(StatusBadge, {
        props: { statusCode: 500, statusText: 'Internal Server Error' }
      })
      const badge = wrapper.find('[data-testid="status-badge"]')
      expect(badge.classes()).toContain('status-error')
    })
  })

  describe('edge cases', () => {
    it('shows error state for status 0 (connection error)', async () => {
      const wrapper = await mountSuspended(StatusBadge, {
        props: { statusCode: 0, statusText: 'Connection Error' }
      })
      const badge = wrapper.find('[data-testid="status-badge"]')
      expect(badge.classes()).toContain('status-error')
      expect(wrapper.text()).toContain('Connection Error')
    })
  })
})
