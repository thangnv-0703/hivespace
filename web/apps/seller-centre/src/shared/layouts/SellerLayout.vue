<script setup lang="ts">
// Seller centre shell (ux-foundations B2, design.md §4) built on the shared AppShell.
// Section pages arrive with their slices (FEAT-009 onward); the links show the IA only.
import { setLocale, type Locale } from '@hivespace/core'
import { AppShell, LanguageSwitcher, ThemeToggle, type SidebarMenuGroup } from '@hivespace/ui'
import { ClipboardList, LayoutGrid, Package, Settings, UserRound } from '@lucide/vue'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRoute } from 'vue-router'

const i18n = useI18n()
const route = useRoute()
// The storefront is another host (ADR-0027): set by the AppHost in development, by .env.production for the image.
const storefrontOrigin = import.meta.env.VITE_STOREFRONT_ORIGIN ?? 'http://localhost:5080'

const menuGroups = computed<SidebarMenuGroup[]>(() => [
  {
    title: i18n.t('nav.menu'),
    items: [
      { name: i18n.t('nav.dashboard'), icon: LayoutGrid, path: '/' },
      {
        name: i18n.t('nav.products'),
        icon: Package,
        subItems: [
          { name: i18n.t('nav.productList'), path: '/products' },
          { name: i18n.t('nav.productNew'), path: '/products/new' },
        ],
      },
      { name: i18n.t('nav.orders'), icon: ClipboardList, path: '/orders' },
      { name: i18n.t('nav.settings'), icon: Settings, path: '/settings' },
    ],
  },
])

const labels = computed(() => ({
  nav: i18n.t('nav.label'),
  search: i18n.t('common.search'),
  searchPlaceholder: i18n.t('common.searchPlaceholder'),
  openMenu: i18n.t('nav.openMenu'),
  closeMenu: i18n.t('nav.closeMenu'),
  skipToContent: i18n.t('common.skipToContent'),
}))

function changeLocale(value: string) {
  setLocale(i18n.locale, value as Locale)
}
</script>

<template>
  <AppShell :menu-groups="menuGroups" :active-path="route.path" :link-as="RouterLink" :labels="labels">
    <template #actions>
      <LanguageSwitcher :model-value="i18n.locale.value" :label="$t('common.language')" @update:model-value="changeLocale" />
      <ThemeToggle :label="$t('common.toggleTheme')" />
      <a :href="`${storefrontOrigin}/`" class="hidden px-2 text-sm text-text-link hover:text-text-link-hover md:inline">{{ $t('nav.storefront') }}</a>
      <!-- STUB: account menu arrives with sign-in (FEAT-001). -->
      <span class="inline-flex min-h-11 items-center gap-2 rounded-lg px-2 text-sm text-text-secondary">
        <span class="inline-flex size-9 items-center justify-center rounded-full bg-bg-subtle">
          <UserRound class="size-5" aria-hidden="true" />
        </span>
        <span class="hidden md:inline">{{ $t('nav.account') }}</span>
      </span>
    </template>
    <slot />
  </AppShell>
</template>
