<script setup lang="ts">
import { ChevronLeft, ChevronRight } from '@lucide/vue'
import { computed } from 'vue'

const props = defineProps<{
  currentPage: number
  totalPages: number
  /** Accessible names come from the app's i18n (the ui package has none). */
  labels: { nav: string; previous: string; next: string }
  summary?: string
}>()
const emit = defineEmits<{ 'update:currentPage': [page: number] }>()

const pages = computed<(number | 'gap')[]>(() => {
  const { currentPage: c, totalPages: t } = props
  if (t <= 7) return Array.from({ length: t }, (_, i) => i + 1)
  if (c <= 3) return [1, 2, 3, 4, 'gap', t - 1, t]
  if (c >= t - 2) return [1, 2, 'gap', t - 3, t - 2, t - 1, t]
  return [1, 'gap', c - 1, c, c + 1, 'gap', t]
})

function go(page: number) {
  if (page >= 1 && page <= props.totalPages && page !== props.currentPage) emit('update:currentPage', page)
}

const base =
  'inline-flex size-9 items-center justify-center rounded-lg text-sm font-medium focus-visible:outline-2 focus-visible:outline-focus-ring'
const edge = `${base} border border-border-default text-text-secondary hover:bg-bg-subtle disabled:opacity-50`
</script>

<template>
  <div class="flex flex-wrap items-center justify-between gap-3">
    <p v-if="summary" class="text-sm text-text-muted">{{ summary }}</p>
    <nav :aria-label="labels.nav" class="flex items-center gap-2">
      <button type="button" :class="edge" :disabled="currentPage === 1" :aria-label="labels.previous" @click="go(currentPage - 1)">
        <ChevronLeft class="size-4" aria-hidden="true" />
      </button>
      <template v-for="(page, index) in pages" :key="index">
        <span v-if="page === 'gap'" class="inline-flex size-9 items-center justify-center text-text-muted" aria-hidden="true">...</span>
        <button
          v-else
          type="button"
          :aria-current="page === currentPage ? 'page' : undefined"
          :class="[base, page === currentPage ? 'bg-action-primary-bg text-action-primary-fg' : 'text-text-secondary hover:bg-bg-subtle']"
          @click="go(page)"
        >
          {{ page }}
        </button>
      </template>
      <button type="button" :class="edge" :disabled="currentPage === totalPages" :aria-label="labels.next" @click="go(currentPage + 1)">
        <ChevronRight class="size-4" aria-hidden="true" />
      </button>
    </nav>
  </div>
</template>
