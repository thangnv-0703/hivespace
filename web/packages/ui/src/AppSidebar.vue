<script setup lang="ts">
import { ChevronDown } from '@lucide/vue'
import { computed, type Component } from 'vue'
import BrandLogo from './BrandLogo.vue'
import type { SidebarMenuGroup } from './sidebar.types'
import { useSidebar } from './useSidebar'

/**
 * Seller sidebar (design.md §4): fixed left, 290 px wide or 90 px collapsed (icons with tooltips),
 * an off-canvas drawer below `lg`. Links render through `linkAs` (for example RouterLink) so this
 * package stays free of vue-router.
 */
const props = defineProps<{
  menuGroups: SidebarMenuGroup[]
  activePath: string
  navLabel: string
  homePath?: string
  linkAs?: string | Component
}>()

const { isExpanded, isHovered, isMobileOpen, openSubmenu, setHovered, toggleSubmenu, closeMobile } = useSidebar()

const showLabels = computed(() => isExpanded.value || isHovered.value || isMobileOpen.value)
const isActive = (path?: string) => path !== undefined && path === props.activePath
const keyOf = (group: number, item: number) => `${group}-${item}`

function isOpen(group: number, item: number) {
  const entry = props.menuGroups[group]?.items[item]
  return openSubmenu.value === keyOf(group, item) || entry?.subItems?.some((sub) => isActive(sub.path)) === true
}

const tag = computed(() => props.linkAs ?? 'a')
const linkAttrs = (path: string) => (props.linkAs ? { to: path } : { href: path })

const row =
  'flex min-h-10 w-full items-center gap-3 rounded-lg px-3 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-focus-ring'
const idle = 'text-text-secondary hover:bg-bg-subtle'
const current = 'bg-brand-50 text-brand-600 dark:bg-brand-950 dark:text-brand-300'
</script>

<template>
  <aside
    :class="[
      'fixed inset-y-0 left-0 z-40 flex flex-col overflow-hidden border-r border-border-default bg-bg-surface transition-[transform,width] duration-200',
      'w-[var(--size-sidebar-sel)]',
      isMobileOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0',
      showLabels ? '' : 'lg:w-[var(--size-sidebar-sel-collapsed)]',
    ]"
    data-testid="sidebar"
    @mouseenter="!isExpanded && setHovered(true)"
    @mouseleave="setHovered(false)"
  >
    <div class="flex h-[var(--size-header-sel)] shrink-0 items-center px-6" :class="showLabels ? '' : 'lg:justify-center lg:px-0'">
      <component :is="tag" v-bind="linkAttrs(homePath ?? '/')" class="rounded focus-visible:outline-2 focus-visible:outline-focus-ring">
        <BrandLogo :compact="!showLabels" />
      </component>
    </div>

    <nav :aria-label="navLabel" class="flex-1 overflow-y-auto px-3 pb-6">
      <div v-for="(group, groupIndex) in menuGroups" :key="group.title" class="mb-4">
        <p v-if="showLabels" class="px-3 pb-2 text-[11px] font-bold uppercase tracking-[0.08em] text-text-muted">{{ group.title }}</p>
        <hr v-else class="mx-3 mb-2 border-border-default" />
        <ul class="flex flex-col gap-1">
          <li v-for="(item, itemIndex) in group.items" :key="item.name">
            <template v-if="item.subItems?.length">
              <button
                type="button"
                :class="[row, !showLabels ? 'lg:justify-center' : '', isOpen(groupIndex, itemIndex) ? current : idle]"
                :aria-expanded="isOpen(groupIndex, itemIndex)"
                :title="showLabels ? undefined : item.name"
                @click="toggleSubmenu(keyOf(groupIndex, itemIndex))"
              >
                <component :is="item.icon" class="size-5 shrink-0" aria-hidden="true" />
                <span v-if="showLabels" class="flex-1 text-left">{{ item.name }}</span>
                <ChevronDown v-if="showLabels" class="size-4 transition-transform" :class="isOpen(groupIndex, itemIndex) ? 'rotate-180' : ''" aria-hidden="true" />
              </button>
              <ul v-show="isOpen(groupIndex, itemIndex) && showLabels" class="mt-1 flex flex-col gap-1">
                <li v-for="sub in item.subItems" :key="sub.path">
                  <component
                    :is="tag"
                    v-bind="linkAttrs(sub.path)"
                    :class="[row, 'pl-11', isActive(sub.path) ? current : idle]"
                    :aria-current="isActive(sub.path) ? 'page' : undefined"
                    @click="closeMobile"
                  >
                    {{ sub.name }}
                  </component>
                </li>
              </ul>
            </template>
            <component
              :is="tag"
              v-else-if="item.path"
              v-bind="linkAttrs(item.path)"
              :class="[row, !showLabels ? 'lg:justify-center' : '', isActive(item.path) ? current : idle]"
              :aria-current="isActive(item.path) ? 'page' : undefined"
              :title="showLabels ? undefined : item.name"
              @click="closeMobile"
            >
              <component :is="item.icon" class="size-5 shrink-0" aria-hidden="true" />
              <span v-if="showLabels">{{ item.name }}</span>
              <span v-else class="sr-only">{{ item.name }}</span>
            </component>
          </li>
        </ul>
      </div>
    </nav>
  </aside>
</template>
