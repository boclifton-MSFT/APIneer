<script setup lang="ts">
defineOptions({ name: 'AppCommandPalette' })

const isOpen = ref(false)
const router = useRouter()

const groups = computed(() => [
  {
    id: 'actions',
    label: 'Actions',
    items: [
      {
        label: 'New Request',
        icon: 'i-lucide-plus',
        kbds: ['meta', 'N'],
        onSelect: () => {
          isOpen.value = false
          router.push('/')
        }
      },
      {
        label: 'Settings',
        icon: 'i-lucide-settings',
        kbds: ['meta', ','],
        onSelect: () => {
          isOpen.value = false
        }
      }
    ]
  },
  {
    id: 'navigation',
    label: 'Navigation',
    items: [
      {
        label: 'Requests',
        icon: 'i-lucide-send',
        onSelect: () => {
          isOpen.value = false
          router.push('/')
        }
      },
      {
        label: 'Collections',
        icon: 'i-lucide-folder-open',
        onSelect: () => {
          isOpen.value = false
          router.push('/collections')
        }
      },
      {
        label: 'History',
        icon: 'i-lucide-history',
        onSelect: () => {
          isOpen.value = false
          router.push('/history')
        }
      },
      {
        label: 'Environments',
        icon: 'i-lucide-layers',
        onSelect: () => {
          isOpen.value = false
          router.push('/environments')
        }
      }
    ]
  }
])

defineShortcuts({
  meta_k: () => {
    isOpen.value = !isOpen.value
  },
  meta_n: () => {
    router.push('/')
  }
})
</script>

<template>
  <UModal v-model:open="isOpen" :ui="{ content: 'p-0' }">
    <template #body>
      <UCommandPalette
        :groups="groups"
        placeholder="Type a command or search..."
        class="h-80"
        @update:model-value="isOpen = false"
      />
    </template>
  </UModal>
</template>
