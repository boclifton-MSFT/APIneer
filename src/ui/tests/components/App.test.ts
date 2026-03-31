import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import App from '~/app.vue'

describe('App', () => {
  it('renders the app shell', async () => {
    const wrapper = await mountSuspended(App)
    expect(wrapper.exists()).toBe(true)
  })
})
