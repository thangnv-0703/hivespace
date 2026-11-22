<script setup lang="ts">
// Storefront shell (design.md §3, ux-foundations B1): sticky header = one 80 px row (logo, search, seller centre from lg,
// language, theme, account, divider, cart) with the same 44 px controls as the seller centre. Below md it collapses to a
// 56 px row; the header actions move into a panel.
// Search and cart arrive with their slices (FEAT-018, FEAT-021); the account area is FEAT-001.
import { BrandLogo, Input, LanguageSwitcher, ThemeToggle } from '@hivespace/ui'
import { Menu, Search, ShoppingCart, X } from '@lucide/vue'
import { ref } from 'vue'
import { AccountMenu, UnverifiedBanner } from '@/features/auth'
import { CategoryBar } from '@/features/catalog'
import { ProfileToasts, useLanguagePreference } from '@/features/profile'
import { useI18n } from 'vue-i18n'

const i18n = useI18n()
const { change: changeLocale } = useLanguagePreference()
const menuOpen = ref(false)
// The seller centre is another host (ADR-0027): set by the AppHost in development, by .env.production for the image.
const sellerOrigin = import.meta.env.VITE_SELLER_ORIGIN ?? 'http://seller.localhost:5080'
const searchOpen = ref(false)

const iconButton =
  'inline-flex size-11 items-center justify-center rounded-lg text-text-secondary hover:bg-bg-subtle focus-visible:outline-2 focus-visible:outline-focus-ring'
</script>

<template>
  <div class="min-h-screen bg-bg-canvas text-text-primary">
    <header class="sticky top-0 z-[var(--z-sticky)] border-b border-border-default bg-bg-surface">
      <!-- Main row: 56 px below md, 80 px from md. -->
      <div class="mx-auto flex h-14 max-w-[var(--size-container)] items-center gap-3 px-4 md:h-20 md:gap-6 md:px-6">
        <RouterLink to="/" class="shrink-0 rounded focus-visible:outline-2 focus-visible:outline-focus-ring">
          <BrandLogo />
        </RouterLink>
        <!-- STUB: search arrives with FEAT-018. -->
        <div class="hidden flex-1 md:block">
          <Input type="search" :placeholder="$t('common.searchPlaceholder')" :aria-label="$t('common.search')" />
        </div>
        <div class="ml-auto flex items-center gap-1 md:ml-0">
          <!-- Header actions (md and up); below md they live in the menu panel. -->
          <div class="hidden items-center gap-2 md:flex">
            <a :href="`${sellerOrigin}/`" class="hidden px-2 text-sm text-text-secondary hover:text-text-primary lg:inline">{{ $t('common.sellerCentre') }}</a>
            <LanguageSwitcher :model-value="i18n.locale.value" :label="$t('common.language')" @update:model-value="changeLocale" />
            <ThemeToggle :label="$t('common.toggleTheme')" />
            <AccountMenu />
          </div>
          <span class="mx-2 hidden h-6 w-px bg-border-default md:block" aria-hidden="true" />
          <button
            type="button"
            :class="[iconButton, 'md:hidden']"
            :aria-label="$t('common.search')"
            :aria-expanded="searchOpen"
            @click="searchOpen = !searchOpen"
          >
            <Search class="size-6" aria-hidden="true" />
          </button>
          <!-- STUB: the cart and its count arrive with FEAT-021. -->
          <span :class="[iconButton, 'cursor-default']" role="img" :aria-label="$t('common.cart')">
            <ShoppingCart class="size-6" aria-hidden="true" />
          </span>
          <button
            type="button"
            :class="[iconButton, 'md:hidden']"
            :aria-label="menuOpen ? $t('common.closeMenu') : $t('common.menu')"
            :aria-expanded="menuOpen"
            aria-controls="storefront-menu"
            @click="menuOpen = !menuOpen"
          >
            <X v-if="menuOpen" class="size-6" aria-hidden="true" />
            <Menu v-else class="size-6" aria-hidden="true" />
          </button>
        </div>
      </div>

      <div v-if="searchOpen" class="px-4 pb-3 md:hidden">
        <Input type="search" :placeholder="$t('common.searchPlaceholder')" :aria-label="$t('common.search')" />
      </div>

      <!-- Menu panel (below md): the top-bar items. -->
      <div v-if="menuOpen" id="storefront-menu" class="space-y-3 border-t border-border-default px-4 py-3 md:hidden">
        <a :href="`${sellerOrigin}/`" class="block py-2 text-sm text-text-secondary">{{ $t('common.sellerCentre') }}</a>
        <div class="flex items-center gap-2">
          <LanguageSwitcher :model-value="i18n.locale.value" :label="$t('common.language')" @update:model-value="changeLocale" />
          <ThemeToggle :label="$t('common.toggleTheme')" />
        </div>
        <AccountMenu />
      </div>
      <CategoryBar />
    </header>
    <ProfileToasts />
    <UnverifiedBanner />
    <main class="mx-auto max-w-[var(--size-container)] px-4 py-6">
      <slot />
    </main>
  </div>
</template>
