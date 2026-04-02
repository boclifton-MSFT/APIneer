import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import InlineRename from '~/components/collections/InlineRename.vue'

describe('InlineRename', () => {
  function mountRename(props: Record<string, unknown> = {}) {
    return mount(InlineRename, {
      props: {
        value: 'My Request',
        ...props
      }
    })
  }

  describe('display mode', () => {
    it('renders text content in display mode', () => {
      const wrapper = mountRename()
      expect(wrapper.find('[data-testid="inline-rename-display"]').exists()).toBe(true)
      expect(wrapper.text()).toContain('My Request')
    })

    it('does not show input field in display mode', () => {
      const wrapper = mountRename()
      expect(wrapper.find('[data-testid="inline-rename-input"]').exists()).toBe(false)
    })
  })

  describe('edit mode activation', () => {
    it('activates edit mode on double-click', async () => {
      const wrapper = mountRename()
      const display = wrapper.find('[data-testid="inline-rename-display"]')
      await display.trigger('dblclick')
      expect(wrapper.find('[data-testid="inline-rename-input"]').exists()).toBe(true)
    })

    it('input is auto-focused when edit mode activates', async () => {
      const wrapper = mountRename({ attachTo: document.body })
      const display = wrapper.find('[data-testid="inline-rename-display"]')
      await display.trigger('dblclick')
      const input = wrapper.find('[data-testid="inline-rename-input"]')
      expect(input.element).toBe(document.activeElement)
      wrapper.unmount()
    })

    it('input contains the current name value', async () => {
      const wrapper = mountRename({ value: 'Original Name' })
      const display = wrapper.find('[data-testid="inline-rename-display"]')
      await display.trigger('dblclick')
      const input = wrapper.find<HTMLInputElement>('[data-testid="inline-rename-input"]')
      expect(input.element.value).toBe('Original Name')
    })
  })

  describe('saving', () => {
    it('Enter key saves and emits rename with new value', async () => {
      const wrapper = mountRename({ value: 'Old Name' })
      await wrapper.find('[data-testid="inline-rename-display"]').trigger('dblclick')
      const input = wrapper.find('[data-testid="inline-rename-input"]')
      await input.setValue('New Name')
      await input.trigger('keydown.enter')
      expect(wrapper.emitted('rename')).toBeTruthy()
      expect(wrapper.emitted('rename')![0]).toEqual(['New Name'])
    })

    it('clicking outside the input saves', async () => {
      const wrapper = mountRename({ value: 'Old Name', attachTo: document.body })
      await wrapper.find('[data-testid="inline-rename-display"]').trigger('dblclick')
      const input = wrapper.find('[data-testid="inline-rename-input"]')
      await input.setValue('Blurred Name')
      await input.trigger('blur')
      expect(wrapper.emitted('rename')).toBeTruthy()
      expect(wrapper.emitted('rename')![0]).toEqual(['Blurred Name'])
      wrapper.unmount()
    })

    it('shows the saved name after rename completes', async () => {
      const wrapper = mountRename({ value: 'Old Name' })
      await wrapper.find('[data-testid="inline-rename-display"]').trigger('dblclick')
      const input = wrapper.find('[data-testid="inline-rename-input"]')
      await input.setValue('Updated Name')
      await input.trigger('keydown.enter')
      // After saving, should return to display mode with the new name
      await wrapper.setProps({ value: 'Updated Name' })
      expect(wrapper.find('[data-testid="inline-rename-display"]').text()).toContain('Updated Name')
    })
  })

  describe('canceling', () => {
    it('Escape key cancels and emits cancel event', async () => {
      const wrapper = mountRename({ value: 'Original' })
      await wrapper.find('[data-testid="inline-rename-display"]').trigger('dblclick')
      const input = wrapper.find('[data-testid="inline-rename-input"]')
      await input.setValue('Changed')
      await input.trigger('keydown.escape')
      expect(wrapper.emitted('cancel')).toBeTruthy()
      // Should revert to display mode with original name
      expect(wrapper.find('[data-testid="inline-rename-display"]').text()).toContain('Original')
    })
  })

  describe('validation', () => {
    it('empty name reverts to original and does not emit rename', async () => {
      const wrapper = mountRename({ value: 'Valid Name' })
      await wrapper.find('[data-testid="inline-rename-display"]').trigger('dblclick')
      const input = wrapper.find('[data-testid="inline-rename-input"]')
      await input.setValue('')
      await input.trigger('keydown.enter')
      // Should NOT emit rename with empty string
      const renameEvents = wrapper.emitted('rename')
      expect(!renameEvents || renameEvents.length === 0).toBe(true)
      // Should revert to original
      expect(wrapper.find('[data-testid="inline-rename-display"]').text()).toContain('Valid Name')
    })
  })
})
