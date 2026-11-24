<script setup lang="ts">
import { useCooldown } from '@hivespace/core'
import { Alert, Button, Input, Spinner } from '@hivespace/ui'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'

import { requestCode } from './api'
import AuthPage from './AuthPage.vue'
import { loadChallenge, saveChallenge, secondsUntil } from './codeChallenge'
import { goTo } from './navigation'
import { EMAIL_PATTERN, fieldOf, returnUrlOf } from './validation'

// SCR-STF-021 (FEAT-006). The email typed on the sign-in page arrives in router state, never in the URL (ui-design D-A).
const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const email = ref(typeof router.options.history.state?.email === 'string' ? router.options.history.state.email : '')
const errors = reactive({ email: '' })
const formAlert = ref<{ variant: 'error' | 'warning'; title: string; message: string; code?: string; useExisting?: boolean } | null>(null)
const busy = ref(false)
const emailRef = ref<{ focus: () => void } | null>(null)

const returnUrl = computed(() => returnUrlOf(route.query.returnUrl))
// Without a returnUrl a code sign-in would create only the Identity cookie: start the BFF login first, as SCR-STF-012 does.
const starting = !returnUrl.value
const withReturnUrl = (path: string) => ({ path, query: returnUrl.value ? { returnUrl: returnUrl.value } : {} })

const cooldown = useCooldown(60)
const submitLabel = computed(() =>
  cooldown.remaining.value > 0 ? t('auth.code.request.submitCooldown', { seconds: cooldown.remaining.value }) : t('auth.code.request.submit'),
)
const announcement = ref('')
watch(cooldown.remaining, (seconds) => {
  if (seconds > 0 && (seconds % 10 === 0 || seconds === 1)) announcement.value = t('auth.code.request.submitCooldown', { seconds })
})

onMounted(() => {
  if (starting) goTo('/bff/login')
  else emailRef.value?.focus()
})

function checkEmail(): boolean {
  const value = email.value.trim()
  errors.email = !value ? t('auth.errors.emailRequired') : EMAIL_PATTERN.test(value) ? '' : t('auth.errors.emailInvalid')
  return !errors.email
}

/** The code already sent to this address is still usable (ui-design "too soon" action). */
function hasUsableChallenge(address: string): boolean {
  const stored = loadChallenge()
  return stored !== null && stored.email.toLowerCase() === address.toLowerCase() && secondsUntil(stored.expiresAt) > 0
}

async function submit() {
  formAlert.value = null
  if (!checkEmail()) return emailRef.value?.focus()

  const address = email.value.trim()
  busy.value = true
  const result = await requestCode(address)
  if (result.ok) {
    saveChallenge({ ...result.data, email: address, returnUrl: returnUrl.value })
    // Stays in the sending state until the route changes.
    await router.push(withReturnUrl('/signin/code/enter'))
    return
  }
  busy.value = false
  const { failure } = result
  if (failure.status === 400) {
    for (const detail of failure.details) {
      if (fieldOf(detail) === 'email') errors.email = detail.message ?? t('auth.errors.emailInvalid')
    }
    if (!errors.email) errors.email = t('auth.errors.emailInvalid')
    return emailRef.value?.focus()
  }
  if (failure.status === 429) {
    const tooSoon = failure.code === 'identity.sign_in_code_too_soon'
    formAlert.value = {
      variant: 'warning',
      title: tooSoon ? t('auth.code.request.tooSoonTitle') : t('auth.errors.common.rate_limited.title'),
      message: failure.message ?? t('auth.errors.common.rate_limited.message'),
      useExisting: hasUsableChallenge(address),
    }
    cooldown.start(failure.retryAfter ?? 60)
    return
  }
  formAlert.value = { variant: 'error', title: t('auth.errors.network'), message: '', code: failure.code }
}
</script>

<template>
  <AuthPage :title="$t('auth.code.request.title')" :subtitle="$t('auth.code.request.subtitle')">
    <div v-if="starting" role="status" data-testid="code-request-starting"><Spinner /></div>
    <template v-else>
      <form class="space-y-4" novalidate :aria-busy="busy" @submit.prevent="submit">
        <Alert v-if="formAlert" :variant="formAlert.variant" :title="formAlert.title" :message="formAlert.message || undefined" data-testid="form-alert">
          <template v-if="formAlert.useExisting" #action>
            <RouterLink :to="withReturnUrl('/signin/code/enter')" class="text-text-link underline underline-offset-2 hover:text-text-link-hover">
              {{ $t('auth.code.request.useExisting') }}
            </RouterLink>
          </template>
        </Alert>
        <p v-if="formAlert?.code" class="text-xs text-text-muted">{{ formAlert.code }}</p>
        <Input
          ref="emailRef"
          v-model="email"
          type="email"
          autocomplete="email"
          inputmode="email"
          required
          :disabled="busy"
          :label="$t('auth.fields.email')"
          :error="errors.email"
          @blur="checkEmail"
        />
        <Button type="submit" size="lg" class="w-full" :loading="busy" :disabled="cooldown.remaining.value > 0">{{ submitLabel }}</Button>
        <p class="sr-only" aria-live="polite">{{ announcement }}</p>
      </form>
      <p class="mt-6 text-sm text-text-muted">
        {{ $t('auth.code.request.backPrompt') }}
        <RouterLink
          :to="{ ...withReturnUrl('/signin'), state: { email: email.trim() } }"
          class="font-semibold text-text-link underline underline-offset-2 hover:text-text-link-hover"
          data-testid="back-to-password"
        >
          {{ $t('auth.code.request.backLink') }}
        </RouterLink>
      </p>
    </template>
  </AuthPage>
</template>
