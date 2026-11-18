<script setup lang="ts">
import { Moon, Sun } from '@lucide/vue'
import { ref } from 'vue'
import { setTheme } from './theme'

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
    class="inline-flex size-11 items-center justify-center rounded-full border border-border-default text-text-secondary hover:bg-bg-subtle focus-visible:outline-2 focus-visible:outline-focus-ring"
    @click="toggle"
  >
    <Sun v-if="dark" class="size-5" aria-hidden="true" />
    <Moon v-else class="size-5" aria-hidden="true" />
  </button>
</template>
