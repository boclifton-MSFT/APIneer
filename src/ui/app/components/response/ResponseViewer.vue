<script setup lang="ts">
import StatusBadge from '~/components/response/StatusBadge.vue'
import ResponseBody from '~/components/response/ResponseBody.vue'
import ResponseHeaders from '~/components/response/ResponseHeaders.vue'
import ResponseTiming from '~/components/response/ResponseTiming.vue'

interface ResponseData {
  statusCode: number
  statusText: string
  body: string
  contentType: string
  headers: { name: string; value: string }[]
  timeMs: number
  sizeBytes: number
  error?: string
}

const props = defineProps<{
  response?: ResponseData | null
}>()

const activeTab = ref<'Body' | 'Headers' | 'Timing'>('Body')
const tabs = ['Body', 'Headers', 'Timing'] as const

const isEmpty = computed(() => !props.response)

function setTab(tab: typeof tabs[number]) {
  activeTab.value = tab
}
</script>

<template>
  <div>
    <template v-if="isEmpty">
      <div class="p-8 text-center text-muted">Send a request to see the response</div>
    </template>
    <template v-else-if="response!.error">
      <div class="p-4 rounded-lg bg-error/10 border border-error/30">
        <div class="flex items-center gap-2 mb-1">
          <UIcon name="i-lucide-alert-circle" class="size-4 text-error" />
          <span class="font-semibold text-error text-sm">Request Error</span>
        </div>
        <p class="text-sm text-error/80">{{ response!.error }}</p>
      </div>
    </template>
    <template v-else>
      <div class="flex items-center gap-4 mb-4">
        <StatusBadge
          :status-code="response!.statusCode"
          :status-text="response!.statusText"
        />
        <div class="flex gap-2">
          <button
            v-for="tab in tabs"
            :key="tab"
            data-testid="response-tab"
            :class="{ active: activeTab === tab }"
            class="px-3 py-1 text-sm rounded"
            @click="setTab(tab)"
          >
            {{ tab }}
          </button>
        </div>
      </div>

      <ResponseBody
        v-if="activeTab === 'Body'"
        :body="response!.body"
        :content-type="response!.contentType"
      />
      <ResponseHeaders
        v-if="activeTab === 'Headers'"
        :headers="response!.headers"
      />
      <ResponseTiming
        v-if="activeTab === 'Timing'"
        :time-ms="response!.timeMs"
        :size-bytes="response!.sizeBytes"
      />
    </template>
  </div>
</template>
