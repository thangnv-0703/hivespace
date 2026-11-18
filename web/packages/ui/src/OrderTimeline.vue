<script setup lang="ts">
import { Check } from '@lucide/vue'

/** Order status steps (FR-ORD-002). The reference used a `primary` colour that does not exist here: the brand action token is used. */
export interface TimelineStep {
  key: string
  label: string
  timestamp?: string | null
  isCompleted: boolean
  isCurrent: boolean
}

defineProps<{ steps: TimelineStep[]; label: string }>()
</script>

<template>
  <ol class="flex items-start overflow-x-auto" :aria-label="label">
    <li v-for="(step, index) in steps" :key="step.key" class="flex min-w-0 flex-1 flex-col items-center" :aria-current="step.isCurrent ? 'step' : undefined">
      <div class="flex w-full items-center" aria-hidden="true">
        <span :class="['h-0.5 flex-1', index === 0 ? 'invisible' : step.isCompleted || step.isCurrent ? 'bg-action-primary-bg' : 'bg-border-default']" />
        <span
          :class="[
            'flex size-7 shrink-0 items-center justify-center rounded-full border-2',
            step.isCompleted || step.isCurrent ? 'border-action-primary-bg bg-action-primary-bg text-action-primary-fg' : 'border-border-control bg-bg-surface',
          ]"
        >
          <Check v-if="step.isCompleted || step.isCurrent" class="size-3.5" stroke-width="3" />
        </span>
        <span :class="['h-0.5 flex-1', index === steps.length - 1 ? 'invisible' : step.isCompleted ? 'bg-action-primary-bg' : 'bg-border-default']" />
      </div>
      <span :class="['mt-2 w-full truncate px-1 text-center text-xs font-medium', step.isCompleted || step.isCurrent ? 'text-text-link' : 'text-text-muted']">{{ step.label }}</span>
      <span v-if="step.timestamp" class="mt-0.5 px-1 text-center text-xs text-text-muted">{{ step.timestamp }}</span>
    </li>
  </ol>
</template>
