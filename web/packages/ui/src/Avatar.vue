<script setup lang="ts">
import { UserRound } from '@lucide/vue'

/** Avatar with an optional status dot. Falls back to a user icon when there is no image. */
withDefaults(
  defineProps<{
    src?: string
    alt?: string
    size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl' | '2xl'
    status?: 'online' | 'offline' | 'busy' | 'none'
  }>(),
  { src: undefined, alt: '', size: 'md', status: 'none' },
)

const sizes = { xs: 'size-6', sm: 'size-8', md: 'size-10', lg: 'size-12', xl: 'size-14', '2xl': 'size-16' }
const icons = { xs: 'size-3.5', sm: 'size-5', md: 'size-6', lg: 'size-7', xl: 'size-8', '2xl': 'size-10' }
const dots = { xs: 'size-1.5', sm: 'size-2', md: 'size-2.5', lg: 'size-3', xl: 'size-3.5', '2xl': 'size-4' }
const statusColors = { online: 'bg-feedback-success-icon', offline: 'bg-text-disabled', busy: 'bg-feedback-warning-icon' }
</script>

<template>
  <span :class="['relative inline-flex shrink-0', sizes[size ?? 'md']]">
    <img v-if="src" :src="src" :alt="alt" class="size-full rounded-full object-cover" />
    <span v-else class="flex size-full items-center justify-center rounded-full bg-bg-subtle text-text-muted">
      <UserRound :class="icons[size ?? 'md']" aria-hidden="true" />
    </span>
    <span
      v-if="status && status !== 'none'"
      :class="['absolute bottom-0 right-0 rounded-full border-2 border-bg-surface', dots[size ?? 'md'], statusColors[status]]"
      aria-hidden="true"
    />
  </span>
</template>
