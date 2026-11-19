<script setup lang="ts">
import { X } from '@lucide/vue'
import { Alert, Button } from '@hivespace/ui'
import { computed, ref } from 'vue'

import { resend } from './api'
import { useSessionStore } from './session'
import { useCooldown } from './useCooldown'

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
  <div v-if="visible" class="relative" data-testid="unverified-banner">
    <Alert variant="warning" strip :title="$t('auth.banner.message', { email: session.user?.email ?? '' })">
      <template #action>
        <span v-if="sent" class="font-medium text-text-secondary">{{ $t('auth.banner.sentAgain') }}</span>
        <button
          v-else
          type="button"
          class="font-medium text-text-link underline underline-offset-2 hover:text-text-link-hover disabled:cursor-not-allowed disabled:opacity-50"
          :disabled="remaining > 0"
          @click="resendLink"
        >
          {{ $t('auth.banner.resend') }}<template v-if="remaining > 0"> ({{ remaining }})</template>
        </button>
      </template>
    </Alert>
    <div class="absolute right-2 top-2">
      <Button variant="ghost" size="sm" icon-only :aria-label="$t('auth.banner.dismiss')" :start-icon="X" @click="dismiss" />
    </div>
  </div>
</template>
