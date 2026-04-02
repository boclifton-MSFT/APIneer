import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import CollectionContextMenu from '~/components/collections/CollectionContextMenu.vue'

describe('CollectionContextMenu', () => {
  const collectionItem = {
    id: 'col-1',
    name: 'User API',
    type: 'collection' as const
  }

  const folderItem = {
    id: 'folder-1',
    name: 'Auth',
    type: 'folder' as const,
    collectionId: 'col-1'
  }

  const requestItem = {
    id: 'req-1',
    name: 'Login',
    method: 'POST',
    type: 'request' as const,
    collectionId: 'col-1',
    folderId: 'folder-1'
  }

  function mountMenu(props: Record<string, unknown> = {}) {
    return mount(CollectionContextMenu, {
      props: {
        visible: true,
        item: collectionItem,
        position: { x: 100, y: 200 },
        ...props
      }
    })
  }

  describe('rendering', () => {
    it('renders context menu when visible is true', () => {
      const wrapper = mountMenu()
      expect(wrapper.find('[data-testid="context-menu"]').exists()).toBe(true)
    })

    it('does not render when visible is false', () => {
      const wrapper = mountMenu({ visible: false })
      expect(wrapper.find('[data-testid="context-menu"]').exists()).toBe(false)
    })

    it('positions menu at the specified coordinates', () => {
      const wrapper = mountMenu({ position: { x: 150, y: 300 } })
      const menu = wrapper.find('[data-testid="context-menu"]')
      const style = menu.attributes('style')
      expect(style).toContain('150')
      expect(style).toContain('300')
    })
  })

  describe('collection actions', () => {
    it('shows correct actions for a collection item', () => {
      const wrapper = mountMenu({ item: collectionItem })
      const actions = wrapper.findAll('[data-testid^="menu-action-"]')
      const labels = actions.map(a => a.text())
      expect(labels).toContain('Rename')
      expect(labels).toContain('Delete')
      expect(labels).toContain('Duplicate')
      expect(labels).toContain('New Folder')
      expect(labels).toContain('New Request')
      expect(labels).toContain('Export')
    })

    it('does not show "Move to..." for collections', () => {
      const wrapper = mountMenu({ item: collectionItem })
      const actions = wrapper.findAll('[data-testid^="menu-action-"]')
      const labels = actions.map(a => a.text())
      expect(labels).not.toContain('Move to...')
    })
  })

  describe('folder actions', () => {
    it('shows correct actions for a folder item', () => {
      const wrapper = mountMenu({ item: folderItem })
      const actions = wrapper.findAll('[data-testid^="menu-action-"]')
      const labels = actions.map(a => a.text())
      expect(labels).toContain('Rename')
      expect(labels).toContain('Delete')
      expect(labels).toContain('New Sub-folder')
      expect(labels).toContain('New Request')
    })

    it('does not show Export or Duplicate for folders', () => {
      const wrapper = mountMenu({ item: folderItem })
      const actions = wrapper.findAll('[data-testid^="menu-action-"]')
      const labels = actions.map(a => a.text())
      expect(labels).not.toContain('Export')
      expect(labels).not.toContain('Duplicate')
    })
  })

  describe('request actions', () => {
    it('shows correct actions for a request item', () => {
      const wrapper = mountMenu({ item: requestItem })
      const actions = wrapper.findAll('[data-testid^="menu-action-"]')
      const labels = actions.map(a => a.text())
      expect(labels).toContain('Rename')
      expect(labels).toContain('Delete')
      expect(labels).toContain('Duplicate')
      expect(labels).toContain('Move to...')
    })

    it('does not show New Folder or Export for requests', () => {
      const wrapper = mountMenu({ item: requestItem })
      const actions = wrapper.findAll('[data-testid^="menu-action-"]')
      const labels = actions.map(a => a.text())
      expect(labels).not.toContain('New Folder')
      expect(labels).not.toContain('Export')
    })
  })

  describe('action events', () => {
    it('emits action event with item data when menu item clicked', async () => {
      const wrapper = mountMenu({ item: collectionItem })
      const renameAction = wrapper.find('[data-testid="menu-action-rename"]')
      await renameAction.trigger('click')
      expect(wrapper.emitted('action')).toBeTruthy()
      expect(wrapper.emitted('action')![0]).toEqual([
        { action: 'rename', item: collectionItem }
      ])
    })

    it('emits delete action with correct item data', async () => {
      const wrapper = mountMenu({ item: requestItem })
      const deleteAction = wrapper.find('[data-testid="menu-action-delete"]')
      await deleteAction.trigger('click')
      expect(wrapper.emitted('action')![0]).toEqual([
        { action: 'delete', item: requestItem }
      ])
    })
  })

  describe('menu closing behavior', () => {
    it('emits close after action is selected', async () => {
      const wrapper = mountMenu({ item: collectionItem })
      const renameAction = wrapper.find('[data-testid="menu-action-rename"]')
      await renameAction.trigger('click')
      expect(wrapper.emitted('close')).toBeTruthy()
    })

    it('emits close on click outside the menu', async () => {
      const wrapper = mountMenu({ item: collectionItem })
      const overlay = wrapper.find('[data-testid="context-menu-overlay"]')
      await overlay.trigger('click')
      expect(wrapper.emitted('close')).toBeTruthy()
    })
  })
})
