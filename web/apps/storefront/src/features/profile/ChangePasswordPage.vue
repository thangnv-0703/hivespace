<script setup lang="ts">
import { ChevronLeft } from '@lucide/vue'
import { passwordChecks, passwordRules } from '@hivespace/core'
import { Alert, Button, Card, PasswordField, PasswordRules } from '@hivespace/ui'
import { computed, nextTick, reactive, ref, useId, useTemplateRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'

import { changePassword, type Failure } from './api'
import { goTo } from './navigation'
import { useProfileSession } from './sessionPort'
import { notify } from './toasts'

// SCR-STF-018 (ui-design.md). One form, three password fields; the confirm check stays in the browser and is never sent (D-B).
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
const failed = ref(false)
const heading = useTemplateRef<HTMLElement>('heading')
const currentRef = ref<Focusable | null>(null)
const nextRef = ref<Focusable | null>(null)
const confirmRef = ref<Focusable | null>(null)
const refs = { current: currentRef, next: nextRef, confirm: confirmRef }

const order: Field[] = ['current', 'next', 'confirm']
const labels = computed<Record<Field, string>>(() => ({
  current: t('account.password.fields.current'),
  next: t('account.password.fields.new'),
  confirm: t('account.password.fields.confirm'),
}))
const rules = computed(() => passwordRules(values.next, (key) => t(`auth.rules.${key}`)))
const problems = computed(() => order.filter((field) => errors[field]).map((field) => ({ field, label: labels.value[field], message: errors[field] })))

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
  for (const field of order) {
    values[field] = ''
    errors[field] = ''
  }
  serverRuleMessages.value = []
  showRuleErrors.value = false
}

function applyFailure(failure: Failure) {
  if (failure.status === 401) {
    session.markSignedOut()
    return goTo('/bff/login?returnUrl=%2Fprofile%2Fpassword')
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
  if (submitting.value) return
  notSet.value = ''
  failed.value = false
  serverRuleMessages.value = []
  for (const field of order) check(field)
  const invalid = order.find((field) => errors[field])
  if (invalid) return focusField(invalid)

  submitting.value = true
  const result = await changePassword({ currentPassword: values.current, newPassword: values.next })
  submitting.value = false
  if (!result.ok) return applyFailure(result.failure)

  // Move focus first: the blur of a filled field passes its check, whereas clearing first would report it empty.
  heading.value?.focus()
  await nextTick()
  reset()
  await session.refresh()
  notify({ variant: 'success', title: t('account.password.toasts.changed') })
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

    <div class="mb-6">
      <h1 :id="headingId" ref="heading" tabindex="-1" class="text-title-sm font-semibold text-text-primary focus:outline-none">
        {{ $t('account.password.title') }}
      </h1>
      <p class="mt-1 text-sm text-text-muted">{{ $t('account.password.subtitle') }}</p>
    </div>

    <div v-if="notSet" class="mb-4"><Alert variant="info" :title="notSet" /></div>

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

        <div class="flex sm:justify-end">
          <Button type="submit" size="md" :loading="submitting" class="w-full sm:w-auto">{{ $t('account.password.actions.submit') }}</Button>
        </div>
      </form>
    </Card>
  </div>
</template>
