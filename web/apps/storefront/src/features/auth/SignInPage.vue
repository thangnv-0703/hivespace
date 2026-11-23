<script setup lang="ts">
import { Alert, Button, Input, PasswordField, Spinner } from '@hivespace/ui'
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'

import { signInWithPassword } from './api'
import AuthPage from './AuthPage.vue'
import GoogleButton from './GoogleButton.vue'
import { GOOGLE_ERROR_TONES, googleErrorOf } from './googleErrors'
import { goTo } from './navigation'
import { EMAIL_PATTERN, fieldOf, returnUrlOf } from './validation'

// SCR-STF-012. Reached from Identity's authorize redirect with ?returnUrl=/connect/authorize... (passed through unchanged).
const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const email = ref('')
const password = ref('')
const errors = reactive({ email: '', password: '' })
const formError = ref<{ message: string; code?: string } | null>(null)
const busy = ref(false)
const googleBusy = ref(false)
// A failed Google attempt comes back as ?error=<code> (FEAT-004). Read once: the URL drops it so a reload or Back does not
// show it again. Only one alert shows at a time; a new password submit clears it.
const googleError = ref(googleErrorOf(route.query.error))
const googleAlert = computed(() =>
  googleError.value
    ? {
        variant: GOOGLE_ERROR_TONES[googleError.value],
        title: t(`auth.errors.${googleError.value}.title`),
        message: t(`auth.errors.${googleError.value}.message`),
      }
    : null,
)
const emailRef = ref<{ focus: () => void } | null>(null)
const passwordRef = ref<{ focus: () => void } | null>(null)

const returnUrl = computed(() => returnUrlOf(route.query.returnUrl))
// Without a returnUrl this page was opened directly (a typed address, "Sign in" after verifying). Signing in here would
// create only the Identity cookie and no gateway session, so start the BFF login, which comes back with a returnUrl.
const starting = !returnUrl.value
onMounted(() => {
  if (starting) goTo('/bff/login')
  else if (route.query.error !== undefined) {
    const query = { ...route.query }
    delete query.error
    void router.replace({ query })
  }
})
const registerTarget = computed(() => ({ path: '/register', query: returnUrl.value ? { returnUrl: returnUrl.value } : {} }))

function checkEmail(): boolean {
  const value = email.value.trim()
  errors.email = !value ? t('auth.errors.emailRequired') : EMAIL_PATTERN.test(value) ? '' : t('auth.errors.emailInvalid')
  return !errors.email
}

function checkPassword(): boolean {
  errors.password = password.value ? '' : t('auth.errors.passwordRequired')
  return !errors.password
}

async function submit() {
  formError.value = null
  googleError.value = null
  const emailOk = checkEmail()
  const passwordOk = checkPassword()
  if (!emailOk) return emailRef.value?.focus()
  if (!passwordOk) return passwordRef.value?.focus()

  busy.value = true
  const result = await signInWithPassword({ email: email.value.trim(), password: password.value, returnUrl: returnUrl.value })
  if (result.ok) {
    // Stay in the loading state until the browser leaves: no flash of the form.
    goTo(result.data.redirectUrl)
    return
  }
  busy.value = false
  const { failure } = result
  if (failure.status === 400) {
    for (const detail of failure.details) {
      if (fieldOf(detail) === 'email') errors.email = detail.message ?? t('auth.errors.emailInvalid')
    }
    return emailRef.value?.focus()
  }
  formError.value = {
    message: failure.status === 401 && failure.message ? failure.message : t('auth.errors.network'),
    code: failure.status === 401 ? undefined : failure.code,
  }
  if (failure.status === 401) {
    password.value = ''
    passwordRef.value?.focus()
  }
}
</script>

<template>
  <AuthPage :title="$t('auth.signIn.title')" :subtitle="$t('auth.signIn.subtitle')">
    <div v-if="starting" role="status" data-testid="signin-starting"><Spinner /></div>
    <template v-else>
      <Alert
        v-if="googleAlert"
        class="mb-6"
        :variant="googleAlert.variant"
        :title="googleAlert.title"
        :message="googleAlert.message"
        data-testid="google-alert"
      />
      <GoogleButton :return-url="returnUrl" :disabled="busy" @start="googleBusy = true" />
    </template>
    <form v-if="!starting" class="space-y-4" novalidate :aria-busy="busy" @submit.prevent="submit">
      <Alert v-if="formError" variant="error" :title="$t('auth.signIn.failedTitle')" :message="formError.message" />
      <p v-if="formError?.code" class="text-xs text-text-muted">{{ formError.code }}</p>
      <Input
        ref="emailRef"
        v-model="email"
        type="email"
        autocomplete="email"
        inputmode="email"
        required
        :disabled="busy || googleBusy"
        :label="$t('auth.fields.email')"
        :error="errors.email"
        @blur="checkEmail"
      />
      <PasswordField
        ref="passwordRef"
        v-model="password"
        autocomplete="current-password"
        :placeholder="$t('auth.signIn.passwordPlaceholder')"
        required
        :disabled="busy || googleBusy"
        :label="$t('auth.fields.password')"
        :error="errors.password"
        :show-label="$t('auth.actions.showPassword')"
        :hide-label="$t('auth.actions.hidePassword')"
        @blur="checkPassword"
      />
      <Button type="submit" size="lg" class="w-full" :loading="busy" :disabled="googleBusy">{{ $t('auth.signIn.submit') }}</Button>
    </form>
    <p class="mt-6 text-sm text-text-muted">
      {{ $t('auth.signIn.noAccount') }}
      <RouterLink :to="registerTarget" class="text-text-link underline underline-offset-2 hover:text-text-link-hover">
        {{ $t('auth.signIn.createAccount') }}
      </RouterLink>
    </p>
  </AuthPage>
</template>
