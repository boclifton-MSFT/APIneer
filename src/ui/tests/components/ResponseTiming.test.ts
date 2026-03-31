import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ResponseTiming from '~/components/response/ResponseTiming.vue'

describe('ResponseTiming', () => {
  it('shows response time in milliseconds', async () => {
    const wrapper = await mountSuspended(ResponseTiming, {
      props: { timeMs: 245, sizeBytes: 1024 }
    })
    expect(wrapper.text()).toContain('245 ms')
  })

  it('shows response time for fast responses', async () => {
    const wrapper = await mountSuspended(ResponseTiming, {
      props: { timeMs: 12, sizeBytes: 512 }
    })
    expect(wrapper.text()).toContain('12 ms')
  })

  describe('human-readable response size', () => {
    it('shows bytes for small responses', async () => {
      const wrapper = await mountSuspended(ResponseTiming, {
        props: { timeMs: 100, sizeBytes: 500 }
      })
      expect(wrapper.text()).toContain('500 B')
    })

    it('shows KB for kilobyte-sized responses', async () => {
      const wrapper = await mountSuspended(ResponseTiming, {
        props: { timeMs: 100, sizeBytes: 1228 } // 1.2 KB
      })
      expect(wrapper.text()).toContain('1.2 KB')
    })

    it('shows MB for megabyte-sized responses', async () => {
      const wrapper = await mountSuspended(ResponseTiming, {
        props: { timeMs: 100, sizeBytes: 3_565_158 } // ~3.4 MB
      })
      expect(wrapper.text()).toContain('3.4 MB')
    })
  })

  it('renders both time and size in a compact display', async () => {
    const wrapper = await mountSuspended(ResponseTiming, {
      props: { timeMs: 245, sizeBytes: 1228 }
    })
    const display = wrapper.find('[data-testid="timing-display"]')
    expect(display.exists()).toBe(true)
    expect(display.text()).toContain('245 ms')
    expect(display.text()).toContain('1.2 KB')
  })
})
