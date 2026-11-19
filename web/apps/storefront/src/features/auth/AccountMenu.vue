<script setup lang="ts">
import { Avatar, DropdownMenu } from '@hivespace/ui'
import { computed, useTemplateRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'

import { useSessionStore } from './session'

// Header account area (ui-design "Extras"): reserved while the probe runs, links when signed out, a menu when signed in.
const { t } = useI18n()
const session = useSessionStore()
const route = useRoute()
const logout = useTemplateRef<HTMLFormElement>('logout')

const signInHref = computed(() => `/bff/login?returnUrl=${encodeURIComponent(route.fullPath)}`)
const items = computed(() => [{ label: t('auth.actions.signOut'), onClick: () => logout.value?.submit() }])
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
      <span class="hidden max-w-[20ch] truncate text-sm text-text-secondary md:inline" data-testid="account-name">{{ session.user?.displayName }}</span>
      <DropdownMenu :items="items" :label="$t('auth.menu.account')">
        <template #icon><Avatar size="sm" :alt="session.user?.displayName ?? ''" /></template>
      </DropdownMenu>
      <!-- Sign out is a same-origin form POST so the redirect chain can clear the Identity cookie (technical-design D-4). -->
      <form ref="logout" method="post" action="/bff/logout" class="hidden" />
    </template>
  </div>
</template>
