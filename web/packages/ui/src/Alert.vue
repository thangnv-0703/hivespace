<script setup lang="ts">
import { CircleAlert, CircleCheck, Info, TriangleAlert } from '@lucide/vue'

defineProps<{
  variant: 'success' | 'error' | 'warning' | 'info'
  title: string
  message?: string
  /** Full-width strip (no radius or side borders), for banners under a header. */
  strip?: boolean
}>()

const styles = {
  success: { box: 'border-feedback-success-icon bg-feedback-success-bg', icon: 'text-feedback-success-icon', component: CircleCheck },
  error: { box: 'border-feedback-error-icon bg-feedback-error-bg', icon: 'text-feedback-error-icon', component: CircleAlert },
  warning: { box: 'border-feedback-warning-icon bg-feedback-warning-bg', icon: 'text-feedback-warning-icon', component: TriangleAlert },
  info: { box: 'border-feedback-info-icon bg-feedback-info-bg', icon: 'text-feedback-info-icon', component: Info },
}
</script>

<template>
  <div
    :class="['flex items-start gap-3 border p-4', strip ? 'rounded-none border-x-0' : 'rounded-xl', styles[variant].box]"
    :role="variant === 'error' ? 'alert' : 'status'"
  >
    <component :is="styles[variant].component" :class="['mt-0.5 size-5 shrink-0', styles[variant].icon]" aria-hidden="true" />
    <div class="min-w-0 flex-1">
      <p class="text-sm font-semibold text-text-primary">{{ title }}</p>
      <p v-if="message" class="mt-1 text-sm text-text-secondary">{{ message }}</p>
      <div v-if="$slots.action" class="mt-2 text-sm"><slot name="action" /></div>
    </div>
  </div>
</template>
