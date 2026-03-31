import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import ResponseHeaders from '~/components/response/ResponseHeaders.vue'

describe('ResponseHeaders', () => {
  const sampleHeaders = [
    { name: 'Content-Type', value: 'application/json' },
    { name: 'Authorization', value: 'Bearer token123' },
    { name: 'X-Request-Id', value: 'abc-123' }
  ]

  it('renders response headers as a key-value table', async () => {
    const wrapper = await mountSuspended(ResponseHeaders, {
      props: { headers: sampleHeaders }
    })
    const table = wrapper.find('table')
    expect(table.exists()).toBe(true)

    const rows = wrapper.findAll('tbody tr')
    expect(rows.length).toBe(3)
  })

  it('displays header name and value in each row', async () => {
    const wrapper = await mountSuspended(ResponseHeaders, {
      props: { headers: sampleHeaders }
    })
    expect(wrapper.text()).toContain('Content-Type')
    expect(wrapper.text()).toContain('application/json')
    expect(wrapper.text()).toContain('Authorization')
    expect(wrapper.text()).toContain('Bearer token123')
  })

  it('sorts headers alphabetically by name', async () => {
    const wrapper = await mountSuspended(ResponseHeaders, {
      props: { headers: sampleHeaders }
    })
    const rows = wrapper.findAll('tbody tr')
    const headerNames = rows.map(row => {
      const cells = row.findAll('td')
      return cells[0]?.text()
    })
    expect(headerNames).toEqual(['Authorization', 'Content-Type', 'X-Request-Id'])
  })

  it('shows header count', async () => {
    const wrapper = await mountSuspended(ResponseHeaders, {
      props: { headers: sampleHeaders }
    })
    expect(wrapper.text()).toContain('3')
  })

  it('shows empty state when no headers are provided', async () => {
    const wrapper = await mountSuspended(ResponseHeaders, {
      props: { headers: [] }
    })
    expect(wrapper.text()).toContain('No headers')
  })

  it('shows empty state when headers prop is undefined', async () => {
    const wrapper = await mountSuspended(ResponseHeaders, {
      props: {}
    })
    expect(wrapper.text()).toContain('No headers')
  })
})
