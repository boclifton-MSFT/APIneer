<script setup lang="ts">
import type { Environment, EnvironmentVariable } from '~/composables/useApi'
import EnvironmentSelector from '~/components/environments/EnvironmentSelector.vue'

definePageMeta({
  layout: 'dashboard'
})

const api = useApi()
const toast = useToast()

const environments = ref<Environment[]>([])
const loading = ref(false)
const selectedEnvId = ref('')
const selectedEnv = ref<Environment | null>(null)

// New environment dialog
const showNewDialog = ref(false)
const newEnvName = ref('')

// Edit dialog
const showEditDialog = ref(false)
const editEnvName = ref('')
const editEnvId = ref('')

// Variable editing
const newVarKey = ref('')
const newVarValue = ref('')
const newVarIsSecret = ref(false)

async function loadEnvironments() {
  loading.value = true
  try {
    environments.value = await api.getEnvironments()
  } catch {
    environments.value = []
  } finally {
    loading.value = false
  }
}

async function selectEnvironment(id: string) {
  selectedEnvId.value = id
  if (!id) {
    selectedEnv.value = null
    return
  }
  try {
    selectedEnv.value = await api.getEnvironment(id)
  } catch {
    selectedEnv.value = null
    toast.add({ title: 'Failed to load environment', color: 'error' })
  }
}

async function createEnvironment() {
  if (!newEnvName.value.trim()) return
  try {
    const env = await api.createEnvironment({ name: newEnvName.value.trim() })
    newEnvName.value = ''
    showNewDialog.value = false
    await loadEnvironments()
    await selectEnvironment(env.id)
    toast.add({ title: 'Environment created', color: 'success', icon: 'i-lucide-check' })
  } catch {
    toast.add({ title: 'Failed to create environment', color: 'error' })
  }
}

function startEdit(env: Environment) {
  editEnvId.value = env.id
  editEnvName.value = env.name
  showEditDialog.value = true
}

async function saveEdit() {
  if (!editEnvName.value.trim()) return
  try {
    await api.updateEnvironment(editEnvId.value, { name: editEnvName.value.trim() })
    showEditDialog.value = false
    await loadEnvironments()
    if (selectedEnvId.value === editEnvId.value) {
      await selectEnvironment(editEnvId.value)
    }
    toast.add({ title: 'Environment updated', color: 'success' })
  } catch {
    toast.add({ title: 'Failed to update environment', color: 'error' })
  }
}

async function deleteEnvironment(id: string) {
  try {
    await api.deleteEnvironment(id)
    if (selectedEnvId.value === id) {
      selectedEnv.value = null
      selectedEnvId.value = ''
    }
    await loadEnvironments()
    toast.add({ title: 'Environment deleted', color: 'success' })
  } catch {
    toast.add({ title: 'Failed to delete environment', color: 'error' })
  }
}

// Variable management
async function addVariable() {
  if (!selectedEnv.value || !newVarKey.value.trim()) return
  try {
    await api.addVariable(selectedEnv.value.id, {
      key: newVarKey.value.trim(),
      value: newVarValue.value,
      isSecret: newVarIsSecret.value
    })
    newVarKey.value = ''
    newVarValue.value = ''
    newVarIsSecret.value = false
    await selectEnvironment(selectedEnv.value.id)
    toast.add({ title: 'Variable added', color: 'success' })
  } catch {
    toast.add({ title: 'Failed to add variable', color: 'error' })
  }
}

async function deleteVariable(variableId: string) {
  if (!selectedEnv.value) return
  try {
    await api.deleteVariable(selectedEnv.value.id, variableId)
    await selectEnvironment(selectedEnv.value.id)
    toast.add({ title: 'Variable deleted', color: 'success' })
  } catch {
    toast.add({ title: 'Failed to delete variable', color: 'error' })
  }
}

onMounted(() => {
  loadEnvironments()
})
</script>

