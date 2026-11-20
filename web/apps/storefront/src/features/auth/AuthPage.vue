<script setup lang="ts">
import { setLocale, type Locale } from '@hivespace/core'
import { AuthLayout, LanguageSwitcher, ThemeToggle } from '@hivespace/ui'
import { useI18n } from 'vue-i18n'

// The three auth pages share this wiring of the presentational AuthLayout to this app's i18n (technical-design D-1).
defineProps<{ title: string; subtitle?: string }>()

const i18n = useI18n()

function changeLocale(value: string) {
  setLocale(i18n.locale, value as Locale)
}
</script>

<template>
  <AuthLayout
    :title="title"
    :subtitle="subtitle"
    :image-heading="$t('auth.brand.heading')"
    :image-body="$t('auth.brand.body')"
    :skip-label="$t('auth.skip')"
  >
    <template #corner>
      <LanguageSwitcher :model-value="i18n.locale.value" :label="$t('common.language')" @update:model-value="changeLocale" />
      <ThemeToggle :label="$t('common.toggleTheme')" />
    </template>
    <slot />
  </AuthLayout>
</template>
