import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import CollectionPicker from '~/components/collections/CollectionPicker.vue'

describe('CollectionPicker', () => {
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
          requests: []
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
              requests: []
            }
          ],
          requests: []
        }
      ],
      requests: []
    },
    {
      id: 'col-2',
      name: 'Payment API',
      folders: [],
      requests: []
    }
  ]

  function mountPicker(props: Record<string, unknown> = {}) {
    return mount(CollectionPicker, {
      props: {
        collections: sampleCollections,
        ...props
      }
    })
  }

  describe('rendering', () => {
    it('renders a dropdown with available collections', () => {
      const wrapper = mountPicker()
      expect(wrapper.find('[data-testid="collection-picker"]').exists()).toBe(true)
    })

    it('shows collection names in the dropdown', () => {
      const wrapper = mountPicker()
      const text = wrapper.text()
      expect(text).toContain('User API')
      expect(text).toContain('Payment API')
    })

    it('shows folder hierarchy indented under collections', () => {
      const wrapper = mountPicker()
      const options = wrapper.findAll('[data-testid^="picker-option-"]')
      const labels = options.map(o => o.text())
      // Should show nested folders with visual hierarchy indicator
      expect(labels.some(l => l.includes('Auth'))).toBe(true)
      expect(labels.some(l => l.includes('Users'))).toBe(true)
      expect(labels.some(l => l.includes('Admin'))).toBe(true)
    })

    it('has a "Default" option for no collection', () => {
      const wrapper = mountPicker()
      const defaultOption = wrapper.find('[data-testid="picker-option-default"]')
      expect(defaultOption.exists()).toBe(true)
      expect(defaultOption.text()).toContain('Default')
    })
  })

  describe('selection', () => {
    it('emits select with collectionId when a collection is chosen', async () => {
      const wrapper = mountPicker()
      const option = wrapper.find('[data-testid="picker-option-col-1"]')
      await option.trigger('click')
      expect(wrapper.emitted('select')).toBeTruthy()
      expect(wrapper.emitted('select')![0]).toEqual([
        { collectionId: 'col-1', folderId: undefined }
      ])
    })

    it('emits select with collectionId and folderId when a folder is chosen', async () => {
      const wrapper = mountPicker()
      const option = wrapper.find('[data-testid="picker-option-folder-1"]')
      await option.trigger('click')
      expect(wrapper.emitted('select')).toBeTruthy()
      expect(wrapper.emitted('select')![0]).toEqual([
        { collectionId: 'col-1', folderId: 'folder-1' }
      ])
    })

    it('emits select with null when Default is chosen', async () => {
      const wrapper = mountPicker()
      const defaultOption = wrapper.find('[data-testid="picker-option-default"]')
      await defaultOption.trigger('click')
      expect(wrapper.emitted('select')![0]).toEqual([
        { collectionId: null, folderId: undefined }
      ])
    })
  })

  describe('display modes', () => {
    it('renders as inline when mode is inline', () => {
      const wrapper = mountPicker({ mode: 'inline' })
      expect(wrapper.find('[data-testid="collection-picker"]').classes()).toContain('inline')
    })

    it('renders as modal when mode is modal', () => {
      const wrapper = mountPicker({ mode: 'modal' })
      expect(wrapper.find('[data-testid="collection-picker-modal"]').exists()).toBe(true)
    })
  })

  describe('disabled state', () => {
    it('shows disabled state when no collections exist', () => {
      const wrapper = mountPicker({ collections: [] })
      const picker = wrapper.find('[data-testid="collection-picker"]')
      expect(picker.attributes('aria-disabled')).toBe('true')
    })

    it('shows a message when disabled with no collections', () => {
      const wrapper = mountPicker({ collections: [] })
      expect(wrapper.text()).toContain('No collections')
    })
  })
})
