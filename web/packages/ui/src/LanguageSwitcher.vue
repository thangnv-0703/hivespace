<script setup lang="ts">
import { ChevronDown } from '@lucide/vue'
import { nextTick, onBeforeUnmount, onMounted, ref, useTemplateRef } from 'vue'
import FlagIcon from './FlagIcon.vue'

/**
 * Language dropdown ported from the reference app: flag + short code + chevron, and a menu with a flag and the
 * language name per row. Locale handling stays in the app (v-model); this component has no i18n, cookie or store.
 */
export interface LanguageOption {
  code: string
  name: string
}

const props = withDefaults(
  defineProps<{ modelValue: string; label: string; options?: LanguageOption[] }>(),
  {
    options: () => [
      { code: 'vi', name: 'Tiếng Việt' },
      { code: 'en', name: 'English' },
    ],
  },
)
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const root = useTemplateRef<HTMLElement>('root')
const trigger = useTemplateRef<HTMLButtonElement>('trigger')
const items = useTemplateRef<HTMLButtonElement[]>('items')
const open = ref(false)

async function show() {
  open.value = true
  await nextTick()
  const current = props.options.findIndex((option) => option.code === props.modelValue)
  items.value?.[Math.max(current, 0)]?.focus()
}

function close(returnFocus = false) {
  open.value = false
  if (returnFocus) trigger.value?.focus()
}

function choose(code: string) {
  emit('update:modelValue', code)
  close(true)
}

function onMenuKeydown(event: KeyboardEvent) {
  const list = items.value ?? []
  const index = list.findIndex((item) => item === document.activeElement)
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
</script>

<template>
  <div ref="root" class="relative inline-block">
    <button
      ref="trigger"
      type="button"
      class="inline-flex min-h-11 items-center gap-2 rounded-lg px-2 text-sm font-medium text-text-secondary hover:bg-bg-subtle focus-visible:outline-2 focus-visible:outline-focus-ring"
      aria-haspopup="menu"
      :aria-expanded="open"
      :aria-label="label"
      data-testid="language-switcher"
      @click="open ? close() : show()"
    >
      <FlagIcon :code="modelValue" />
      <span class="uppercase" aria-hidden="true">{{ modelValue }}</span>
      <ChevronDown class="size-4" aria-hidden="true" />
    </button>
    <div
      v-if="open"
      role="menu"
      :aria-label="label"
      class="absolute right-0 z-[var(--z-dropdown)] mt-2 w-48 rounded-lg border border-border-default bg-bg-surface py-1 shadow-lg"
      @keydown="onMenuKeydown"
    >
      <button
        v-for="option in options"
        :key="option.code"
        ref="items"
        type="button"
        role="menuitemradio"
        :aria-checked="option.code === modelValue"
        :class="[
          'flex w-full items-center gap-3 px-4 py-2 text-sm text-text-secondary hover:bg-bg-subtle focus-visible:bg-bg-subtle focus-visible:outline-none',
          option.code === modelValue ? 'font-medium text-text-primary' : '',
        ]"
        @click="choose(option.code)"
      >
        <FlagIcon :code="option.code" />
        {{ option.name }}
      </button>
    </div>
  </div>
</template>
