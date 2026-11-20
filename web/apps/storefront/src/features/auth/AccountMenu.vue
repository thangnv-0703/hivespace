<script setup lang="ts">
import { DropdownMenu } from '@hivespace/ui'
import { computed, useTemplateRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRoute } from 'vue-router'

import { useSessionStore } from './session'

// Header account area (ui-design "Extras"): reserved while the probe runs, links when signed out, a menu when signed in.
const { t } = useI18n()
const session = useSessionStore()
const route = useRoute()
const logout = useTemplateRef<HTMLFormElement>('logout')

const signInHref = computed(() => `/bff/login?returnUrl=${encodeURIComponent(route.fullPath)}`)
// First letters of the first and last word of the display name ("Linh Nguyen" -> "LN").
const initials = computed(() => {
  const words = (session.user?.displayName ?? '').trim().split(/\s+/).filter(Boolean)
  return ((words[0]?.[0] ?? '') + (words.length > 1 ? (words[words.length - 1]?.[0] ?? '') : '')).toUpperCase()
})
const items = computed(() => [
  { label: t('common.account.profile'), to: '/profile' },
  { label: t('auth.actions.signOut'), onClick: () => logout.value?.submit(), divided: true },
])
</script>

<template>
  <div class="flex min-h-9 items-center gap-3" data-testid="account-area">
    <span v-if="session.status === 'unknown'" class="size-8 animate-pulse rounded-full bg-bg-subtle" aria-hidden="true" />

    <template v-else-if="session.status === 'anonymous'">
      <a :href="signInHref" class="text-sm text-text-link hover:text-text-link-hover hover:underline">{{ $t('auth.actions.signIn') }}</a>
      <RouterLink to="/register" class="text-sm text-text-link hover:text-text-link-hover hover:underline">
        {{ $t('auth.actions.register') }}
      </RouterLink>
    </template>

    <template v-else>
      <span class="max-w-[20ch] truncate text-sm font-medium text-text-primary" data-testid="account-name">{{ session.user?.displayName }}</span>
      <DropdownMenu :items="items" :label="$t('auth.menu.account')" :link-as="RouterLink">
        <template #icon>
          <span class="flex size-7 items-center justify-center rounded-full bg-brand-100 text-xs font-semibold text-brand-700" aria-hidden="true">
            {{ initials }}
          </span>
        </template>
      </DropdownMenu>
      <!-- Sign out is a same-origin form POST so the redirect chain can clear the Identity cookie (technical-design D-4). -->
      <form ref="logout" method="post" action="/bff/logout" class="hidden" />
    </template>
  </div>
</template>
