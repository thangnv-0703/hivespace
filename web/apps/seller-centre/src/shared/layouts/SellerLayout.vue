<script setup lang="ts">
// Seller centre shell (ux-foundations B2, design.md §4) built on the shared AppShell.
// Section pages arrive with their slices (FEAT-009 onward); the links show the IA only.
import { AccountMenu, useLanguagePreference } from '@/features/auth'
import { AppShell, LanguageSwitcher, ThemeToggle, type SidebarMenuGroup } from '@hivespace/ui'
import { ClipboardList, LayoutGrid, Package, Settings } from '@lucide/vue'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRoute } from 'vue-router'

import { notify } from '../toasts'

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
      {
        name: i18n.t('nav.settings'),
        icon: Settings,
        subItems: [
          { name: i18n.t('nav.shopProfile'), path: '/settings/profile' },
          { name: i18n.t('nav.pickupPayout'), path: '/settings/pickup-payout' },
        ],
      },
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

const language = useLanguagePreference((title) => notify({ variant: 'error', title }))
</script>

<template>
  <AppShell :menu-groups="menuGroups" :active-path="route.path" :link-as="RouterLink" :labels="labels">
    <template #actions>
      <LanguageSwitcher :model-value="language.locale.value" :label="$t('common.language')" @update:model-value="language.change" />
      <ThemeToggle :label="$t('common.toggleTheme')" />
      <a :href="`${storefrontOrigin}/`" class="hidden px-2 text-sm text-text-link hover:text-text-link-hover md:inline">{{ $t('nav.storefront') }}</a>
      <AccountMenu />
    </template>
    <slot />
  </AppShell>
</template>
