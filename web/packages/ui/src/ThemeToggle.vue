<script setup lang="ts">
import { Moon, Sun } from '@lucide/vue'
import { ref } from 'vue'
import { setTheme } from './theme'

/** 44 px bordered square (the touch target), the same in the storefront and seller centre headers. */
defineProps<{ label: string }>()

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
    class="inline-flex size-11 items-center justify-center rounded-lg border border-border-default bg-bg-surface text-text-secondary hover:bg-bg-subtle focus-visible:outline-2 focus-visible:outline-focus-ring"
    @click="toggle"
  >
    <Sun v-if="dark" class="size-5" aria-hidden="true" />
    <Moon v-else class="size-5" aria-hidden="true" />
  </button>
</template>
