<script setup lang="ts">
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
    class="inline-flex min-h-11 min-w-11 items-center justify-center rounded-lg text-text-secondary hover:bg-bg-subtle focus-visible:outline-2 focus-visible:outline-focus-ring"
    @click="toggle"
  >
    <span aria-hidden="true">{{ dark ? '☾' : '☀' }}</span>
  </button>
</template>
