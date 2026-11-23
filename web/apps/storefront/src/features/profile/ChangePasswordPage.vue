<script setup lang="ts">
import { ChevronLeft } from '@lucide/vue'
import { passwordChecks, passwordRules, useCooldown } from '@hivespace/core'
import { Alert, Button, Card, GoogleIcon, PasswordField, PasswordRules, Spinner } from '@hivespace/ui'
import { computed, nextTick, onMounted, reactive, ref, useId, useTemplateRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'

import { changePassword, loadProfile, setPassword, type Failure } from './api'
import { goTo } from './navigation'
import { useProfileSession } from './sessionPort'
import { notify } from './toasts'

// SCR-STF-018 (ui-design.md). One form, three password fields; the confirm check stays in the browser and is never sent (D-B).
// FEAT-005: the page reads the profile once and picks its mode: change (the account has a password) or set (it has none, FR-AUTH-027).
const { t } = useI18n()
const session = useProfileSession()
const headingId = useId()

type Field = 'current' | 'next' | 'confirm'
type Focusable = { focus: () => void }

const values = reactive<Record<Field, string>>({ current: '', next: '', confirm: '' })
const errors = reactive<Record<Field, string>>({ current: '', next: '', confirm: '' })
const serverRuleMessages = ref<string[]>([])
const showRuleErrors = ref(false)
const submitting = ref(false)
const notSet = ref('')
const loadState = ref<'loading' | 'ready' | 'failed'>('loading')
const hasPassword = ref(true)
const reauthMessage = ref('')
const lockedMessage = ref('')
const rateLimitedMessage = ref('')
const switchedNotice = ref('')
const cooldown = useCooldown(60)
const googleRef = ref<{ $el?: HTMLElement } | null>(null)
const failed = ref(false)
const heading = useTemplateRef<HTMLElement>('heading')
const currentRef = ref<Focusable | null>(null)
const nextRef = ref<Focusable | null>(null)
const confirmRef = ref<Focusable | null>(null)
const refs = { current: currentRef, next: nextRef, confirm: confirmRef }

const setMode = computed(() => !hasPassword.value)
const order = computed<Field[]>(() => (setMode.value ? ['next', 'confirm'] : ['current', 'next', 'confirm']))
const labels = computed<Record<Field, string>>(() => ({
  current: t('account.password.fields.current'),
  next: t('account.password.fields.new'),
  confirm: t('account.password.fields.confirm'),
}))
const rules = computed(() => passwordRules(values.next, (key) => t(`auth.rules.${key}`)))
const problems = computed(() => order.value.filter((field) => errors[field]).map((field) => ({ field, label: labels.value[field], message: errors[field] })))

function check(field: Field): boolean {
  let message = ''
  if (field === 'current') {
    message = values.current ? '' : t('account.password.errors.currentRequired')
  } else if (field === 'next') {
    const checks = passwordChecks(values.next)
    message = !values.next
      ? t('account.password.errors.newRequired')
      : checks.length && checks.letter && checks.digit
        ? ''
        : t('account.password.errors.rules')
    showRuleErrors.value = Boolean(message) && Boolean(values.next)
  } else {
    message = !values.confirm
      ? t('account.password.errors.confirmRequired')
      : values.confirm === values.next
        ? ''
        : t('account.password.errors.mismatch')
  }
  errors[field] = message
  return !message
}

function focusField(field: Field) {
  refs[field].value?.focus()
}

function reset() {
  for (const field of ['current', 'next', 'confirm'] as Field[]) {
    values[field] = ''
    errors[field] = ''
  }
  serverRuleMessages.value = []
  showRuleErrors.value = false
}

async function load() {
  loadState.value = 'loading'
  const result = await loadProfile()
  if (!result.ok) {
    if (result.failure.status === 401) {
      session.markSignedOut()
      return goTo('/bff/login?returnUrl=%2Fprofile%2Fpassword')
    }
    loadState.value = 'failed'
    return
  }
  hasPassword.value = result.data.hasPassword
  loadState.value = 'ready'
}

onMounted(load)

// The submit button shows the Retry-After countdown, announced at most every 10 s (ui-design "error", both modes).
const announcement = ref('')
const submitLabel = computed(() => {
  const base = setMode.value ? t('account.password.set.submit') : t('account.password.actions.submit')
  return cooldown.remaining.value > 0 ? t('account.password.actions.submitCooldown', { label: base, seconds: cooldown.remaining.value }) : base
})
watch(cooldown.remaining, (seconds) => {
  if (seconds > 0 && (seconds % 10 === 0 || seconds === 1)) announcement.value = t('account.password.actions.submitCooldown', { label: submitLabel.value, seconds })
})

/** The re-authentication action: through Google and back here (technical-design §3.6, D-5). */
function reauthenticate() {
  goTo('/bff/login?reauth=true&returnUrl=%2Fprofile%2Fpassword')
}

async function applyFailure(failure: Failure) {
  if (failure.status === 401) {
    session.markSignedOut()
    return goTo('/bff/login?returnUrl=%2Fprofile%2Fpassword')
  }
  if (failure.status === 429) {
    rateLimitedMessage.value = failure.message ?? t('account.password.errors.rateLimited')
    cooldown.start(failure.retryAfter ?? 60)
    return
  }
  if (failure.status === 423) {
    // Locked (FR-AUTH-012): the current password is cleared, the other fields keep their values (ui-design "error").
    lockedMessage.value = failure.message ?? ''
    values.current = ''
    return
  }
  if (failure.status === 403 && failure.code === 'identity.reauthentication_required') {
    reauthMessage.value = failure.message ?? t('account.password.errors.reauth')
    await nextTick()
    googleRef.value?.$el?.focus()
    return
  }
  if (failure.status === 409 && failure.code === 'identity.password_already_set') {
    // The profile was stale: the account has a password now, so this is the change form (ui-design "error", set mode).
    switchedNotice.value = failure.message ?? t('account.password.errors.alreadySet')
    reset()
    await load()
    return
  }
  if (failure.status === 409 && failure.code === 'identity.password_not_set') {
    notSet.value = failure.message ?? t('account.password.notices.notSet')
    return
  }
  if (failure.status === 400) {
    const current = failure.details.filter((detail) => detail.field === 'currentPassword')
    const next = failure.details.filter((detail) => detail.field === 'newPassword')
    if (current.length || next.length) {
      errors.current = current[0] ? (current[0].message ?? failure.message ?? '') : ''
      serverRuleMessages.value = next.map((detail) => detail.message ?? failure.message ?? '')
      showRuleErrors.value = next.length > 0
      // D-C: a wrong current password is cleared; what the user typed for the new one is kept.
      if (current.some((detail) => detail.code === 'identity.current_password_incorrect')) values.current = ''
      focusField(current.length ? 'current' : 'next')
      return
    }
  }
  failed.value = true
}

async function submit() {
  if (submitting.value || cooldown.remaining.value > 0) return
  notSet.value = ''
  failed.value = false
  reauthMessage.value = ''
  lockedMessage.value = ''
  rateLimitedMessage.value = ''
  switchedNotice.value = ''
  serverRuleMessages.value = []
  for (const field of order.value) check(field)
  const invalid = order.value.find((field) => errors[field])
  if (invalid) return focusField(invalid)

  submitting.value = true
  const wasSet = setMode.value
  const result = wasSet
    ? await setPassword({ newPassword: values.next })
    : await changePassword({ currentPassword: values.current, newPassword: values.next })
  submitting.value = false
  if (!result.ok) return applyFailure(result.failure)

  // Move focus first: the blur of a filled field passes its check, whereas clearing first would report it empty.
  heading.value?.focus()
  await nextTick()
  reset()
  await session.refresh()
  // A first password turns the page into the change form: the account has one now.
  if (wasSet) hasPassword.value = true
  notify({ variant: 'success', title: t(wasSet ? 'account.password.toasts.set' : 'account.password.toasts.changed') })
}
</script>

<template>
  <div class="mx-auto max-w-2xl">
    <RouterLink
      to="/profile"
      :aria-label="$t('account.password.backLabel')"
      class="mb-4 inline-flex min-h-11 items-center gap-1 text-sm text-text-link hover:text-text-link-hover hover:underline"
    >
      <ChevronLeft class="size-4" aria-hidden="true" />
      {{ $t('account.password.back') }}
    </RouterLink>

    <div v-if="loadState === 'loading'" role="status" class="py-8" data-testid="password-loading"><Spinner /></div>
    <div v-else-if="loadState === 'failed'" class="space-y-4" data-testid="password-load-failed">
      <Alert variant="error" :title="$t('account.password.errors.loadFailed')" />
      <Button variant="secondary" @click="load">{{ $t('account.password.actions.retry') }}</Button>
    </div>
    <template v-else>
    <div class="mb-6">
      <h1 :id="headingId" ref="heading" tabindex="-1" class="text-title-sm font-semibold text-text-primary focus:outline-none">
        {{ setMode ? $t('account.password.set.title') : $t('account.password.title') }}
      </h1>
      <p class="mt-1 text-sm text-text-muted">{{ setMode ? $t('account.password.set.subtitle') : $t('account.password.subtitle') }}</p>
    </div>

    <div v-if="notSet" class="mb-4"><Alert variant="info" :title="notSet" /></div>
    <div v-if="switchedNotice" class="mb-4"><Alert variant="info" :title="switchedNotice" data-testid="already-set" /></div>
    <div v-if="reauthMessage" class="mb-4" data-testid="reauth-alert">
      <Alert variant="info" :title="$t('account.password.reauth.title')" :message="reauthMessage">
        <template #action>
          <Button ref="googleRef" variant="secondary" size="md" class="w-full gap-3 sm:w-auto" :start-icon="GoogleIcon" data-testid="reauth-google" @click="reauthenticate">
            {{ $t('account.password.reauth.action') }}
          </Button>
          <p class="mt-2 text-xs text-text-muted">{{ $t('account.password.reauth.help') }}</p>
        </template>
      </Alert>
    </div>
    <div v-if="lockedMessage" class="mb-4">
      <Alert variant="warning" :title="$t('account.password.errors.lockedTitle')" :message="lockedMessage" data-testid="locked-alert" />
    </div>

    <Card>
      <form novalidate autocomplete="on" :aria-labelledby="headingId" :aria-busy="submitting" class="space-y-4" @submit.prevent="submit">
        <!-- Lets password managers attach the new password to the right account; never shown or sent. -->
        <input
          type="text"
          name="username"
          autocomplete="username"
          :value="session.email()"
          readonly
          tabindex="-1"
          aria-hidden="true"
          class="sr-only"
        />

        <Alert v-if="problems.length >= 3" variant="error" :title="$t('account.password.errors.summaryTitle')">
          <template #action>
            <ul class="list-disc space-y-1 pl-4">
              <li v-for="problem in problems" :key="problem.field">
                <button type="button" class="text-left underline underline-offset-2" @click="focusField(problem.field)">
                  {{ problem.label }}: {{ problem.message }}
                </button>
              </li>
            </ul>
          </template>
        </Alert>

        <PasswordField
          v-if="!setMode"
          ref="currentRef"
          v-model="values.current"
          autocomplete="current-password"
          required
          :label="labels.current"
          :error="errors.current"
          :show-label="$t('auth.actions.showPassword')"
          :hide-label="$t('auth.actions.hidePassword')"
          @blur="check('current')"
        />

        <div>
          <PasswordField
            ref="nextRef"
            v-model="values.next"
            autocomplete="new-password"
            required
            :label="labels.next"
            :error="errors.next"
            :show-label="$t('auth.actions.showPassword')"
            :hide-label="$t('auth.actions.hidePassword')"
            @blur="check('next')"
          />
          <PasswordRules :rules="rules" :show-errors="showRuleErrors" :label="$t('auth.rules.label')" />
          <ul v-if="serverRuleMessages.length" class="mt-2 space-y-1" role="alert">
            <li v-for="message in serverRuleMessages" :key="message" class="text-xs text-feedback-error-fg">{{ message }}</li>
          </ul>
        </div>

        <PasswordField
          ref="confirmRef"
          v-model="values.confirm"
          autocomplete="new-password"
          required
          :label="labels.confirm"
          :error="errors.confirm"
          :show-label="$t('auth.actions.showPassword')"
          :hide-label="$t('auth.actions.hidePassword')"
          @blur="check('confirm')"
        />

        <Alert v-if="failed" variant="error" :title="$t('account.password.errors.failed')" />
        <Alert
          v-if="rateLimitedMessage"
          variant="warning"
          :title="$t('account.password.errors.rateLimitedTitle')"
          :message="rateLimitedMessage"
          data-testid="rate-limited-alert"
        />

        <div class="flex sm:justify-end">
          <Button type="submit" size="md" :loading="submitting" :disabled="cooldown.remaining.value > 0" class="w-full sm:w-auto">{{ submitLabel }}</Button>
        </div>
        <p class="sr-only" aria-live="polite">{{ announcement }}</p>
      </form>
    </Card>
    </template>
  </div>
</template>
