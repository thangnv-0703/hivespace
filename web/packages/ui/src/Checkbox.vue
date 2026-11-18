<script setup lang="ts">
import { Check } from '@lucide/vue'
import { useId } from 'vue'

/** Checkbox (design.md §4.2): 20 px box, whole label row clickable, 44 px tall rows for touch. */
withDefaults(defineProps<{ modelValue?: boolean; label?: string; disabled?: boolean }>(), {
  modelValue: false,
  label: undefined,
  disabled: false,
})
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

const id = useId()
</script>

<template>
  <label :for="id" :class="['flex min-h-11 select-none items-center gap-3 text-sm font-medium', disabled ? 'cursor-not-allowed text-text-disabled' : 'text-text-secondary']">
    <input
      :id="id"
      type="checkbox"
      class="peer sr-only"
      :checked="modelValue"
      :disabled="disabled"
      @change="emit('update:modelValue', ($event.target as HTMLInputElement).checked)"
    />
    <span
      :class="[
        'flex size-5 shrink-0 items-center justify-center rounded-md border peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-focus-ring',
        modelValue ? 'border-action-primary-bg bg-action-primary-bg text-action-primary-fg' : 'border-border-control bg-bg-surface',
        disabled ? 'opacity-50' : '',
      ]"
      aria-hidden="true"
    >
      <Check v-if="modelValue" class="size-3.5" stroke-width="3" />
    </span>
    <span><slot>{{ label }}</slot></span>
  </label>
</template>
