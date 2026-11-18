<script setup lang="ts">
import { useId } from 'vue'

/** Radio (design.md §4.2). Native radio behaviour: a chosen option stays chosen (the reference allowed unchecking). */
const props = withDefaults(
  defineProps<{ modelValue: string | number | boolean | null; value: string | number | boolean; name: string; disabled?: boolean }>(),
  { disabled: false },
)
const emit = defineEmits<{ 'update:modelValue': [value: string | number | boolean] }>()

const id = useId()
const selected = () => props.modelValue === props.value
</script>

<template>
  <label :for="id" :class="['flex min-h-11 select-none items-center gap-3 text-sm font-medium', disabled ? 'cursor-not-allowed text-text-disabled' : 'text-text-secondary']">
    <input
      :id="id"
      type="radio"
      class="peer sr-only"
      :name="name"
      :value="String(value)"
      :checked="selected()"
      :disabled="disabled"
      @change="emit('update:modelValue', value)"
    />
    <span
      :class="[
        'flex size-5 shrink-0 items-center justify-center rounded-full border peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-focus-ring',
        selected() ? 'border-action-primary-bg bg-action-primary-bg' : 'border-border-control bg-bg-surface',
        disabled ? 'opacity-50' : '',
      ]"
      aria-hidden="true"
    >
      <span :class="['size-2 rounded-full bg-action-primary-fg transition-transform', selected() ? 'scale-100' : 'scale-0']" />
    </span>
    <span><slot /></span>
  </label>
</template>
