import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import CollectionSidebar from '~/components/collections/CollectionSidebar.vue'

describe('CollectionSidebar', () => {
  const sampleCollections = [
    {
      id: 'col-1',
      name: 'User API',
      folders: [
        {
          id: 'folder-1',
          name: 'Auth',
          sortOrder: 0,
          subFolders: [],
          requests: [
            { id: 'req-1', name: 'Login', method: 'POST', url: '/auth/login', sortOrder: 0 },
            { id: 'req-2', name: 'Logout', method: 'POST', url: '/auth/logout', sortOrder: 1 }
          ]
        }
      ],
      requests: [
        { id: 'req-3', name: 'Health Check', method: 'GET', url: '/health', sortOrder: 0 }
      ]
    },
    {
      id: 'col-2',
      name: 'Payment API',
      folders: [],
      requests: [
        { id: 'req-4', name: 'Charge', method: 'POST', url: '/payments/charge', sortOrder: 0 },
        { id: 'req-5', name: 'Refund', method: 'POST', url: '/payments/refund', sortOrder: 1 },
        { id: 'req-6', name: 'List Payments', method: 'GET', url: '/payments', sortOrder: 2 }
      ]
    }
  ]

  function mountSidebar(props: Record<string, unknown> = {}) {
    return mount(CollectionSidebar, {
      props: {
        collections: sampleCollections,
        ...props
      }
    })
  }

  describe('tree rendering', () => {
    it('renders collection tree with all collections', () => {
      const wrapper = mountSidebar()
      expect(wrapper.text()).toContain('User API')
      expect(wrapper.text()).toContain('Payment API')
    })

    it('renders requests within collections', () => {
      const wrapper = mountSidebar()
      expect(wrapper.text()).toContain('Login')
      expect(wrapper.text()).toContain('Health Check')
      expect(wrapper.text()).toContain('Charge')
    })

    it('shows request count per collection', () => {
      const wrapper = mountSidebar()
      // User API: 3 requests (2 in folder + 1 root)
      const col1Count = wrapper.find('[data-testid="collection-count-col-1"]')
      expect(col1Count.exists()).toBe(true)
      expect(col1Count.text()).toContain('3')
      // Payment API: 3 requests
      const col2Count = wrapper.find('[data-testid="collection-count-col-2"]')
      expect(col2Count.exists()).toBe(true)
      expect(col2Count.text()).toContain('3')
    })
  })

  describe('actions', () => {
    it('"New Request" button creates request in selected collection', async () => {
      const wrapper = mountSidebar({ selectedCollectionId: 'col-1' })
      const newReqBtn = wrapper.find('[data-testid="new-request-button"]')
      expect(newReqBtn.exists()).toBe(true)
      await newReqBtn.trigger('click')
      expect(wrapper.emitted('new-request')).toBeTruthy()
      expect(wrapper.emitted('new-request')![0]).toEqual([{ collectionId: 'col-1' }])
    })

    it('"New Collection" button emits new-collection event', async () => {
      const wrapper = mountSidebar()
      const newColBtn = wrapper.find('[data-testid="new-collection-button"]')
      expect(newColBtn.exists()).toBe(true)
      await newColBtn.trigger('click')
      expect(wrapper.emitted('new-collection')).toBeTruthy()
    })
  })

  describe('request selection', () => {
    it('clicking a request emits select-request with request ID', async () => {
      const wrapper = mountSidebar()
      const reqItem = wrapper.find('[data-testid="request-item-req-3"]')
      expect(reqItem.exists()).toBe(true)
      await reqItem.trigger('click')
      expect(wrapper.emitted('select-request')).toBeTruthy()
      expect(wrapper.emitted('select-request')![0]).toEqual(['req-3'])
    })

    it('active request is highlighted with active class', () => {
      const wrapper = mountSidebar({ activeRequestId: 'req-1' })
      const activeItem = wrapper.find('[data-testid="request-item-req-1"]')
      expect(activeItem.exists()).toBe(true)
      expect(activeItem.classes()).toContain('active')
    })

    it('non-active requests are not highlighted', () => {
      const wrapper = mountSidebar({ activeRequestId: 'req-1' })
      const otherItem = wrapper.find('[data-testid="request-item-req-3"]')
      expect(otherItem.classes()).not.toContain('active')
    })
  })

  describe('empty state', () => {
    it('shows empty state when no collections exist', () => {
      const wrapper = mountSidebar({ collections: [] })
      const emptyState = wrapper.find('[data-testid="sidebar-empty-state"]')
      expect(emptyState.exists()).toBe(true)
      expect(wrapper.text()).toContain('No collections')
    })
  })

  describe('search/filter', () => {
    it('has a search input field', () => {
      const wrapper = mountSidebar()
      const search = wrapper.find('[data-testid="sidebar-search"]')
      expect(search.exists()).toBe(true)
    })

    it('filters requests by name when search text is entered', async () => {
      const wrapper = mountSidebar()
      const search = wrapper.find('[data-testid="sidebar-search"]')
      await search.setValue('Login')
      // Should show matching request
      expect(wrapper.text()).toContain('Login')
      // Should hide non-matching requests
      expect(wrapper.text()).not.toContain('Charge')
      expect(wrapper.text()).not.toContain('Refund')
    })
  })
})
