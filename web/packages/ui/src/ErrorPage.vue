<script setup lang="ts">
import Button from './Button.vue'

/** Shared error page (SCR-STF-019, SCR-SEL-009): 404, 500 and maintenance. Text comes from the app's i18n. */
defineProps<{ code: '404' | '500' | 'maintenance'; title: string; message: string; actionLabel: string; actionHref: string }>()
</script>

<template>
  <section class="mx-auto flex max-w-xl flex-col items-center gap-4 px-4 py-16 text-center" role="alert">
    <p class="text-title-sm font-semibold text-action-primary-bg">{{ code === 'maintenance' ? '…' : code }}</p>
    <h1 class="text-xl font-semibold text-text-primary">{{ title }}</h1>
    <p class="text-md text-text-muted">{{ message }}</p>
    <Button :href="actionHref">{{ actionLabel }}</Button>
  </section>
</template>
