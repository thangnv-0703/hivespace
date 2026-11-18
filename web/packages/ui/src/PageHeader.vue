<script setup lang="ts">
import { ChevronRight } from '@lucide/vue'

/** Title row with breadcrumb on the right (design.md §4, SEL). The last crumb is the current page. */
export interface Crumb {
  label: string
  to?: string
}

defineProps<{ title: string; crumbs: Crumb[]; navLabel: string }>()
</script>

<template>
  <div class="mb-6 flex flex-wrap items-center justify-between gap-3">
    <h1 class="text-xl font-semibold text-text-primary">{{ title }}</h1>
    <nav :aria-label="navLabel">
      <ol class="flex items-center gap-2 text-sm text-text-muted">
        <li v-for="(crumb, index) in crumbs" :key="crumb.label" class="flex items-center gap-2">
          <a v-if="crumb.to && index < crumbs.length - 1" :href="crumb.to" class="hover:text-text-primary">{{ crumb.label }}</a>
          <span v-else class="text-text-primary" aria-current="page">{{ crumb.label }}</span>
          <ChevronRight v-if="index < crumbs.length - 1" class="size-4" aria-hidden="true" />
        </li>
      </ol>
    </nav>
  </div>
</template>
