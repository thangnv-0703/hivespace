<script setup lang="ts">
defineProps<{ modelValue: string; label: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<template>
  <label class="inline-flex items-center gap-2 text-sm text-text-secondary">
    <span class="sr-only">{{ label }}</span>
    <select
      :value="modelValue"
      class="min-h-11 rounded-lg border border-border-control bg-bg-surface px-2 text-text-primary focus-visible:outline-2 focus-visible:outline-focus-ring"
      data-testid="language-switcher"
      @change="emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
    >
      <option value="vi">Tiếng Việt</option>
      <option value="en">English</option>
    </select>
  </label>
</template>
