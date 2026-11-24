<script setup lang="ts">
import { useCooldown } from '@hivespace/core'
import { Alert, Button, CodeInput } from '@hivespace/ui'
import { CircleCheck } from '@lucide/vue'
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'

import { requestCode, signInWithEmailCode } from './api'
import AuthPage from './AuthPage.vue'
import { clearChallenge, formatClock, loadChallenge, saveChallenge, secondsUntil, type CodeChallenge } from './codeChallenge'
import { goTo } from './navigation'
import { returnUrlOf } from './validation'

// SCR-STF-022 (FEAT-006). Reads the challenge stored by SCR-STF-021; without one it goes back there.
const { t } = useI18n()
const route = useRoute()
const router = useRouter()

type Mode = 'entry' | 'expired' | 'ended' | 'locked'

const challenge = ref<CodeChallenge | null>(loadChallenge())
const returnUrl = computed(() => challenge.value?.returnUrl ?? returnUrlOf(route.query.returnUrl))
const withReturnUrl = (path: string) => ({ path, query: returnUrl.value ? { returnUrl: returnUrl.value } : {} })

const code = ref('')
const mode = ref<Mode>('entry')
const codeError = ref('')
const alert = ref<{ variant: 'error' | 'warning'; title: string; message: string; code?: string } | null>(null)
const resent = ref(false)
const busy = ref(false)
const resendBusy = ref(false)
const codeRef = ref<{ focus: () => void } | null>(null)
const otherWayRef = ref<{ $el: HTMLElement } | null>(null)

// One clock drives both countdowns; the server stays the authority on expiry (ui-design D-D).
const now = ref(Date.now())
const ticker = setInterval(() => (now.value = Date.now()), 1000)
onBeforeUnmount(() => clearInterval(ticker))
const expirySeconds = computed(() => (challenge.value ? secondsUntil(challenge.value.expiresAt, now.value) : 0))
const resendSeconds = computed(() => (challenge.value ? secondsUntil(challenge.value.resendAvailableAt, now.value) : 0))
const submitCooldown = useCooldown(60)

const ended = computed(() => mode.value === 'expired' || mode.value === 'ended')
const resendLabel = computed(() =>
  resendSeconds.value > 0 ? t('auth.code.entry.resendIn', { time: formatClock(resendSeconds.value) }) : t('auth.code.entry.resend'),
)
const primaryLabel = computed(() => {
  if (ended.value) {
    return resendSeconds.value > 0 ? t('auth.code.entry.newCodeCooldown', { time: formatClock(resendSeconds.value) }) : t('auth.code.entry.newCode')
  }
  return submitCooldown.remaining.value > 0
    ? t('auth.signIn.submitCooldown', { seconds: submitCooldown.remaining.value })
    : t('auth.code.entry.submit')
})
// Countdowns change every second on screen but are announced at most every 10 s.
const announcement = ref('')
watch(resendSeconds, (seconds) => {
  if (seconds > 0 && seconds % 10 === 0) announcement.value = resendLabel.value
})
// The code ran out on the page: no request needed (ui-design "expired").
watch(expirySeconds, (seconds) => {
  if (seconds === 0 && mode.value === 'entry' && challenge.value) {
    showEnded('expired', t('auth.errors.identity.sign_in_code_expired.message'))
  }
})

onMounted(() => {
  if (!challenge.value) void router.replace(withReturnUrl('/signin/code'))
  else codeRef.value?.focus()
})

function showEnded(next: 'expired' | 'ended', message: string) {
  mode.value = next
  const key = next === 'expired' ? 'sign_in_code_expired' : 'sign_in_challenge_ended'
  alert.value = { variant: 'warning', title: t(`auth.errors.identity.${key}.title`), message }
}

async function submit() {
  if (!challenge.value || busy.value || mode.value !== 'entry' || code.value.length !== 6 || submitCooldown.remaining.value > 0) return
  codeError.value = ''
  alert.value = null
  resent.value = false
  busy.value = true
  const result = await signInWithEmailCode({ challengeId: challenge.value.challengeId, code: code.value, returnUrl: returnUrl.value })
  if (result.ok) {
    clearChallenge()
    // Stays in the verifying state until the browser leaves.
    goTo(result.data.redirectUrl)
    return
  }
  busy.value = false
  const { failure } = result
  if (failure.code === 'identity.sign_in_code_invalid' || failure.status === 400) {
    codeError.value = failure.message ?? failure.details[0]?.message ?? ''
    code.value = ''
    await nextTick()
    return codeRef.value?.focus()
  }
  if (failure.code === 'identity.sign_in_code_expired') return showEnded('expired', failure.message ?? '')
  if (failure.code === 'identity.sign_in_challenge_ended') return showEnded('ended', failure.message ?? '')
  if (failure.status === 423) {
    mode.value = 'locked'
    alert.value = { variant: 'warning', title: t('auth.errors.identity.account_locked.title'), message: failure.message ?? '' }
    await nextTick()
    return otherWayRef.value?.$el.focus()
  }
  if (failure.status === 429) {
    alert.value = { variant: 'warning', title: t('auth.errors.common.rate_limited.title'), message: failure.message ?? '' }
    return submitCooldown.start(failure.retryAfter ?? 60)
  }
  alert.value = { variant: 'error', title: t('auth.errors.network'), message: '', code: failure.code }
}

