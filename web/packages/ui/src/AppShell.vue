<script setup lang="ts">
import type { Component } from 'vue'
import AppHeader from './AppHeader.vue'
import AppSidebar from './AppSidebar.vue'
import type { SidebarMenuGroup } from './sidebar.types'
import { useSidebarProvider } from './useSidebar'

/** Seller centre shell: sidebar + header + content. Labels and links come from the app (this package has no i18n or router). */
defineProps<{
  menuGroups: SidebarMenuGroup[]
  activePath: string
  linkAs?: string | Component
  labels: {
    nav: string
    search: string
    searchPlaceholder: string
    openMenu: string
    closeMenu: string
    skipToContent: string
  }
}>()

const { isExpanded, isMobileOpen, closeMobile } = useSidebarProvider()
</script>

<template>
  <div class="min-h-screen bg-bg-canvas text-text-primary">
    <a
      href="#main-content"
      class="sr-only z-50 rounded-lg bg-bg-surface px-4 py-2 focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus-visible:outline-2 focus-visible:outline-focus-ring"
    >
      {{ labels.skipToContent }}
    </a>
    <div v-if="isMobileOpen" class="fixed inset-0 z-30 bg-bg-overlay lg:hidden" aria-hidden="true" @click="closeMobile" />
    <AppSidebar :menu-groups="menuGroups" :active-path="activePath" :link-as="linkAs" :nav-label="labels.nav" />
    <div
      :class="[
        'flex min-h-screen min-w-0 flex-col transition-[margin] duration-200',
        isExpanded ? 'lg:ml-[var(--size-sidebar-sel)]' : 'lg:ml-[var(--size-sidebar-sel-collapsed)]',
      ]"
    >
      <AppHeader
        :search-label="labels.search"
        :search-placeholder="labels.searchPlaceholder"
        :open-menu-label="labels.openMenu"
        :close-menu-label="labels.closeMenu"
      >
        <template #actions><slot name="actions" /></template>
      </AppHeader>
      <main id="main-content" class="min-w-0 p-4 md:p-6"><slot /></main>
    </div>
  </div>
</template>
