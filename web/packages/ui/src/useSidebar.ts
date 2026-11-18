import { computed, inject, onMounted, onUnmounted, provide, ref, type ComputedRef, type Ref } from 'vue'

export interface SidebarContext {
  /** Desktop: the wide sidebar is pinned open. Always false on small screens. */
  isExpanded: ComputedRef<boolean>
  isMobileOpen: Ref<boolean>
  /** Desktop, collapsed: hovering expands the sidebar over the content without moving it. */
  isHovered: Ref<boolean>
  openSubmenu: Ref<string | null>
  /** Small screens toggle the drawer; desktop toggles collapsed/expanded. */
  toggleSidebar: () => void
  closeMobile: () => void
  setHovered: (value: boolean) => void
  toggleSubmenu: (key: string) => void
}

const key = Symbol('hivespace-sidebar')
// Matches the `lg:` breakpoint the layout classes switch on.
const DESKTOP_QUERY = '(min-width: 1024px)'

export function useSidebarProvider(): SidebarContext {
  const pinned = ref(true)
  const isDesktop = ref(true)
  const isMobileOpen = ref(false)
  const isHovered = ref(false)
  const openSubmenu = ref<string | null>(null)
  let query: MediaQueryList | undefined

  const sync = () => {
    isDesktop.value = query?.matches ?? true
    if (isDesktop.value) isMobileOpen.value = false
  }
  const onKeydown = (event: KeyboardEvent) => {
    if (event.key === 'Escape') isMobileOpen.value = false
  }

  onMounted(() => {
    // matchMedia is missing in some test DOMs; the sidebar then stays in its desktop state.
    query = typeof window.matchMedia === 'function' ? window.matchMedia(DESKTOP_QUERY) : undefined
    sync()
    query?.addEventListener('change', sync)
    window.addEventListener('keydown', onKeydown)
  })
  onUnmounted(() => {
    query?.removeEventListener('change', sync)
    window.removeEventListener('keydown', onKeydown)
  })

  const context: SidebarContext = {
    isExpanded: computed(() => isDesktop.value && pinned.value),
    isMobileOpen,
    isHovered,
    openSubmenu,
    toggleSidebar: () => {
      if (isDesktop.value) pinned.value = !pinned.value
      else isMobileOpen.value = !isMobileOpen.value
    },
    closeMobile: () => {
      isMobileOpen.value = false
    },
    setHovered: (value) => {
      isHovered.value = value
    },
    toggleSubmenu: (submenu) => {
      openSubmenu.value = openSubmenu.value === submenu ? null : submenu
    },
  }
  provide(key, context)
  return context
}

export function useSidebar(): SidebarContext {
  const context = inject<SidebarContext | null>(key, null)
  if (!context) throw new Error('useSidebar must be used inside an AppShell (or a component that calls useSidebarProvider)')
  return context
}
