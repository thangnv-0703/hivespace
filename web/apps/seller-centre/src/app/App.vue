<script setup lang="ts">
import { setLocale, type Locale } from '@hivespace/core'
import { useSessionStore } from '@/features/auth'
import { dashboardSessionKey } from '@/features/dashboard'
import { settingsSessionKey } from '@/features/settings'
import { shopSessionKey } from '@/features/shop'
import OnboardingLayout from '@/shared/layouts/OnboardingLayout.vue'
import SellerLayout from '@/shared/layouts/SellerLayout.vue'
import ToastHost from '@/shared/ToastHost.vue'
import { notify } from '@/shared/toasts'
import { onMounted, provide } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'

const route = useRoute()
const session = useSessionStore()
const i18n = useI18n()

// The feature pages get the session through these ports instead of importing the auth feature (features do not import each other).
provide(shopSessionKey, {
  email: () => session.user?.email ?? '',
  emailVerified: () => session.user?.emailVerified ?? false,
  refresh: () => session.refresh(),
  markSignedOut: () => session.markSignedOut(),
  notify,
})
provide(settingsSessionKey, {
  refresh: () => session.refresh(),
  markSignedOut: () => session.markSignedOut(),
  notify,
})
provide(dashboardSessionKey, {
  refresh: () => session.refresh(),
  markSignedOut: () => session.markSignedOut(),
})

// One session probe per page load (GET /bff/user). A signed-in user's saved language wins over the browser's.
onMounted(async () => {
  await session.load()
  if (session.user && session.user.language !== i18n.locale.value) setLocale(i18n.locale, session.user.language as Locale)
})
</script>

<template>
  <OnboardingLayout v-if="route.meta.onboarding">
    <RouterView />
  </OnboardingLayout>
  <SellerLayout v-else>
    <RouterView />
  </SellerLayout>
  <ToastHost />
</template>
