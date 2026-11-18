<script setup lang="ts">
export interface ChipOption {
  label: string
  value: string
}

defineProps<{ options: ChipOption[]; modelValue: string; label?: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<template>
  <div role="group" :aria-label="label" class="flex flex-wrap items-center gap-2">
    <button
      v-for="option in options"
      :key="option.value"
      type="button"
      :aria-pressed="modelValue === option.value"
      :class="[
        'min-h-9 whitespace-nowrap rounded-full border px-4 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-focus-ring',
        modelValue === option.value
          ? 'border-action-primary-bg bg-brand-50 text-brand-700 dark:bg-brand-950 dark:text-brand-300'
          : 'border-border-control bg-bg-surface text-text-secondary hover:bg-bg-subtle',
      ]"
      @click="emit('update:modelValue', option.value)"
    >
      {{ option.label }}
    </button>
  </div>
</template>
