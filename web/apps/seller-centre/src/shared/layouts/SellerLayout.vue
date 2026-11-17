<script setup lang="ts">
// Seller centre shell (ux-foundations B2): left sidebar with four sections, top header.
// Section pages arrive with their slices (FEAT-009 onward); the links show the IA only.
import { setLocale, type Locale } from '@hivespace/core'
import { LanguageSwitcher, ThemeToggle } from '@hivespace/ui'
import { useI18n } from 'vue-i18n'

const i18n = useI18n()
const sections = ['dashboard', 'products', 'orders', 'settings'] as const

function changeLocale(value: string) {
  setLocale(i18n.locale, value as Locale)
}
</script>

<template>
  <div class="flex min-h-screen bg-bg-canvas text-text-primary">
    <aside
      class="hidden w-[var(--size-sidebar-sel)] shrink-0 border-r border-border-default bg-bg-surface lg:block"
      data-testid="sidebar"
    >
      <div class="px-6 py-5 text-xl font-bold text-action-primary-bg">HiveSpace</div>
      <nav :aria-label="$t('nav.label')">
        <ul class="flex flex-col gap-1 px-3">
          <li v-for="section in sections" :key="section">
            <span
              class="flex min-h-11 items-center rounded-lg px-3 text-text-secondary"
              :class="section === 'dashboard' ? 'bg-brand-50 font-medium text-action-primary-bg dark:bg-bg-subtle' : ''"
            >
              {{ $t(`nav.${section}`) }}
            </span>
          </li>
        </ul>
      </nav>
    </aside>
    <div class="flex min-w-0 flex-1 flex-col">
      <header
        class="flex h-[var(--size-header-sel)] items-center justify-between gap-4 border-b border-border-default bg-bg-surface px-4"
      >
        <span class="font-semibold">{{ $t('nav.title') }}</span>
        <div class="flex items-center gap-2">
          <a href="/" class="text-text-link hover:text-text-link-hover">{{ $t('nav.storefront') }}</a>
          <LanguageSwitcher
            :model-value="i18n.locale.value"
            :label="$t('common.language')"
            @update:model-value="changeLocale"
          />
          <ThemeToggle :label="$t('common.toggleTheme')" />
        </div>
      </header>
      <main class="p-4 lg:p-6">
        <slot />
      </main>
    </div>
  </div>
</template>
