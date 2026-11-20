<script setup lang="ts">
import { Moon, Sun } from '@lucide/vue'
import { ref } from 'vue'
import { setTheme } from './theme'

/** `compact` (32 px) is for dense bars such as the storefront top bar; the default is the 44 px touch target. */
defineProps<{ label: string; compact?: boolean }>()

const dark = ref(document.documentElement.classList.contains('dark'))

function toggle() {
  dark.value = !dark.value
  setTheme(dark.value)
}
</script>

<template>
  <button
    type="button"
    :aria-pressed="dark"
    :aria-label="label"
    :class="compact ? 'size-8' : 'size-11'"
    class="inline-flex items-center justify-center rounded-lg border border-border-default bg-bg-surface text-text-secondary hover:bg-bg-subtle focus-visible:outline-2 focus-visible:outline-focus-ring"
    @click="toggle"
  >
    <Sun v-if="dark" class="size-5" aria-hidden="true" />
    <Moon v-else class="size-5" aria-hidden="true" />
  </button>
</template>
