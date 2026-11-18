<script setup lang="ts">
/** Inline loading indicator for areas where the rest of the page stays usable (design.md §4 loading state). */
withDefaults(defineProps<{ size?: 'sm' | 'md' | 'lg'; label?: string }>(), { size: 'md', label: undefined })
const sizes = { sm: 'size-4', md: 'size-8', lg: 'size-12' }
</script>

<template>
  <span class="inline-flex" :role="label ? 'status' : undefined">
    <span
      :class="[sizes[size ?? 'md'], 'inline-block animate-spin rounded-full border-2 border-current border-t-transparent text-action-primary-bg']"
      aria-hidden="true"
    />
    <span v-if="label" class="sr-only">{{ label }}</span>
  </span>
</template>
