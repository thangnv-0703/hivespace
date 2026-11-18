<script setup lang="ts">
import { Menu, Search, X } from '@lucide/vue'
import { useSidebar } from './useSidebar'

/** Seller header (design.md §4): menu toggle, search, and an `actions` slot for theme, language and account. */
defineProps<{
  searchLabel: string
  searchPlaceholder: string
  openMenuLabel: string
  closeMenuLabel: string
}>()

const { isMobileOpen, toggleSidebar } = useSidebar()
</script>

<template>
  <header
    class="sticky top-0 z-30 flex h-[var(--size-header-sel)] items-center justify-between gap-4 border-b border-border-default bg-bg-surface px-4 lg:px-6"
  >
    <div class="flex min-w-0 flex-1 items-center gap-3">
      <button
        type="button"
        class="inline-flex size-11 items-center justify-center rounded-lg text-text-secondary hover:bg-bg-subtle focus-visible:outline-2 focus-visible:outline-focus-ring"
        :aria-label="isMobileOpen ? closeMenuLabel : openMenuLabel"
        :aria-expanded="isMobileOpen"
        @click="toggleSidebar"
      >
        <X v-if="isMobileOpen" class="size-5" aria-hidden="true" />
        <Menu v-else class="size-5" aria-hidden="true" />
      </button>
      <label class="relative hidden w-full max-w-md sm:block">
        <span class="sr-only">{{ searchLabel }}</span>
        <Search class="pointer-events-none absolute left-3 top-1/2 size-5 -translate-y-1/2 text-text-muted" aria-hidden="true" />
        <input
          type="search"
          :placeholder="searchPlaceholder"
          class="min-h-11 w-full rounded-lg border border-border-default bg-bg-canvas pl-10 pr-3 text-sm placeholder:text-text-muted focus-visible:outline-2 focus-visible:outline-focus-ring"
        />
      </label>
    </div>
    <div class="flex items-center gap-2"><slot name="actions" /></div>
  </header>
</template>
