<script setup lang="ts">
import type { Component } from 'vue'
import Spinner from './Spinner.vue'

/** Button variants per design.md §4.1. No success/warning: status is shown with Badge and Alert. */
const props = withDefaults(
  defineProps<{
    variant?: 'primary' | 'secondary' | 'primary-outline' | 'ghost' | 'danger'
    size?: 'sm' | 'md'
    type?: 'button' | 'submit' | 'reset'
    /** Renders a link that looks like the button (navigation, not an action). */
    href?: string
    disabled?: boolean
    loading?: boolean
    iconOnly?: boolean
    startIcon?: Component
    endIcon?: Component
    /** Required when `iconOnly`. */
    ariaLabel?: string
  }>(),
  {
    variant: 'primary',
    size: 'md',
    type: 'button',
    href: undefined,
    disabled: false,
    loading: false,
    iconOnly: false,
    startIcon: undefined,
    endIcon: undefined,
    ariaLabel: undefined,
  },
)

const variants = {
  primary:
    'bg-action-primary-bg text-action-primary-fg hover:bg-action-primary-bg-hover active:bg-action-primary-bg-active disabled:bg-action-primary-bg-disabled',
  secondary:
    'border border-action-secondary-border bg-action-secondary-bg text-action-secondary-fg shadow-xs hover:bg-action-secondary-bg-hover',
  'primary-outline': 'border border-action-primary-bg text-text-link hover:bg-bg-subtle',
  ghost: 'text-text-secondary hover:bg-bg-subtle',
  danger: 'bg-action-danger-bg text-action-danger-fg hover:bg-action-danger-bg-hover',
}
const heights = { sm: 'min-h-9 text-sm', md: 'min-h-11 text-sm' }
const squares = { sm: 'size-9', md: 'size-11' }
</script>

<template>
  <component
    :is="href ? 'a' : 'button'"
    :href="href"
    :type="href ? undefined : type"
    :disabled="href ? undefined : disabled || loading"
    :aria-busy="loading || undefined"
    :aria-label="iconOnly ? ariaLabel : undefined"
    :class="[
      'inline-flex shrink-0 items-center justify-center gap-2 rounded-lg font-medium transition-colors duration-150 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring disabled:cursor-not-allowed disabled:opacity-50',
      iconOnly ? squares[props.size] : [heights[props.size], 'px-4'],
      variants[props.variant],
    ]"
  >
    <Spinner v-if="loading" size="sm" />
    <component :is="startIcon" v-else-if="startIcon" class="size-5" aria-hidden="true" />
    <slot />
    <component :is="endIcon" v-if="endIcon && !loading" class="size-5" aria-hidden="true" />
  </component>
</template>
