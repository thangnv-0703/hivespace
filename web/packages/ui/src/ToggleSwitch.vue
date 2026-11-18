<script setup lang="ts">
import { useId } from 'vue'

/** Toggle switch (design.md §4.2): 44 x 24 px track, `role="switch"`. */
withDefaults(defineProps<{ modelValue: boolean; disabled?: boolean }>(), { disabled: false })
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

const id = useId()
</script>

<template>
  <label :for="id" :class="['inline-flex min-h-11 select-none items-center gap-3 text-sm font-medium text-text-secondary', disabled ? 'cursor-not-allowed opacity-50' : '']">
    <input
      :id="id"
      type="checkbox"
      role="switch"
      class="peer sr-only"
      :checked="modelValue"
      :aria-checked="modelValue"
      :disabled="disabled"
      @change="emit('update:modelValue', ($event.target as HTMLInputElement).checked)"
    />
    <span
      :class="[
        'relative block h-6 w-11 shrink-0 rounded-full transition-colors duration-150 peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-focus-ring',
        modelValue ? 'bg-action-primary-bg' : 'bg-gray-300 dark:bg-gray-700',
      ]"
      aria-hidden="true"
    >
      <span :class="['absolute left-0.5 top-0.5 size-5 rounded-full bg-white shadow-sm transition-transform duration-150', modelValue ? 'translate-x-5' : '']" />
    </span>
    <span><slot /></span>
  </label>
</template>
