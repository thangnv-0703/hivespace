<script setup lang="ts">
export interface TabOption {
  label: string
  value: string
}

withDefaults(defineProps<{ options: TabOption[]; modelValue: string; variant?: 'underline' | 'pills'; label?: string }>(), {
  variant: 'underline',
  label: undefined,
})
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<template>
  <div
    role="tablist"
    :aria-label="label"
    :class="variant === 'pills' ? 'inline-flex gap-1 rounded-lg bg-bg-subtle p-1' : 'flex gap-1 border-b border-border-default'"
  >
    <button
      v-for="option in options"
      :key="option.value"
      type="button"
      role="tab"
      :aria-selected="modelValue === option.value"
      :class="[
        'min-h-9 whitespace-nowrap px-3 text-sm font-medium focus-visible:outline-2 focus-visible:outline-focus-ring',
        variant === 'pills'
          ? ['rounded-md', modelValue === option.value ? 'bg-bg-surface text-text-primary shadow-xs' : 'text-text-muted hover:text-text-primary']
          : ['-mb-px border-b-2', modelValue === option.value ? 'border-action-primary-bg text-text-primary' : 'border-transparent text-text-muted hover:text-text-primary'],
      ]"
      @click="emit('update:modelValue', option.value)"
    >
      {{ option.label }}
    </button>
  </div>
</template>
