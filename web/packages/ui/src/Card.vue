<script setup lang="ts">
/** Card with an optional header (the reference's ComponentCard). Padding follows design.md §4.10 (16 to 24 px). */
defineProps<{ title?: string; description?: string }>()
</script>

<template>
  <section class="rounded-xl border border-border-default bg-bg-surface shadow-xs">
    <header v-if="title || $slots.header" class="border-b border-border-default px-6 py-4">
      <slot name="header">
        <h2 class="text-md font-medium text-text-primary">{{ title }}</h2>
        <p v-if="description" class="mt-1 text-sm text-text-muted">{{ description }}</p>
      </slot>
    </header>
    <div class="p-4 sm:p-6"><slot /></div>
  </section>
</template>
