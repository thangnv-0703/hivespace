<script setup lang="ts">
// Storefront shell (ux-foundations B1): sticky header with logo, search slot and utility bar.
// Search, cart and account arrive with their slices (FEAT-018, FEAT-021, FEAT-001).
import { setLocale, type Locale } from '@hivespace/core'
import { LanguageSwitcher, ThemeToggle } from '@hivespace/ui'
import { CategoryBar } from '@/features/catalog'
import { useI18n } from 'vue-i18n'

const i18n = useI18n()

function changeLocale(value: string) {
  setLocale(i18n.locale, value as Locale)
}
</script>

<template>
  <div class="min-h-screen bg-bg-canvas text-text-primary">
    <header class="sticky top-0 z-[var(--z-sticky)] border-b border-border-default bg-bg-surface">
      <div class="mx-auto flex max-w-[var(--size-container)] items-center justify-between gap-4 px-4 py-3">
        <RouterLink to="/" class="text-xl font-bold text-action-primary-bg" data-testid="brand">
          HiveSpace
        </RouterLink>
        <div class="flex items-center gap-2">
          <a href="/seller/" class="text-sm text-text-link hover:text-text-link-hover">
            {{ $t('common.sellerCentre') }}
          </a>
          <LanguageSwitcher
            :model-value="i18n.locale.value"
            :label="$t('common.language')"
            @update:model-value="changeLocale"
          />
          <ThemeToggle :label="$t('common.toggleTheme')" />
        </div>
      </div>
      <CategoryBar />
    </header>
    <main class="mx-auto max-w-[var(--size-container)] px-4 py-6">
      <slot />
    </main>
  </div>
</template>
