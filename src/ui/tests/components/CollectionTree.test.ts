import { describe, it, expect } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import CollectionTree from '~/components/collections/CollectionTree.vue'

describe('CollectionTree', () => {
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
        },
        {
          id: 'folder-2',
          name: 'Users',
          sortOrder: 1,
          subFolders: [
            {
              id: 'folder-3',
              name: 'Admin',
              sortOrder: 0,
              subFolders: [],
              requests: [
                { id: 'req-5', name: 'List Admins', method: 'GET', url: '/users/admins', sortOrder: 0 }
              ]
            }
          ],
          requests: [
            { id: 'req-3', name: 'Get Users', method: 'GET', url: '/users', sortOrder: 0 }
          ]
        }
      ],
      requests: [
        { id: 'req-4', name: 'Health Check', method: 'GET', url: '/health', sortOrder: 0 }
      ]
    }
  ]

  describe('tree structure rendering', () => {
    it('renders collection names', async () => {
      const wrapper = await mountSuspended(CollectionTree, {
        props: { collections: sampleCollections }
      })
      expect(wrapper.text()).toContain('User API')
    })

    it('renders folder names within collections', async () => {
      const wrapper = await mountSuspended(CollectionTree, {
        props: { collections: sampleCollections }
      })
      expect(wrapper.text()).toContain('Auth')
      expect(wrapper.text()).toContain('Users')
    })

    it('renders request names within folders', async () => {
      const wrapper = await mountSuspended(CollectionTree, {
        props: { collections: sampleCollections }
      })
      expect(wrapper.text()).toContain('Login')
      expect(wrapper.text()).toContain('Get Users')
    })

    it('renders root-level requests (not in any folder)', async () => {
      const wrapper = await mountSuspended(CollectionTree, {
        props: { collections: sampleCollections }
      })
      expect(wrapper.text()).toContain('Health Check')
    })

    it('renders nested subfolders', async () => {
      const wrapper = await mountSuspended(CollectionTree, {
        props: { collections: sampleCollections }
      })
      expect(wrapper.text()).toContain('Admin')
      expect(wrapper.text()).toContain('List Admins')
    })

    it('displays HTTP method badges for requests', async () => {
      const wrapper = await mountSuspended(CollectionTree, {
        props: { collections: sampleCollections }
      })
      const badges = wrapper.findAll('[data-testid="method-badge"]')
      expect(badges.length).toBeGreaterThan(0)
      const badgeTexts = badges.map(b => b.text())
      expect(badgeTexts).toContain('GET')
      expect(badgeTexts).toContain('POST')
    })
  })

  describe('expand/collapse', () => {
    it('toggles folder visibility on click', async () => {
      const wrapper = await mountSuspended(CollectionTree, {
        props: { collections: sampleCollections }
      })
      const folderToggle = wrapper.find('[data-testid="folder-toggle-folder-1"]')
      expect(folderToggle.exists()).toBe(true)

      // Click to collapse
      await folderToggle.trigger('click')
      // After collapse, child requests should be hidden
      const folderContent = wrapper.find('[data-testid="folder-content-folder-1"]')
      expect(folderContent.isVisible()).toBe(false)

      // Click again to expand
      await folderToggle.trigger('click')
      expect(folderContent.isVisible()).toBe(true)
    })

    it('shows expand/collapse icon for folders', async () => {
      const wrapper = await mountSuspended(CollectionTree, {
        props: { collections: sampleCollections }
      })
      const icon = wrapper.find('[data-testid="folder-icon-folder-1"]')
      expect(icon.exists()).toBe(true)
    })
  })

  describe('active request highlighting', () => {
    it('highlights the active request', async () => {
      const wrapper = await mountSuspended(CollectionTree, {
        props: {
          collections: sampleCollections,
          activeRequestId: 'req-1'
        }
      })
      const activeItem = wrapper.find('[data-testid="request-item-req-1"]')
      expect(activeItem.exists()).toBe(true)
      expect(activeItem.classes()).toContain('active')
    })

    it('does not highlight non-active requests', async () => {
      const wrapper = await mountSuspended(CollectionTree, {
        props: {
          collections: sampleCollections,
          activeRequestId: 'req-1'
        }
      })
      const otherItem = wrapper.find('[data-testid="request-item-req-2"]')
      expect(otherItem.exists()).toBe(true)
      expect(otherItem.classes()).not.toContain('active')
    })

    it('emits select event when request is clicked', async () => {
      const wrapper = await mountSuspended(CollectionTree, {
        props: { collections: sampleCollections }
      })
      const requestItem = wrapper.find('[data-testid="request-item-req-1"]')
      await requestItem.trigger('click')
      expect(wrapper.emitted('select-request')).toBeTruthy()
      expect(wrapper.emitted('select-request')![0]).toEqual(['req-1'])
    })
  })

  describe('empty state', () => {
    it('shows empty state when no collections provided', async () => {
      const wrapper = await mountSuspended(CollectionTree, {
        props: { collections: [] }
      })
      const emptyState = wrapper.find('[data-testid="empty-collections"]')
      expect(emptyState.exists()).toBe(true)
      expect(wrapper.text()).toContain('No collections')
    })

    it('shows empty state when collections is undefined', async () => {
      const wrapper = await mountSuspended(CollectionTree, {
        props: {}
      })
      const emptyState = wrapper.find('[data-testid="empty-collections"]')
      expect(emptyState.exists()).toBe(true)
    })
  })
})
