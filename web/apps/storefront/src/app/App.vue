<script setup lang="ts">
import { setLocale, type Locale } from '@hivespace/core'
import { useSessionStore } from '@/features/auth'
import { profileSessionKey } from '@/features/profile'
import StorefrontLayout from '@/shared/layouts/StorefrontLayout.vue'
import { onMounted, provide } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'

const route = useRoute()
const session = useSessionStore()
const i18n = useI18n()

// The profile feature gets the session through this port instead of importing the auth feature (features do not import each other).
provide(profileSessionKey, {
  isSignedIn: () => session.status === 'authenticated',
  email: () => session.user?.email ?? '',
  refresh: () => session.refresh(),
  markSignedOut: () => session.markSignedOut(),
})

// One session probe per page load (GET /bff/user); a failure counts as signed out. A signed-in user's saved language
// wins over the browser's (technical-design D-7).
onMounted(async () => {
  await session.load()
  if (session.user && session.user.language !== i18n.locale.value) setLocale(i18n.locale, session.user.language as Locale)
})
</script>

<template>
  <RouterView v-if="route.meta.fullscreen" />
  <StorefrontLayout v-else>
    <RouterView />
  </StorefrontLayout>
</template>
