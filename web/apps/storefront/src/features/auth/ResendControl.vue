<script setup lang="ts">
import { Alert, Button, Input } from '@hivespace/ui'
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { resend } from './api'
import { useCooldown } from '@hivespace/core'
import { EMAIL_PATTERN } from './validation'

// "Resend email" with the 60 s client-side cooldown (ui-design, Register notice and Verify expired/invalid).
// Known email: just the button. No email: asks for one first. The answer is the same for any address (no enumeration).
const props = defineProps<{ email?: string; newLink?: boolean; initialEmail?: string }>()

const { t } = useI18n()
const typed = ref(props.initialEmail ?? '')
const emailError = ref('')
const sentTo = ref('')
const failed = ref(false)
const busy = ref(false)
const { remaining, start } = useCooldown(60)

const target = computed(() => props.email ?? typed.value.trim())
const label = computed(() => {
  const key = props.newLink ? 'sendNewLink' : 'resend'
  return remaining.value > 0 ? t(`auth.actions.${key}Wait`, { seconds: remaining.value }) : t(`auth.actions.${key}`)
})

async function send() {
  failed.value = false
  if (!props.email && !EMAIL_PATTERN.test(target.value)) {
    emailError.value = target.value ? t('auth.errors.emailInvalid') : t('auth.errors.emailRequired')
    return
  }
  emailError.value = ''
  busy.value = true
  const result = await resend(target.value)
  busy.value = false
  if (result.ok) {
    sentTo.value = target.value
    start()
  } else {
    failed.value = true
  }
}
</script>

<template>
  <div class="space-y-3">
    <Input
      v-if="!email"
      v-model="typed"
      type="email"
      autocomplete="email"
      inputmode="email"
      :label="$t('auth.verify.newLinkEmail')"
      :error="emailError"
    />
    <Alert v-if="failed" variant="error" :title="$t('auth.errors.network')" />
    <Button variant="secondary" class="w-full" :loading="busy" :disabled="remaining > 0" @click="send">{{ label }}</Button>
    <p v-if="sentTo" role="status" class="text-sm text-text-muted">
      {{ email ? $t('auth.register.sentAgain', { email: sentTo }) : $t('auth.verify.sentAgain') }}
    </p>
  </div>
</template>
