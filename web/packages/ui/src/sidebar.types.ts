import type { Component } from 'vue'

export interface SidebarSubItem {
  name: string
  path: string
}

export interface SidebarMenuItem {
  name: string
  icon: Component
  /** A leaf link. Omit when the item has `subItems`. */
  path?: string
  subItems?: SidebarSubItem[]
}

export interface SidebarMenuGroup {
  title: string
  items: SidebarMenuItem[]
}
