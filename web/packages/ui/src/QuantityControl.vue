<script setup lang="ts">
import { Minus, Plus } from '@lucide/vue'

/** `[-] value [+]` pill (design.md §4.2). Buttons stop at `min` and `max`; the "Only N left" message is the screen's job. */
const props = withDefaults(
  defineProps<{
    modelValue: number
    min?: number
    max?: number
    /** Accessible names come from the app's i18n. */
    labels: { decrease: string; increase: string; quantity: string }
  }>(),
  { min: 1, max: undefined },
)
const emit = defineEmits<{ 'update:modelValue': [value: number] }>()

const clamp = (value: number) => Math.min(props.max ?? Infinity, Math.max(props.min, value))

function commit(event: Event) {
  const input = event.target as HTMLInputElement
  const parsed = Number.parseInt(input.value, 10)
  const next = Number.isNaN(parsed) ? props.modelValue : clamp(parsed)
  input.value = String(next)
  if (next !== props.modelValue) emit('update:modelValue', next)
}

const side =
  'inline-flex size-9 items-center justify-center text-text-secondary hover:bg-bg-subtle focus-visible:outline-2 focus-visible:outline-focus-ring disabled:opacity-40'
</script>

<template>
  <div class="inline-flex items-center overflow-hidden rounded-full border border-border-control bg-bg-surface">
    <button type="button" :class="side" :disabled="modelValue <= min" :aria-label="labels.decrease" @click="emit('update:modelValue', clamp(modelValue - 1))">
      <Minus class="size-4" aria-hidden="true" />
    </button>
    <input
      type="text"
      inputmode="numeric"
      :value="modelValue"
      :aria-label="labels.quantity"
      class="h-9 w-12 border-x border-border-default bg-transparent text-center text-sm text-text-primary focus-visible:outline-2 focus-visible:outline-focus-ring"
      @change="commit"
    />
    <button type="button" :class="side" :disabled="max !== undefined && modelValue >= max" :aria-label="labels.increase" @click="emit('update:modelValue', clamp(modelValue + 1))">
      <Plus class="size-4" aria-hidden="true" />
    </button>
  </div>
</template>
