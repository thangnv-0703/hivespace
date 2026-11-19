<script setup lang="ts">
import { Check, Circle, CircleAlert } from '@lucide/vue'

export interface PasswordRule {
  key: string
  label: string
  met: boolean
}

/**
 * Live password checklist (ui-design E-2). A hint only: the API is the authority.
 * Unmet rules stay muted until `showErrors`, then change icon and tone (colour is never the only signal).
 */
defineProps<{ rules: PasswordRule[]; showErrors?: boolean; label?: string }>()
</script>

<template>
  <ul class="mt-2 space-y-1" :aria-label="label" data-testid="password-rules">
    <li
      v-for="rule in rules"
      :key="rule.key"
      :data-state="rule.met ? 'met' : showErrors ? 'error' : 'idle'"
      :class="[
        'flex items-center gap-2 text-xs',
        rule.met ? 'text-feedback-success-fg' : showErrors ? 'text-feedback-error-fg' : 'text-text-muted',
      ]"
    >
      <Check v-if="rule.met" class="size-4 shrink-0" aria-hidden="true" />
      <CircleAlert v-else-if="showErrors" class="size-4 shrink-0" aria-hidden="true" />
      <Circle v-else class="size-4 shrink-0" aria-hidden="true" />
      <span>{{ rule.label }}</span>
    </li>
  </ul>
</template>
