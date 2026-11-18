<script setup lang="ts">
import { CircleAlert, CircleCheck, Info, TriangleAlert } from '@lucide/vue'

defineProps<{ variant: 'success' | 'error' | 'warning' | 'info'; title: string; message?: string }>()

const styles = {
  success: { box: 'border-feedback-success-icon bg-feedback-success-bg', icon: 'text-feedback-success-icon', component: CircleCheck },
  error: { box: 'border-feedback-error-icon bg-feedback-error-bg', icon: 'text-feedback-error-icon', component: CircleAlert },
  warning: { box: 'border-feedback-warning-icon bg-feedback-warning-bg', icon: 'text-feedback-warning-icon', component: TriangleAlert },
  info: { box: 'border-feedback-info-icon bg-feedback-info-bg', icon: 'text-feedback-info-icon', component: Info },
}
</script>

<template>
  <div :class="['flex items-start gap-3 rounded-xl border p-4', styles[variant].box]" :role="variant === 'error' ? 'alert' : 'status'">
    <component :is="styles[variant].component" :class="['mt-0.5 size-5 shrink-0', styles[variant].icon]" aria-hidden="true" />
    <div>
      <p class="text-sm font-semibold text-text-primary">{{ title }}</p>
      <p v-if="message" class="mt-1 text-sm text-text-secondary">{{ message }}</p>
    </div>
  </div>
</template>
