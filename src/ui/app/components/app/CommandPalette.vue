<script setup lang="ts">
defineOptions({ name: 'AppCommandPalette' })

const isOpen = ref(false)
const router = useRouter()
const api = useApi()
const toast = useToast()

async function createNewRequest() {
  try {
    const req = await api.createRequest({
      name: 'Untitled Request',
      method: 'GET',
      url: ''
    })
    isOpen.value = false
    await router.push({ path: '/', query: { request: req.id } })
    toast.add({ title: 'Request created', color: 'success', icon: 'i-lucide-check' })
  } catch {
    isOpen.value = false
    router.push('/')
  }
}

const groups = computed(() => [
  {
    id: 'actions',
    label: 'Actions',
    items: [
      {
        label: 'New Request',
        icon: 'i-lucide-plus',
        kbds: ['meta', 'N'],
        onSelect: () => createNewRequest()
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
    createNewRequest()
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