async function resend() {
  if (!challenge.value || resendBusy.value || resendSeconds.value > 0) return
  resendBusy.value = true
  const result = await requestCode(challenge.value.email)
  resendBusy.value = false
  if (result.ok) {
    const next = { ...result.data, email: challenge.value.email, returnUrl: challenge.value.returnUrl }
    saveChallenge(next)
    challenge.value = next
    now.value = Date.now()
    code.value = ''
    codeError.value = ''
    alert.value = null
    mode.value = 'entry'
    resent.value = true
    await nextTick()
    return codeRef.value?.focus()
  }
  const { failure } = result
  if (failure.status === 429) {
    // Restart the resend countdown from Retry-After, with no alert.
    const wait = (failure.retryAfter ?? 60) * 1000
    challenge.value = { ...challenge.value, resendAvailableAt: new Date(Date.now() + wait).toISOString() }
    now.value = Date.now()
    return
  }
  alert.value = { variant: 'error', title: t('auth.errors.network'), message: '', code: failure.code }
}

function onCodeChange(value: string) {
  code.value = value
  if (codeError.value && value) codeError.value = ''
}

function primary() {
  if (ended.value) void resend()
  else void submit()
}
</script>

<template>
  <AuthPage v-if="challenge" :title="$t('auth.code.entry.title')">
    <template #subtitle>
      <i18n-t keypath="auth.code.entry.subtitle" tag="span">
        <template #email><strong class="font-semibold text-text-primary" data-testid="code-email">{{ challenge.email }}</strong></template>
      </i18n-t>
      {{ ' ' }}
      <RouterLink
        :to="{ ...withReturnUrl('/signin/code'), state: { email: challenge.email } }"
        class="text-text-link underline underline-offset-2 hover:text-text-link-hover"
      >
        {{ $t('auth.code.entry.changeEmail') }}
      </RouterLink>
    </template>
    <form class="space-y-4" novalidate :aria-busy="busy" @submit.prevent="primary">
      <Alert v-if="alert" :variant="alert.variant" :title="alert.title" :message="alert.message || undefined" data-testid="code-alert">
        <template v-if="mode === 'locked'" #action>
          <RouterLink
            ref="otherWayRef"
            :to="withReturnUrl('/signin')"
            class="text-text-link underline underline-offset-2 hover:text-text-link-hover"
            data-testid="other-way"
          >
            {{ $t('auth.code.entry.otherWay') }}
          </RouterLink>
        </template>
      </Alert>
      <p v-if="alert?.code" class="text-xs text-text-muted">{{ alert.code }}</p>
      <CodeInput
        ref="codeRef"
        :model-value="code"
        :legend="$t('auth.code.entry.legend')"
        :digit-label="$t('auth.code.entry.digit', { index: '{index}' })"
        :error="codeError || undefined"
        :hint="mode === 'entry' ? $t('auth.code.entry.expiresIn', { time: formatClock(expirySeconds) }) : undefined"
        :disabled="busy || mode !== 'entry'"
        @update:model-value="onCodeChange"
        @complete="submit"
      />
      <Button
        type="submit"
        size="lg"
        class="w-full"
        :loading="busy || (ended && resendBusy)"
        :disabled="mode === 'locked' || (ended ? resendSeconds > 0 : submitCooldown.remaining.value > 0)"
        data-testid="code-primary"
      >
        {{ primaryLabel }}
      </Button>
      <p class="sr-only" aria-live="polite">{{ announcement }}</p>
    </form>
    <div class="mt-4 flex flex-wrap items-center gap-x-1 text-sm text-text-secondary">
      <span>{{ $t('auth.code.entry.notReceived') }}</span>
      <Button variant="ghost" size="sm" :disabled="busy || resendBusy || resendSeconds > 0" data-testid="code-resend" @click="resend">
        {{ resendLabel }}
      </Button>
    </div>
    <p v-if="resent" role="status" class="mt-2 flex items-center gap-1.5 text-sm text-feedback-success-fg" data-testid="code-resent">
      <CircleCheck class="size-4 shrink-0" aria-hidden="true" />{{ $t('auth.code.entry.resent') }}
    </p>
    <i18n-t keypath="auth.code.entry.help" tag="p" class="mt-6 text-sm text-text-muted">
      <template #link>
        <RouterLink :to="withReturnUrl('/signin')" class="text-text-link underline underline-offset-2 hover:text-text-link-hover">
          {{ $t('auth.code.entry.helpLink') }}
        </RouterLink>
      </template>
    </i18n-t>
  </AuthPage>
</template>
