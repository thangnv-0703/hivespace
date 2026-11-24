<script setup lang="ts">
import { DropdownMenu } from '@hivespace/ui'
import { computed, useTemplateRef } from 'vue'
import { useI18n } from 'vue-i18n'

import { useSessionStore } from './session'

// Seller-centre account menu (ui-design "Shell extras"): reserved while the probe runs, a menu when signed in.
const { t } = useI18n()
const session = useSessionStore()
const logout = useTemplateRef<HTMLFormElement>('logout')
// The storefront is another host (ADR-0027): set by the AppHost in development, by .env.production for the image.
const storefrontOrigin = import.meta.env.VITE_STOREFRONT_ORIGIN ?? 'http://localhost:5080'

// First letters of the first and last word of the display name ("Linh Nguyen" -> "LN").
const initials = computed(() => {
  const words = (session.user?.displayName ?? '').trim().split(/\s+/).filter(Boolean)
  return ((words[0]?.[0] ?? '') + (words.length > 1 ? (words[words.length - 1]?.[0] ?? '') : '')).toUpperCase()
})
const items = computed(() => [
  { label: t('auth.menu.storefront'), onClick: () => globalThis.location.assign(`${storefrontOrigin}/`) },
  { label: t('auth.menu.signOut'), onClick: () => logout.value?.submit(), divided: true },
])
</script>

<template>
  <div class="flex min-h-11 items-center gap-3" data-testid="account-area">
    <span v-if="session.status === 'unknown'" class="size-8 animate-pulse rounded-full bg-bg-subtle" aria-hidden="true" />

    <template v-else-if="session.status === 'authenticated'">
      <span class="hidden max-w-[20ch] truncate text-sm font-medium text-text-primary md:inline" data-testid="account-name">
        {{ session.user?.displayName }}
      </span>
      <DropdownMenu :items="items" :label="$t('auth.menu.account')">
        <template #icon>
          <span class="flex size-8 items-center justify-center rounded-full bg-brand-100 text-xs font-semibold text-brand-700" aria-hidden="true">
            {{ initials }}
          </span>
        </template>
      </DropdownMenu>
      <!-- Sign out is a same-origin form POST so the redirect chain can clear the Identity cookie (ADR-0027). -->
      <form ref="logout" method="post" action="/bff/logout" class="hidden" />
    </template>
  </div>
</template>
