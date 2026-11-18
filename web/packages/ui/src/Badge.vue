<script setup lang="ts">
/** Status badge (design.md §4.3): status is shown with a Badge, never with a coloured button. */
withDefaults(defineProps<{ tone?: 'neutral' | 'primary' | 'success' | 'warning' | 'error' | 'info'; size?: 'sm' | 'md' }>(), {
  tone: 'neutral',
  size: 'md',
})
const tones = {
  neutral: 'bg-bg-subtle text-text-secondary',
  primary: 'bg-brand-50 text-brand-700 dark:bg-brand-950 dark:text-brand-300',
  success: 'bg-feedback-success-bg text-feedback-success-fg',
  warning: 'bg-feedback-warning-bg text-feedback-warning-fg',
  error: 'bg-feedback-error-bg text-feedback-error-fg',
  info: 'bg-feedback-info-bg text-feedback-info-fg',
}
</script>

<template>
  <span
    :class="[
      'inline-flex items-center gap-1 whitespace-nowrap rounded-full px-2.5 py-0.5 font-medium',
      size === 'sm' ? 'text-xs' : 'text-sm',
      tones[tone ?? 'neutral'],
    ]"
  >
    <slot />
  </span>
</template>
