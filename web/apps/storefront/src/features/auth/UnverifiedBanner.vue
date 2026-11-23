<script setup lang="ts">
import { TriangleAlert, X } from '@lucide/vue'
import { computed, ref } from 'vue'

import { resend } from './api'
import { useSessionStore } from './session'
import { useCooldown } from '@hivespace/core'

// Strip under the header for signed-in users whose email is not verified (FR-AUTH-008: browsing is not blocked).
const KEY = 'hs-banner-dismissed'
const session = useSessionStore()
const { remaining, start } = useCooldown(60)
const sent = ref(false)

function wasDismissed(): boolean {
  try {
    return sessionStorage.getItem(KEY) === '1'
  } catch {
    return false
  }
}
const dismissed = ref(wasDismissed())
const visible = computed(() => session.status === 'authenticated' && session.user?.emailVerified === false && !dismissed.value)

function dismiss() {
  dismissed.value = true
  try {
    sessionStorage.setItem(KEY, '1')
  } catch {
    // per-tab convenience only
  }
}

async function resendLink() {
  if (!session.user) return
  const result = await resend(session.user.email)
  if (!result.ok) return
  start()
  sent.value = true
  setTimeout(() => (sent.value = false), 5000)
}
</script>

<template>
  <!-- Single-row warning strip (mockup UnverifiedBanner): icon, message with the email in bold, resend link, 44 px dismiss. -->
  <div
    v-if="visible"
    role="status"
    class="flex items-center gap-3 border-b border-feedback-warning-icon bg-feedback-warning-bg px-4 py-3 text-sm text-feedback-warning-fg md:px-6"
    data-testid="unverified-banner"
  >
    <TriangleAlert class="size-5 shrink-0 text-feedback-warning-icon" aria-hidden="true" />
    <i18n-t keypath="auth.banner.message" tag="p" class="min-w-0 flex-1">
      <template #email>
        <strong>{{ session.user?.email ?? '' }}</strong>
      </template>
    </i18n-t>
    <span v-if="sent" class="whitespace-nowrap font-semibold">{{ $t('auth.banner.sentAgain') }}</span>
    <button
      v-else
      type="button"
      class="whitespace-nowrap font-semibold underline underline-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
      :disabled="remaining > 0"
      @click="resendLink"
    >
      {{ $t('auth.banner.resend') }}<template v-if="remaining > 0"> ({{ remaining }})</template>
    </button>
    <button
      type="button"
      class="-my-3 -mr-3 inline-flex size-11 shrink-0 items-center justify-center rounded-lg hover:bg-black/5 focus-visible:outline-2 focus-visible:outline-focus-ring"
      :aria-label="$t('auth.banner.dismiss')"
      @click="dismiss"
    >
      <X class="size-5" aria-hidden="true" />
    </button>
  </div>
</template>