<template>
  <UDashboardPanel id="env-list" :resizable="true" class="min-w-64 max-w-sm">
    <template #header>
      <UDashboardNavbar title="Environments">
        <template #right>
          <UButton
            icon="i-lucide-plus"
            size="xs"
            variant="ghost"
            color="neutral"
            aria-label="New Environment"
            @click="showNewDialog = true"
          />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <div v-if="loading" class="flex items-center justify-center p-8">
        <UIcon name="i-lucide-loader-2" class="size-5 animate-spin text-muted" />
      </div>

      <div v-else-if="environments.length === 0" class="flex flex-col items-center justify-center h-full gap-4 p-8">
        <div class="flex items-center justify-center size-12 rounded-xl bg-primary/10">
          <UIcon name="i-lucide-layers" class="size-6 text-primary" />
        </div>
        <div class="text-center">
          <h3 class="text-sm font-semibold text-highlighted">No environments</h3>
          <p class="text-xs text-muted mt-1">Create an environment to manage variables.</p>
        </div>
        <UButton
          label="New Environment"
          icon="i-lucide-plus"
          size="sm"
          @click="showNewDialog = true"
        />
      </div>

      <div v-else class="flex flex-col">
        <!-- Active environment selector -->
        <div class="p-3 border-b border-default">
          <EnvironmentSelector
            :environments="environments"
            :model-value="selectedEnvId"
            @update:model-value="selectEnvironment"
            @activate="selectEnvironment"
          />
        </div>

        <!-- Environment list -->
        <button
          v-for="env in environments"
          :key="env.id"
          class="flex items-center justify-between px-3 py-2 text-left text-sm hover:bg-elevated border-b border-default transition-colors"
          :class="{ 'bg-elevated': selectedEnvId === env.id }"
          @click="selectEnvironment(env.id)"
        >
          <div class="flex items-center gap-2">
            <UIcon name="i-lucide-layers" class="size-4 text-muted" />
            <span>{{ env.name }}</span>
            <UBadge v-if="env.isActive" label="Active" color="success" variant="subtle" size="xs" />
          </div>
          <div class="flex items-center gap-1">
            <UButton
              icon="i-lucide-pencil"
              size="2xs"
              variant="ghost"
              color="neutral"
              @click.stop="startEdit(env)"
            />
            <UButton
              icon="i-lucide-trash-2"
              size="2xs"
              variant="ghost"
              color="error"
              @click.stop="deleteEnvironment(env.id)"
            />
          </div>
        </button>
      </div>
    </template>
  </UDashboardPanel>

  <UDashboardPanel id="env-detail" class="hidden lg:flex">
    <template #header>
      <UDashboardNavbar :title="selectedEnv?.name || 'Environment Variables'" />
    </template>

    <template #body>
      <div v-if="!selectedEnv" class="flex flex-col items-center justify-center h-full gap-4">
        <div class="flex items-center justify-center size-16 rounded-xl bg-primary/10">
          <UIcon name="i-lucide-layers" class="size-8 text-primary" />
        </div>
        <div class="text-center">
          <h2 class="text-lg font-semibold text-highlighted">Select an environment</h2>
          <p class="text-sm text-muted mt-1">Choose an environment from the sidebar to manage its variables.</p>
        </div>
      </div>

      <div v-else class="p-4 space-y-6">
        <div>
          <h3 class="text-sm font-semibold text-highlighted mb-3">Variables</h3>

          <!-- Variable table -->
          <table v-if="selectedEnv.variables && selectedEnv.variables.length > 0" class="w-full text-sm">
            <thead>
              <tr class="border-b border-default">
                <th class="text-left py-2 px-3 font-semibold text-xs uppercase text-muted">Key</th>
                <th class="text-left py-2 px-3 font-semibold text-xs uppercase text-muted">Value</th>
                <th class="text-left py-2 px-3 font-semibold text-xs uppercase text-muted">Secret</th>
                <th class="py-2 px-3 w-16"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="variable in selectedEnv.variables" :key="variable.id" class="border-b border-default hover:bg-elevated">
                <td class="py-2 px-3 font-mono">{{ variable.key }}</td>
                <td class="py-2 px-3 font-mono">{{ variable.isSecret ? '***masked***' : variable.value }}</td>
                <td class="py-2 px-3">
                  <UBadge v-if="variable.isSecret" label="Secret" color="warning" variant="subtle" size="xs" />
                </td>
                <td class="py-2 px-3">
                  <UButton
                    icon="i-lucide-trash-2"
                    size="2xs"
                    variant="ghost"
                    color="error"
                    @click="deleteVariable(variable.id)"
                  />
                </td>
              </tr>
            </tbody>
          </table>

          <div v-else class="text-sm text-muted py-4 text-center">
            No variables defined. Add one below.
          </div>
        </div>

        <!-- Add variable form -->
        <USeparator />
        <div>
          <h4 class="text-sm font-semibold text-highlighted mb-3">Add Variable</h4>
          <div class="flex items-end gap-3">
            <UFormField label="Key" class="flex-1">
              <UInput v-model="newVarKey" placeholder="API_KEY" />
            </UFormField>
            <UFormField label="Value" class="flex-1">
              <UInput v-model="newVarValue" placeholder="your-value-here" :type="newVarIsSecret ? 'password' : 'text'" />
            </UFormField>
            <div class="flex items-center">
              <UCheckbox v-model="newVarIsSecret" label="Secret" />
            </div>
            <UButton
              label="Add"
              icon="i-lucide-plus"
              size="sm"
              :disabled="!newVarKey.trim()"
              @click="addVariable"
            />
          </div>
        </div>
      </div>
    </template>
  </UDashboardPanel>

  <!-- New Environment Modal -->
  <UModal v-model:open="showNewDialog" title="New Environment" description="Create a new environment for variable management.">
    <template #body>
      <UFormField label="Environment Name" required>
        <UInput
          v-model="newEnvName"
          placeholder="Development"
          autofocus
          @keydown.enter="createEnvironment"
        />
      </UFormField>
    </template>
    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton variant="ghost" color="neutral" label="Cancel" @click="showNewDialog = false" />
        <UButton label="Create" icon="i-lucide-plus" :disabled="!newEnvName.trim()" @click="createEnvironment" />
      </div>
    </template>
  </UModal>

  <!-- Edit Environment Modal -->
  <UModal v-model:open="showEditDialog" title="Edit Environment">
    <template #body>
      <UFormField label="Environment Name" required>
        <UInput
          v-model="editEnvName"
          autofocus
          @keydown.enter="saveEdit"
        />
      </UFormField>
    </template>
    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton variant="ghost" color="neutral" label="Cancel" @click="showEditDialog = false" />
        <UButton label="Save" :disabled="!editEnvName.trim()" @click="saveEdit" />
      </div>
    </template>
  </UModal>
</template>
