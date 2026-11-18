<script setup lang="ts">
import Spinner from './Spinner.vue'

/** Overlay + spinner for route-level blocking work only (design.md §4.3), such as placing an order. */
defineProps<{ visible: boolean; message: string }>()
</script>

<template>
  <Teleport to="body">
    <div v-if="visible" class="fixed inset-0 z-[var(--z-modal)] flex items-center justify-center bg-bg-overlay" role="status" aria-live="polite">
      <div class="flex flex-col items-center gap-3 rounded-xl bg-bg-surface px-8 py-6 shadow-lg">
        <Spinner />
        <p class="text-sm font-medium text-text-secondary">{{ message }}</p>
      </div>
    </div>
  </Teleport>
</template>
