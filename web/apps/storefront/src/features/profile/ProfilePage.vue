<script setup lang="ts">
import { Alert, Button, Card, Input, RadioGroup, Select, type SelectOption } from '@hivespace/ui'
import { computed, nextTick, onMounted, reactive, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'

import { loadProfile, saveProfile, type Failure, type Profile } from './api'
import { checkDate, EMPTY_DATE, formatPhone, joinDate, splitDate, type DateParts } from './dateOfBirth'
import { goTo } from './navigation'
import { useProfileSession } from './sessionPort'
import { notify } from './toasts'
import { useLanguagePreference } from './useLanguagePreference'

// SCR-STF-016 (ui-design.md). Two cards: the details form, and the language card, which is outside the form and saves on selection.
const { t, locale } = useI18n()
const session = useProfileSession()
const language = useLanguagePreference()
const headingId = useId()
const languageHeadingId = useId()
const passwordHeadingId = useId()
const dateErrorId = useId()

type Field = 'displayName' | 'phone' | 'gender' | 'dateOfBirth'
type Focusable = { focus: () => void }

const state = ref<'loading' | 'ready' | 'failed'>('loading')
const hasPassword = ref(true)
const loadFailure = ref<Failure | null>(null)
const email = ref('')
const displayName = ref('')
const savedName = ref('')
const phone = ref('')
const gender = ref<string | null>(null)
const date = reactive<DateParts>({ ...EMPTY_DATE })
const errors = reactive<Record<Field, string>>({ displayName: '', phone: '', gender: '', dateOfBirth: '' })
const saving = ref(false)
const saveError = ref(false)
const nameRef = ref<Focusable | null>(null)
const phoneRef = ref<Focusable | null>(null)
const genderRef = ref<HTMLElement | null>(null)
const dateRef = ref<HTMLElement | null>(null)

const labels = computed<Record<Field, string>>(() => ({
  displayName: t('profile.fields.displayName'),
  phone: t('profile.fields.phone'),
  gender: t('profile.fields.gender'),
  dateOfBirth: t('profile.fields.dateOfBirth'),
}))
const order: Field[] = ['displayName', 'phone', 'gender', 'dateOfBirth']
const problems = computed(() => order.filter((field) => errors[field]).map((field) => ({ field, label: labels.value[field], message: errors[field] })))

const initials = computed(() => {
  const words = (savedName.value || displayName.value).trim().split(/\s+/).filter(Boolean)
  return ((words[0]?.[0] ?? '') + (words.length > 1 ? (words[words.length - 1]?.[0] ?? '') : '')).toUpperCase()
})
const genderOptions = computed(() =>
  (['male', 'female', 'other', 'preferNotToSay'] as const).map((key) => ({
    label: t(`profile.gender.${key}`),
    value: key === 'preferNotToSay' ? 'prefer_not_to_say' : key,
  })),
)
const languageOptions = [
  { label: 'Tiếng Việt', value: 'vi' },
  { label: 'English', value: 'en' },
]
const dayOptions: SelectOption[] = Array.from({ length: 31 }, (_, i) => ({ label: String(i + 1), value: String(i + 1) }))
const monthOptions = computed<SelectOption[]>(() => {
  const format = new Intl.DateTimeFormat(locale.value, { month: 'long', timeZone: 'UTC' })
  return Array.from({ length: 12 }, (_, i) => ({ label: format.format(new Date(Date.UTC(2000, i, 1))), value: String(i + 1) }))
})
const yearOptions = computed<SelectOption[]>(() => {
  const current = new Date().getFullYear()
  return Array.from({ length: 121 }, (_, i) => ({ label: String(current - i), value: String(current - i) }))
})
const dateClass = computed(() => (errors.dateOfBirth ? '!border-feedback-error-icon' : ''))
const hasDate = computed(() => Boolean(date.day || date.month || date.year))

function fill(profile: Profile) {
  hasPassword.value = profile.hasPassword
  email.value = profile.email
  displayName.value = profile.displayName
  savedName.value = profile.displayName
  phone.value = formatPhone(profile.phone)
  gender.value = profile.gender
  Object.assign(date, splitDate(profile.dateOfBirth))
}

async function load() {
  state.value = 'loading'
  const result = await loadProfile()
  if (result.ok) {
    fill(result.data)
    state.value = 'ready'
    return
  }
  if (result.failure.status === 401) return signInAgain()
  loadFailure.value = result.failure
  state.value = 'failed'
}

function signInAgain() {
  session.markSignedOut()
  goTo('/bff/login?returnUrl=%2Fprofile')
}

/** Client checks give early feedback; the server is the authority for everything else (ui-design "error"). */
function check(field: Field): boolean {
  let message = ''
  if (field === 'displayName') {
    message = displayName.value.trim() ? '' : t('profile.errors.displayNameRequired')
  } else if (field === 'dateOfBirth') {
    const result = checkDate(date)
    message = result === 'incomplete' ? t('profile.errors.dateOfBirthIncomplete') : result === 'invalid' ? t('profile.errors.dateOfBirthInvalid') : ''
  }
  errors[field] = message
  return !message
}

function focusField(field: Field) {
  if (field === 'displayName') nameRef.value?.focus()
  else if (field === 'phone') phoneRef.value?.focus()
  else (field === 'gender' ? genderRef.value : dateRef.value)?.querySelector<HTMLElement>('input, select')?.focus()
}

function applyFailure(failure: Failure) {
  if (failure.status === 401) return signInAgain()
  if (failure.status === 400) {
    let shown = false
    for (const detail of failure.details) {
      const field = order.find((name) => name === detail.field)
      if (field) {
        errors[field] = detail.message ?? failure.message ?? ''
        shown = true
      }
    }
    if (shown) {
      const first = order.find((field) => errors[field])
      if (first) focusField(first)
      return
    }
  }
  saveError.value = true
}

async function submit() {
  if (saving.value) return
  saveError.value = false
  for (const field of order) check(field)
  const invalid = order.find((field) => errors[field])
  if (invalid) return focusField(invalid)

  saving.value = true
  const result = await saveProfile({
    displayName: displayName.value.trim(),
    phone: phone.value.trim() || null,
    gender: (gender.value as Profile['gender']) ?? null,
    dateOfBirth: joinDate(date),
  })
  saving.value = false
  if (!result.ok) return applyFailure(result.failure)

  for (const field of order) errors[field] = ''
  fill(result.data)
  await session.refresh()
  notify({ variant: 'success', title: t('profile.toasts.saved') })
}

function clearDate() {
  Object.assign(date, EMPTY_DATE)
  errors.dateOfBirth = ''
}

async function retry() {
  await load()
  await nextTick()
}

onMounted(load)
</script>

<template>
  <div class="mx-auto max-w-2xl" :aria-busy="state === 'loading'">
    <div class="mb-6">
      <h1 :id="headingId" class="text-title-sm font-semibold text-text-primary">{{ $t('profile.title') }}</h1>
      <p class="mt-1 text-sm text-text-muted">{{ $t('profile.subtitle') }}</p>
    </div>

    <!-- loading: skeleton in the shape of the two cards -->
    <div v-if="state === 'loading'" class="space-y-6" data-testid="profile-loading">
      <Card>
        <div class="animate-pulse space-y-4" aria-hidden="true">
          <div class="flex items-center gap-4">
            <div class="size-16 rounded-full bg-bg-subtle" />
            <div class="space-y-2">
              <div class="h-5 w-40 rounded bg-bg-subtle" />
              <div class="h-4 w-56 rounded bg-bg-subtle" />
            </div>
          </div>
          <div v-for="n in 5" :key="n" class="space-y-2">
            <div class="h-4 w-24 rounded bg-bg-subtle" />
            <div class="h-11 rounded-lg bg-bg-subtle" />
          </div>
          <div class="ml-auto h-11 w-40 rounded-lg bg-bg-subtle" />
        </div>
      </Card>
      <Card>
        <div class="animate-pulse space-y-3" aria-hidden="true">
          <div class="h-5 w-24 rounded bg-bg-subtle" />
          <div class="h-5 w-32 rounded bg-bg-subtle" />
          <div class="h-5 w-32 rounded bg-bg-subtle" />
        </div>
      </Card>
    </div>

    <!-- error: the profile could not be loaded -->
    <Alert v-else-if="state === 'failed'" variant="error" :title="$t('profile.errors.loadFailed')">
      <template #action>
        <p v-if="loadFailure?.code" class="mb-2 text-xs text-text-muted">{{ loadFailure.code }}</p>
        <Button variant="secondary" size="sm" @click="retry">{{ $t('profile.actions.retry') }}</Button>
      </template>
    </Alert>

    <div v-else class="space-y-6">
      <Card>
        <form novalidate :aria-labelledby="headingId" :aria-busy="saving" class="space-y-4" @submit.prevent="submit">
          <div class="flex items-center gap-4">
            <!-- Initials until avatars arrive with FEAT-035. -->
            <span
              class="flex size-16 shrink-0 items-center justify-center rounded-full bg-brand-100 text-xl font-semibold text-brand-700"
              aria-hidden="true"
            >
              {{ initials }}
            </span>
            <div class="min-w-0">
              <p class="truncate text-lg font-semibold text-text-primary" data-testid="profile-name">{{ savedName }}</p>
              <p class="truncate text-sm text-text-muted">{{ email }}</p>
            </div>
          </div>

          <Alert v-if="problems.length >= 3" variant="error" :title="$t('profile.errors.summaryTitle')">
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

          <Input
            :model-value="email"
            :label="$t('profile.fields.email')"
            :hint="$t('profile.help.email')"
            readonly
            aria-readonly="true"
            class="!border-transparent !bg-bg-subtle"
            data-testid="profile-email"
          />
          <Input
            ref="nameRef"
            v-model="displayName"
            :label="$t('profile.fields.displayName')"
            :hint="$t('profile.help.displayName')"
            :error="errors.displayName"
            required
            autocomplete="name"
            @blur="check('displayName')"
          />
          <Input
            ref="phoneRef"
            v-model="phone"
            type="tel"
            inputmode="tel"
            autocomplete="tel"
            placeholder="0912 345 678"
            :label="$t('profile.fields.phone')"
            :hint="$t('profile.help.phone')"
            :error="errors.phone"
          />

          <div ref="genderRef">
            <div class="mb-1.5 flex items-center justify-between">
              <span class="text-sm font-medium text-text-secondary">{{ $t('profile.fields.gender') }}</span>
              <button
                v-if="gender"
                type="button"
                class="text-sm text-text-link hover:text-text-link-hover hover:underline"
                @click="gender = null"
              >
                {{ $t('profile.actions.clear') }}
              </button>
            </div>
            <RadioGroup v-model="gender" direction="horizontal" :label="$t('profile.fields.gender')" :options="genderOptions" />
            <p v-if="errors.gender" role="alert" class="mt-1 text-xs text-feedback-error-fg">{{ errors.gender }}</p>
          </div>

          <fieldset ref="dateRef" :aria-describedby="errors.dateOfBirth ? dateErrorId : undefined">
            <div class="mb-1.5 flex items-center justify-between">
              <legend class="text-sm font-medium text-text-secondary">{{ $t('profile.fields.dateOfBirth') }}</legend>
              <button
                v-if="hasDate"
                type="button"
                class="text-sm text-text-link hover:text-text-link-hover hover:underline"
                @click="clearDate"
              >
                {{ $t('profile.actions.clear') }}
              </button>
            </div>
            <div class="flex gap-3">
              <div class="min-w-0 flex-1 sm:flex-none sm:w-28">
                <Select v-model="date.day" :aria-label="$t('profile.fields.day')" :placeholder="$t('profile.fields.day')" :options="dayOptions" :class="dateClass" />
              </div>
              <div class="min-w-0 flex-1 sm:flex-none sm:w-40">
                <Select v-model="date.month" :aria-label="$t('profile.fields.month')" :placeholder="$t('profile.fields.month')" :options="monthOptions" :class="dateClass" />
              </div>
              <div class="min-w-0 flex-1 sm:flex-none sm:w-28">
                <Select v-model="date.year" :aria-label="$t('profile.fields.year')" :placeholder="$t('profile.fields.year')" :options="yearOptions" :class="dateClass" />
              </div>
            </div>
            <p v-if="errors.dateOfBirth" :id="dateErrorId" role="alert" class="mt-1 text-xs text-feedback-error-fg">{{ errors.dateOfBirth }}</p>
          </fieldset>

          <Alert v-if="saveError" variant="error" :title="$t('profile.errors.saveFailed')" />

          <div class="flex sm:justify-end">
            <Button type="submit" size="md" :loading="saving" class="w-full sm:w-auto">{{ $t('profile.actions.save') }}</Button>
          </div>
        </form>
      </Card>

      <section :aria-labelledby="languageHeadingId" :aria-busy="language.busy.value">
        <Card>
          <h2 :id="languageHeadingId" class="text-lg font-semibold text-text-primary">{{ $t('profile.language.title') }}</h2>
          <p class="mb-3 mt-1 text-sm text-text-muted">{{ $t('profile.help.language') }}</p>
          <RadioGroup
            :model-value="locale"
            :label="$t('profile.language.title')"
            :options="languageOptions"
            @update:model-value="language.change(String($event))"
          />
        </Card>
      </section>

      <section :aria-labelledby="passwordHeadingId">
        <Card>
          <h2 :id="passwordHeadingId" class="text-lg font-semibold text-text-primary">{{ $t('profile.password.title') }}</h2>
          <p class="mb-3 mt-1 text-sm text-text-muted">{{ hasPassword ? $t('profile.password.body') : $t('profile.password.setBody') }}</p>
          <RouterLink v-slot="{ href, navigate }" to="/profile/password" custom>
            <Button variant="secondary" size="md" :href="href" class="w-full sm:w-auto" @click="navigate">
              {{ hasPassword ? $t('profile.password.action') : $t('profile.password.setAction') }}
            </Button>
          </RouterLink>
        </Card>
      </section>
    </div>
  </div>
</template>
