<script setup lang="ts">
import { EllipsisVertical } from '@lucide/vue'
import { nextTick, onBeforeUnmount, onMounted, ref, useTemplateRef, type Component } from 'vue'

/** Action menu (the reference's DropdownMenu): a trigger button and a list of actions or links. Links render through `linkAs`. */
export interface MenuItem {
  label: string
  to?: string
  onClick?: () => void
}

const props = defineProps<{
  items: MenuItem[]
  /** Accessible name of the trigger (it is an icon button by default). */
  label: string
  linkAs?: string | Component
}>()

const root = useTemplateRef<HTMLElement>('root')
const trigger = useTemplateRef<HTMLButtonElement>('trigger')
const rowRefs = useTemplateRef<unknown[]>('rows')
// Rows may be components (for example RouterLink): use their root element.
const elements = () => (rowRefs.value ?? []).map((row) => ((row as { $el?: HTMLElement }).$el ?? row) as HTMLElement)
const open = ref(false)

async function show() {
  open.value = true
  await nextTick()
  elements()[0]?.focus()
}
function close(returnFocus = false) {
  open.value = false
  if (returnFocus) trigger.value?.focus()
}
function pick(item: MenuItem) {
  item.onClick?.()
  close(true)
}
function onKeydown(event: KeyboardEvent) {
  const list = elements()
  const index = list.findIndex((row) => row === document.activeElement)
  if (event.key === 'ArrowDown') list[(index + 1) % list.length]?.focus()
  else if (event.key === 'ArrowUp') list[(index - 1 + list.length) % list.length]?.focus()
  else if (event.key === 'Home') list[0]?.focus()
  else if (event.key === 'End') list[list.length - 1]?.focus()
  else if (event.key === 'Escape') close(true)
  else if (event.key === 'Tab') {
    close()
    return
  } else return
  event.preventDefault()
}
const onOutside = (event: Event) => {
  if (open.value && root.value && !root.value.contains(event.target as Node)) close()
}
onMounted(() => document.addEventListener('click', onOutside))
onBeforeUnmount(() => document.removeEventListener('click', onOutside))

const row =
  'flex w-full items-center rounded-lg px-3 py-2 text-left text-sm font-medium text-text-secondary hover:bg-bg-subtle focus-visible:bg-bg-subtle focus-visible:outline-none'
const tag = (item: MenuItem) => (item.to ? (props.linkAs ?? 'a') : 'button')
const attrs = (item: MenuItem) => (item.to ? (props.linkAs ? { to: item.to } : { href: item.to }) : { type: 'button' })
</script>

<template>
  <div ref="root" class="relative inline-block">
    <button
      ref="trigger"
      type="button"
      class="inline-flex size-9 items-center justify-center rounded-lg text-text-muted hover:bg-bg-subtle focus-visible:outline-2 focus-visible:outline-focus-ring"
      aria-haspopup="menu"
      :aria-expanded="open"
      :aria-label="label"
      @click="open ? close() : show()"
    >
      <slot name="icon" :open="open"><EllipsisVertical class="size-5" aria-hidden="true" /></slot>
    </button>
    <div
      v-if="open"
      role="menu"
      :aria-label="label"
      class="absolute right-0 z-[var(--z-dropdown)] mt-2 w-44 space-y-1 rounded-xl border border-border-default bg-bg-surface p-2 shadow-lg"
      @keydown="onKeydown"
    >
      <component :is="tag(item)" v-for="item in items" :key="item.label" ref="rows" v-bind="attrs(item)" role="menuitem" :class="row" @click="pick(item)">
        {{ item.label }}
      </component>
    </div>
  </div>
</template>
