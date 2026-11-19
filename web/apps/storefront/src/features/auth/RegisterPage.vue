<script setup lang="ts">
import { Mail } from '@lucide/vue'
import { Alert, Button, Input, PasswordRules } from '@hivespace/ui'
import { computed, nextTick, reactive, ref, type Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'

import { register } from './api'
import AuthPage from './AuthPage.vue'
import { focusTitle } from './navigation'
import PasswordField from './PasswordField.vue'
import ResendControl from './ResendControl.vue'
import { DISPLAY_NAME_MAX, EMAIL_PATTERN, fieldOf, passwordChecks, passwordRules, returnUrlOf } from './validation'

// SCR-STF-013. One route, two views: the form, then "check your email" as soon as the API answers 201 (UC-001 4a).
const { t } = useI18n()
const route = useRoute()

type Field = 'displayName' | 'email' | 'password' | 'confirm'
type Focusable = { focus: () => void }
const view = ref<'form' | 'notice'>('form')
const values = reactive({ displayName: '', email: '', password: '', confirm: '' })
const errors = reactive<Record<Field, string>>({ displayName: '', email: '', password: '', confirm: '' })
const passwordMessages = ref<string[]>([])
const emailTaken = ref(false)
const showRuleErrors = ref(false)
const formError = ref<{ message: string; code?: string } | null>(null)
const busy = ref(false)
// Template refs by field, so errors can move focus to the first invalid one.
const displayNameRef = ref<Focusable | null>(null)
const emailRef = ref<Focusable | null>(null)
const passwordRef = ref<Focusable | null>(null)
const confirmRef = ref<Focusable | null>(null)
const refs: Record<Field, Ref<Focusable | null>> = {
  displayName: displayNameRef,
  email: emailRef,
  password: passwordRef,
  confirm: confirmRef,
}

const returnUrl = computed(() => returnUrlOf(route.query.returnUrl))
const signInTarget = computed(() => ({ path: '/signin', query: returnUrl.value ? { returnUrl: returnUrl.value } : {} }))
const rules = computed(() => passwordRules(values.password, (key) => t(`auth.rules.${key}`)))
const labels = computed<Record<Field, string>>(() => ({
  displayName: t('auth.fields.displayName'),
  email: t('auth.fields.email'),
  password: t('auth.fields.password'),
  confirm: t('auth.fields.confirmPassword'),
}))
const order: Field[] = ['displayName', 'email', 'password', 'confirm']
const problems = computed(() => order.filter((field) => errors[field]).map((field) => ({ field, label: labels.value[field], message: errors[field] })))

function check(field: Field): boolean {
  const text = values[field]
  let message = ''
  if (field === 'displayName') {
    message = !text.trim() ? t('auth.errors.displayNameRequired') : text.trim().length > DISPLAY_NAME_MAX ? t('auth.errors.displayNameTooLong') : ''
  } else if (field === 'email') {
    message = !text.trim() ? t('auth.errors.emailRequired') : EMAIL_PATTERN.test(text.trim()) ? '' : t('auth.errors.emailInvalid')
    emailTaken.value = false
  } else if (field === 'password') {
    message = !text ? t('auth.errors.passwordRequired') : ''
    passwordMessages.value = []
  } else {
    message = !text ? t('auth.errors.confirmRequired') : text !== values.password ? t('auth.errors.passwordMismatch') : ''
  }
  errors[field] = message
  return !message
}

function firstInvalid(): Field | undefined {
  return order.find((field) => errors[field])
}

async function submit() {
  formError.value = null
  for (const field of order) check(field)
  const checks = passwordChecks(values.password)
  showRuleErrors.value = !(checks.length && checks.letter && checks.digit)
  const invalid = firstInvalid()
  if (invalid) return refs[invalid].value?.focus()

  busy.value = true
  const result = await register({ email: values.email.trim(), displayName: values.displayName.trim(), password: values.password })
  busy.value = false
  if (result.ok) {
    try {
      sessionStorage.setItem('hs-verify-email', values.email.trim())
    } catch {
      // storage blocked: the verify page just starts with an empty email box
    }
    view.value = 'notice'
    await nextTick()
    focusTitle()
    return
  }
  applyFailure(result.failure)
}

function applyFailure(failure: { status: number; code: string; message?: string; details: { field: string; code: string; message?: string }[] }) {
  if (failure.status === 409) {
    errors.email = failure.message ?? t('auth.errors.emailInUse')
    emailTaken.value = true
    return refs.email.value?.focus()
  }
  if (failure.status === 400) {
    const passwordProblems: string[] = []
    for (const detail of failure.details) {
      const field = fieldOf(detail)
      if (field === 'password') passwordProblems.push(detail.message ?? t('auth.errors.passwordRules'))
      else if (field) errors[field] = detail.message ?? t(`auth.errors.${field}Required`)
    }
    if (passwordProblems.length > 0) {
      passwordMessages.value = passwordProblems
      showRuleErrors.value = true
    }
    const first = firstInvalid() ?? (passwordProblems.length > 0 ? 'password' : undefined)
    if (first) return refs[first].value?.focus()
  }
  formError.value = { message: t('auth.errors.network'), code: failure.code }
}

async function goBack() {
  values.password = ''
  values.confirm = ''
  errors.password = ''
  errors.confirm = ''
  view.value = 'form'
  await nextTick()
  focusTitle()
}
</script>

<template>
  <AuthPage
    v-if="view === 'form'"
    :title="$t('auth.register.title')"
    :subtitle="$t('auth.register.subtitle')"
  >
    <form class="space-y-4" novalidate :aria-busy="busy" @submit.prevent="submit">
      <Alert v-if="formError" variant="error" :title="formError.message" />
      <p v-if="formError?.code" class="text-xs text-text-muted">{{ formError.code }}</p>
      <Alert v-if="problems.length >= 3" variant="error" :title="$t('auth.register.summaryTitle')">
        <template #action>
          <ul class="list-disc space-y-1 pl-4">
            <li v-for="problem in problems" :key="problem.field">
              <button type="button" class="text-left underline underline-offset-2" @click="refs[problem.field].value?.focus()">
                {{ problem.label }}: {{ problem.message }}
              </button>
            </li>
          </ul>
        </template>
      </Alert>
      <Input
        ref="displayNameRef"
        v-model="values.displayName"
        autocomplete="name"
        required
        :disabled="busy"
        :label="$t('auth.fields.displayName')"
        :error="errors.displayName"
        @blur="check('displayName')"
      />
      <div>
        <Input
          ref="emailRef"
          v-model="values.email"
          type="email"
          autocomplete="email"
          inputmode="email"
          required
          :disabled="busy"
          :label="$t('auth.fields.email')"
          :error="errors.email"
          @blur="check('email')"
        />
        <p v-if="emailTaken" class="mt-1 text-xs">
          <RouterLink :to="signInTarget" class="text-text-link underline underline-offset-2 hover:text-text-link-hover">
            {{ $t('auth.register.emailInUseSignIn') }}
          </RouterLink>
        </p>
      </div>
      <div>
        <PasswordField
          ref="passwordRef"
          v-model="values.password"
          autocomplete="new-password"
          required
          :disabled="busy"
          :label="$t('auth.fields.password')"
          :error="errors.password"
          :show-label="$t('auth.actions.showPassword')"
          :hide-label="$t('auth.actions.hidePassword')"
          @blur="check('password')"
        />
        <PasswordRules :rules="rules" :show-errors="showRuleErrors" :label="$t('auth.rules.label')" />
        <ul v-if="passwordMessages.length" class="mt-2 space-y-1" role="alert">
          <li v-for="message in passwordMessages" :key="message" class="text-xs text-feedback-error-fg">{{ message }}</li>
        </ul>
      </div>
      <PasswordField
        ref="confirmRef"
        v-model="values.confirm"
        autocomplete="new-password"
        required
        :disabled="busy"
        :label="$t('auth.fields.confirmPassword')"
        :error="errors.confirm"
        :show-label="$t('auth.actions.showPassword')"
        :hide-label="$t('auth.actions.hidePassword')"
        @blur="check('confirm')"
      />
      <Button type="submit" size="lg" class="w-full" :loading="busy">{{ $t('auth.register.submit') }}</Button>
    </form>
    <p class="mt-6 text-sm text-text-muted">
      {{ $t('auth.register.haveAccount') }}
      <RouterLink :to="signInTarget" class="text-text-link underline underline-offset-2 hover:text-text-link-hover">
        {{ $t('auth.register.signIn') }}
      </RouterLink>
    </p>
  </AuthPage>

  <AuthPage v-else :title="$t('auth.register.noticeTitle')" :subtitle="$t('auth.register.noticeSubtitle')">
    <div
      class="flex flex-col items-center gap-3 rounded-xl border border-border-default bg-bg-surface p-6 text-center shadow-sm"
      data-testid="register-notice"
    >
      <div class="flex size-16 items-center justify-center rounded-full bg-brand-50 text-brand-600">
        <Mail class="size-8" aria-hidden="true" />
      </div>
      <p class="text-md text-text-secondary">{{ $t('auth.register.noticeBody', { email: values.email.trim() }) }}</p>
      <p class="text-sm text-text-muted">{{ $t('auth.register.noticeValid') }}</p>
      <ResendControl :email="values.email.trim()" />
    </div>
    <p class="mt-6 flex flex-col gap-2 text-sm">
      <button type="button" class="text-left text-text-link underline underline-offset-2 hover:text-text-link-hover" @click="goBack">
        {{ $t('auth.register.wrongAddress') }}
      </button>
      <RouterLink :to="signInTarget" class="text-text-link underline underline-offset-2 hover:text-text-link-hover">
        {{ $t('auth.register.signIn') }}
      </RouterLink>
    </p>
  </AuthPage>
</template>
