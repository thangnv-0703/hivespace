<script setup lang="ts">
import { computed } from 'vue'
import Toast from './Toast.vue'

/** Stack of toasts, top-right on desktop and full width on mobile; at most 3 are shown (design.md §4.3). */
export interface ToastData {
  id: string
  variant: 'success' | 'error' | 'warning' | 'info'
  title: string
  message?: string
  /** Defaults: 5000 ms, and 0 (stays until closed) for errors. */
  duration?: number
}

const props = defineProps<{ toasts: ToastData[]; closeLabel: string }>()
defineEmits<{ remove: [id: string] }>()

const visible = computed(() => props.toasts.slice(-3))
const durationOf = (toast: ToastData) => toast.duration ?? (toast.variant === 'error' ? 0 : 5000)
</script>

<template>
  <Teleport to="body">
    <div
      class="pointer-events-none fixed left-4 right-4 top-4 z-[var(--z-toast)] flex flex-col items-center gap-3 sm:left-auto sm:items-end"
      aria-live="polite"
    >
      <Toast
        v-for="toast in visible"
        :key="toast.id"
        :id="toast.id"
        :variant="toast.variant"
        :title="toast.title"
        :message="toast.message"
        :duration="durationOf(toast)"
        :close-label="closeLabel"
        @close="$emit('remove', $event)"
      />
    </div>
  </Teleport>
</template>
