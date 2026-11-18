<script setup lang="ts">
import { CircleAlert, CircleCheck, Info, TriangleAlert, X } from '@lucide/vue'
import { onBeforeUnmount, onMounted, ref } from 'vue'

/**
 * Toast (design.md §4.3): surface card with a 4 px bar in the feedback colour. Success and info dismiss after 5 s
 * (pass `duration` 0 for errors, which stay until closed). The timer pauses on hover and focus.
 */
const props = withDefaults(
  defineProps<{
    id: string
    variant: 'success' | 'error' | 'warning' | 'info'
    title: string
    message?: string
    /** Milliseconds before auto-dismiss; 0 keeps the toast until it is closed. */
    duration?: number
    closeLabel: string
  }>(),
  { message: undefined, duration: 5000 },
)
const emit = defineEmits<{ close: [id: string] }>()

const styles = {
  success: { bar: 'bg-feedback-success-icon', icon: 'text-feedback-success-icon', component: CircleCheck },
  error: { bar: 'bg-feedback-error-icon', icon: 'text-feedback-error-icon', component: CircleAlert },
  warning: { bar: 'bg-feedback-warning-icon', icon: 'text-feedback-warning-icon', component: TriangleAlert },
  info: { bar: 'bg-feedback-info-icon', icon: 'text-feedback-info-icon', component: Info },
}

let timer: ReturnType<typeof setTimeout> | undefined
let remaining = props.duration
let startedAt = 0

function start() {
  if (remaining <= 0) return
  startedAt = Date.now()
  timer = setTimeout(() => emit('close', props.id), remaining)
}
function pause() {
  if (timer === undefined) return
  clearTimeout(timer)
  timer = undefined
  remaining -= Date.now() - startedAt
}
const hovered = ref(false)

onMounted(start)
onBeforeUnmount(() => clearTimeout(timer))
</script>

<template>
  <div
    :role="variant === 'error' ? 'alert' : 'status'"
    class="pointer-events-auto relative flex w-full max-w-sm items-start gap-3 overflow-hidden rounded-xl bg-bg-surface py-4 pl-5 pr-3 shadow-lg"
    @mouseenter="(hovered = true), pause()"
    @mouseleave="(hovered = false), start()"
    @focusin="pause"
    @focusout="start"
  >
    <span :class="['absolute inset-y-0 left-0 w-1', styles[variant].bar]" aria-hidden="true" />
    <component :is="styles[variant].component" :class="['mt-0.5 size-5 shrink-0', styles[variant].icon]" aria-hidden="true" />
    <div class="min-w-0 flex-1">
      <p class="truncate text-sm font-semibold text-text-primary">{{ title }}</p>
      <p v-if="message" class="mt-1 line-clamp-2 text-sm text-text-secondary">{{ message }}</p>
    </div>
    <button
      type="button"
      class="inline-flex size-8 shrink-0 items-center justify-center rounded-lg text-text-muted hover:bg-bg-subtle focus-visible:outline-2 focus-visible:outline-focus-ring"
      :aria-label="closeLabel"
      @click="emit('close', id)"
    >
      <X class="size-4" aria-hidden="true" />
    </button>
  </div>
</template>
