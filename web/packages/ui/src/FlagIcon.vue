<script setup lang="ts">
/** Flags for the language menu, ported from the reference app (24x24, rounded). Decorative: the language name is next to it. */
defineProps<{ code: string }>()
</script>

<template>
  <svg v-if="code === 'vi'" class="size-5 shrink-0" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    <rect y="3" width="24" height="18" rx="3.67347" fill="#DA251D" />
    <path d="M12 6L13.5 10.5H18L14.25 13.5L15.75 18L12 15L8.25 18L9.75 13.5L6 10.5H10.5L12 6Z" fill="#FFFF00" />
  </svg>
  <svg v-else class="size-5 shrink-0" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    <rect y="3" width="24" height="18" rx="3.42857" fill="#012169" />
    <path d="M0 0L24 24M24 0L0 24" stroke="#FFFFFF" stroke-width="3" />
    <path d="M0 0L24 24M24 0L0 24" stroke="#C8102E" stroke-width="2" />
    <path d="M12 0V24M0 12H24" stroke="#FFFFFF" stroke-width="5" />
    <path d="M12 0V24M0 12H24" stroke="#C8102E" stroke-width="3" />
  </svg>
</template>
