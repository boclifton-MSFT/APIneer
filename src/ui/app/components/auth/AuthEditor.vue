<script setup lang="ts">
defineOptions({ name: 'AuthEditor' })

interface AuthConfig {
  type: string
  keyName?: string
  keyValue?: string
  placement?: string
  token?: string
  username?: string
  password?: string
  tokenEndpoint?: string
  clientId?: string
  clientSecret?: string
  scope?: string
}

const props = withDefaults(
  defineProps<{
    modelValue?: AuthConfig
    showInherit?: boolean
    inherit?: boolean
  }>(),
  {
    modelValue: () => ({ type: 'none' }),
    showInherit: false,
    inherit: false,
  },
)

const emit = defineEmits<{
  'update:modelValue': [value: AuthConfig]
  'update:inherit': [value: boolean]
}>()

const AUTH_TYPES = [
  { value: 'none', label: 'None' },
  { value: 'api_key', label: 'API Key' },
  { value: 'bearer', label: 'Bearer Token' },
  { value: 'basic', label: 'Basic Auth' },
  { value: 'oauth2', label: 'OAuth 2.0' },
]

function defaultConfigFor(type: string): AuthConfig {
  switch (type) {
    case 'api_key':
      return { type, keyName: '', keyValue: '', placement: 'header' }
    case 'bearer':
      return { type, token: '' }
    case 'basic':
      return { type, username: '', password: '' }
    case 'oauth2':
      return { type, tokenEndpoint: '', clientId: '', clientSecret: '', scope: '' }
    default:
      return { type: 'none' }
  }
}

function onTypeChange(event: Event) {
  const newType = (event.target as HTMLSelectElement).value
  emit('update:modelValue', defaultConfigFor(newType))
}

function onFieldChange(field: string, event: Event) {
  const value = (event.target as HTMLInputElement).value
  emit('update:modelValue', { ...props.modelValue, [field]: value })
}

function onInheritChange(event: Event) {
  const checked = (event.target as HTMLInputElement).checked
  emit('update:inherit', checked)
}
</script>

<template>
  <div class="auth-editor">
    <select
      data-testid="auth-type-selector"
      :value="modelValue.type"
      @change="onTypeChange"
    >
      <option v-for="opt in AUTH_TYPES" :key="opt.value" :value="opt.value">
        {{ opt.label }}
      </option>
    </select>

    <!-- API Key fields -->
    <template v-if="modelValue.type === 'api_key'">
      <input
        data-testid="apikey-key-name"
        :value="modelValue.keyName"
        placeholder="Key Name"
        @input="onFieldChange('keyName', $event)"
      />
      <input
        data-testid="apikey-key-value"
        :value="modelValue.keyValue"
        placeholder="Key Value"
        @input="onFieldChange('keyValue', $event)"
      />
      <select
        data-testid="apikey-placement"
        :value="modelValue.placement"
        @change="onFieldChange('placement', $event)"
      >
        <option value="header">Header</option>
        <option value="query">Query</option>
      </select>
    </template>

    <!-- Bearer Token -->
    <template v-if="modelValue.type === 'bearer'">
      <input
        data-testid="bearer-token"
        :value="modelValue.token"
        placeholder="Token"
        @input="onFieldChange('token', $event)"
      />
    </template>

    <!-- Basic Auth -->
    <template v-if="modelValue.type === 'basic'">
      <input
        data-testid="basic-username"
        :value="modelValue.username"
        placeholder="Username"
        @input="onFieldChange('username', $event)"
      />
      <input
        data-testid="basic-password"
        :value="modelValue.password"
        type="password"
        placeholder="Password"
        @input="onFieldChange('password', $event)"
      />
    </template>

    <!-- OAuth 2.0 -->
    <template v-if="modelValue.type === 'oauth2'">
      <input
        data-testid="oauth2-token-endpoint"
        :value="modelValue.tokenEndpoint"
        placeholder="Token Endpoint"
        @input="onFieldChange('tokenEndpoint', $event)"
      />
      <input
        data-testid="oauth2-client-id"
        :value="modelValue.clientId"
        placeholder="Client ID"
        @input="onFieldChange('clientId', $event)"
      />
      <input
        data-testid="oauth2-client-secret"
        :value="modelValue.clientSecret"
        type="password"
        placeholder="Client Secret"
        @input="onFieldChange('clientSecret', $event)"
      />
      <input
        data-testid="oauth2-scope"
        :value="modelValue.scope"
        placeholder="Scope"
        @input="onFieldChange('scope', $event)"
      />
    </template>

    <!-- Inherit toggle -->
    <template v-if="showInherit">
      <label class="auth-inherit-label">
        <input
          data-testid="auth-inherit-toggle"
          type="checkbox"
          :checked="inherit"
          @change="onInheritChange"
        />
        Inherit from collection
      </label>
    </template>
  </div>
</template>

<style scoped>
.auth-editor {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.auth-editor select,
.auth-editor input[type="text"],
.auth-editor input[type="password"],
.auth-editor input:not([type]) {
  width: 100%;
  padding: 0.5rem 0.75rem;
  font-size: 0.875rem;
  border: 1px solid var(--ui-border, #e5e7eb);
  border-radius: 0.375rem;
  background: var(--ui-bg, #fff);
  color: var(--ui-text, #111827);
  outline: none;
  transition: border-color 0.15s;
}

.auth-editor select:focus,
.auth-editor input:focus {
  border-color: var(--ui-primary, #2563eb);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--ui-primary, #2563eb) 20%, transparent);
}

.auth-editor input::placeholder {
  color: var(--ui-text-muted, #6b7280);
}

.auth-inherit-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.875rem;
  color: var(--ui-text-muted, #6b7280);
  cursor: pointer;
}
</style>
