<script setup lang="ts">
// Storefront shell (ux-foundations B1): sticky header with logo, search slot and utility bar.
// Search and cart arrive with their slices (FEAT-018, FEAT-021); the account area is FEAT-001.
import { setLocale, type Locale } from '@hivespace/core'
import { BrandLogo, Input, LanguageSwitcher, ThemeToggle } from '@hivespace/ui'
import { AccountMenu, UnverifiedBanner } from '@/features/auth'
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
        <RouterLink to="/" class="rounded focus-visible:outline-2 focus-visible:outline-focus-ring">
          <BrandLogo />
        </RouterLink>
        <!-- STUB: search arrives with FEAT-018. -->
        <div class="hidden max-w-xl flex-1 md:block">
          <Input type="search" :placeholder="$t('common.searchPlaceholder')" :aria-label="$t('common.search')" />
        </div>
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
          <AccountMenu />
        </div>
      </div>
      <CategoryBar />
    </header>
    <UnverifiedBanner />
    <main class="mx-auto max-w-[var(--size-container)] px-4 py-6">
      <slot />
    </main>
  </div>
</template>
