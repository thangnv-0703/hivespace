<script setup lang="ts">
import { CircleAlert, CircleCheck, Clock } from '@lucide/vue'
import { Alert, Button, Spinner } from '@hivespace/ui'
import { computed, nextTick, onMounted, ref, type Component } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'

import { verify } from './api'
import AuthPage from './AuthPage.vue'
import { focusTitle } from './navigation'
import ResendControl from './ResendControl.vue'
import { useSessionStore } from './session'

// SCR-STF-014. The link in the email lands here with ?token=; the page verifies once on load.
type State = 'loading' | 'verified' | 'expired' | 'invalid' | 'already' | 'missing' | 'network'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const session = useSessionStore()

const rawToken = Array.isArray(route.query.token) ? route.query.token[0] : route.query.token
const token = typeof rawToken === 'string' ? rawToken : ''
const state = ref<State>(token ? 'loading' : 'missing')
const errorCode = ref('')
// The one guard against a double call (dev strict mode, re-render): a started token is never verified twice.
let started = false

let rememberedEmail = ''
try {
  rememberedEmail = sessionStorage.getItem('hs-verify-email') ?? ''
} catch {
  rememberedEmail = ''
}

const icons: Partial<Record<State, { icon: Component; tone: string }>> = {
  verified: { icon: CircleCheck, tone: 'text-feedback-success-icon' },
  already: { icon: CircleCheck, tone: 'text-feedback-info-icon' },
  expired: { icon: Clock, tone: 'text-feedback-warning-icon' },
  invalid: { icon: CircleAlert, tone: 'text-feedback-error-icon' },
  missing: { icon: CircleAlert, tone: 'text-feedback-error-icon' },
}
const needsNewLink = computed(() => ['expired', 'invalid', 'missing'].includes(state.value))
const signedIn = computed(() => session.status === 'authenticated')
// Mockup subtitles: verified and expired show a line under the title; the other states keep the body inside the card.
const subtitle = computed(() =>
  state.value === 'verified' ? t('auth.verify.verifiedSubtitle') : state.value === 'expired' ? t('auth.verify.expiredBody') : undefined,
)
const titleKey = computed(() => (state.value === 'loading' ? 'loadingTitle' : state.value === 'network' ? 'networkTitle' : `${state.value}Title`))

async function run() {
  if (started || !token) return
  started = true
  state.value = 'loading'
  const result = await verify(token)
  if (result.ok) {
    state.value = 'verified'
  } else if (result.failure.code === 'identity.verification_token_expired') {
    state.value = 'expired'
  } else if (result.failure.code === 'identity.verification_token_invalid') {
    state.value = 'invalid'
  } else if (result.failure.code === 'identity.email_already_verified') {
    state.value = 'already'
  } else {
    state.value = 'network'
    errorCode.value = result.failure.code
    started = false
  }
  await nextTick()
  focusTitle()
}

onMounted(() => {
  if (session.status === 'unknown') void session.load()
  void run()
})
</script>

<template>
  <AuthPage :title="$t(`auth.verify.${titleKey}`)" :subtitle="subtitle">
    <div class="space-y-4" data-testid="verify-result" :data-state="state">
      <div v-if="state === 'loading'" class="flex items-center gap-3" role="status">
        <Spinner size="lg" />
        <p class="text-sm text-text-muted">{{ $t('auth.verify.loading') }}</p>
      </div>

      <template v-else-if="state === 'network'">
        <Alert variant="error" :title="$t('auth.verify.networkTitle')" />
        <p v-if="errorCode" class="text-xs text-text-muted">{{ errorCode }}</p>
        <Button size="lg" class="w-full" @click="run">{{ $t('auth.actions.retry') }}</Button>
      </template>

      <div
        v-else-if="state === 'verified'"
        class="flex flex-col items-center gap-3 rounded-xl border border-border-default bg-bg-surface px-6 py-8 text-center shadow-sm"
      >
        <div class="flex size-16 items-center justify-center rounded-full bg-feedback-success-bg" :class="icons.verified?.tone">
          <component :is="icons.verified?.icon" class="size-8" aria-hidden="true" />
        </div>
        <h2 class="text-lg font-semibold text-text-primary">{{ $t('auth.verify.verifiedHeading') }}</h2>
        <p class="text-sm text-text-muted">{{ $t('auth.verify.verifiedBody') }}</p>
        <Button size="lg" class="mt-2 w-full" @click="router.push(signedIn ? '/' : '/signin')">
          {{ signedIn ? $t('auth.actions.continueShopping') : $t('auth.actions.signIn') }}
        </Button>
      </div>

      <template v-else>
        <Alert
          v-if="state === 'expired'"
          variant="warning"
          :title="$t('auth.verify.expiredAlertTitle')"
          :message="$t('auth.verify.expiredAlertBody')"
        />
        <component v-else :is="icons[state]?.icon" :class="['size-12', icons[state]?.tone]" aria-hidden="true" />
        <p v-if="state === 'invalid' || state === 'missing'" class="text-sm text-text-muted">{{ $t('auth.verify.invalidBody') }}</p>

        <ResendControl v-if="needsNewLink" new-link :initial-email="rememberedEmail" />

        <Button v-if="state === 'already'" size="lg" class="w-full" @click="router.push('/signin')">
          {{ $t('auth.actions.signIn') }}
        </Button>
        <Button v-else variant="secondary" size="lg" class="w-full" @click="router.push('/signin')">{{ $t('auth.actions.signIn') }}</Button>
      </template>
    </div>
  </AuthPage>
</template>
