<script setup lang="ts">
// SCR-SEL-001: register a shop (FR-SHOP-001). Rendered inside OnboardingLayout (AuthLayout), which supplies the frame.
import { useCooldown } from '@hivespace/core'
import { Alert, Button, Input, RadioGroup, Spinner } from '@hivespace/ui'
import { computed, nextTick, onMounted, reactive, ref, useTemplateRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'

import { register, resend, type SellerType } from './api'
import { useShopSession } from './sessionPort'

const NAME_MIN = 3
const NAME_MAX = 60

const { t } = useI18n()
const router = useRouter()
const session = useShopSession()

const name = ref('')
const sellerType = ref<SellerType | null>(null)
const errors = reactive({ name: '', sellerType: '' })
const submitting = ref(false)
const starting = ref(false)
const failed = ref(false)
const stillUnverified = ref(false)
const forcedUnverified = ref(false)
const resendSent = ref(false)
const resendMessage = ref('')
const nameInput = useTemplateRef<{ focus: () => void }>('nameInput')
const typeGroup = useTemplateRef<HTMLElement>('typeGroup')
const { remaining, start } = useCooldown(60)

const unverified = computed(() => forcedUnverified.value || !session.emailVerified())
const typeOptions = computed(() => [
  { label: t('shop.register.type.individual'), value: 'individual' },
  { label: t('shop.register.type.business'), value: 'business' },
])
const resendLabel = computed(() => (remaining.value > 0 ? t('shop.unverified.resendWait', { seconds: remaining.value }) : t('shop.unverified.resend')))

onMounted(async () => {
  // A token issued before the email was verified still says "unverified": refresh once before showing the warning (D-3).
  if (!session.emailVerified()) {
    starting.value = true
    await session.refresh()
    starting.value = false
  }
  await nextTick()
  if (!unverified.value) nameInput.value?.focus()
})

function checkName(): boolean {
  const trimmed = name.value.trim().replace(/\s+/g, ' ')
  errors.name = !trimmed ? t('shop.errors.nameRequired') : trimmed.length < NAME_MIN ? t('shop.errors.nameTooShort') : ''
  return !errors.name
}

function checkType(): boolean {
  errors.sellerType = sellerType.value ? '' : t('shop.errors.typeRequired')
  return !errors.sellerType
}

async function submit() {
  failed.value = false
  const nameOk = checkName()
  const typeOk = checkType()
  if (!nameOk) return nameInput.value?.focus()
  if (!typeOk) return typeGroup.value?.querySelector<HTMLElement>('input')?.focus()

  submitting.value = true
  const result = await register({ name: name.value, sellerType: sellerType.value as SellerType })
  if (result.ok) return finish(true)

  const { failure } = result
  if (failure.status === 401) {
    session.markSignedOut()
  } else if (failure.code === 'identity.shop_already_owned') {
    // The user already has a shop (another tab): treat it as success (D-5).
    return finish(false)
  } else if (failure.code === 'identity.email_not_verified') {
    forcedUnverified.value = true
  } else if (failure.code === 'identity.shop_name_taken') {
    errors.name = failure.message ?? t('shop.errors.nameTaken')
    nameInput.value?.focus()
  } else if (failure.status === 400 && failure.details.length > 0) {
    for (const detail of failure.details) {
      if (detail.field === 'name') errors.name = detail.message ?? t('shop.errors.nameInvalid')
      else if (detail.field === 'sellerType') errors.sellerType = detail.message ?? t('shop.errors.typeRequired')
    }
  } else {
    failed.value = true
  }
  submitting.value = false
}

// The refresh comes first so the dashboard's seller-only call carries the new role (D-2).
async function finish(announce: boolean) {
  await session.refresh()
  await router.replace('/')
  if (announce) session.notify({ variant: 'success', title: t('shop.toasts.opened') })
}

async function sendAgain() {
  resendMessage.value = ''
  const result = await resend(session.email())
  if (result.ok) {
    resendSent.value = true
    resendMessage.value = t('shop.unverified.sent')
    start()
  } else {
    resendSent.value = false
    resendMessage.value = result.failure.message ?? t('shop.errors.network')
    if (result.failure.retryAfter) start(result.failure.retryAfter)
  }
}

async function recheck() {
  await session.refresh()
  forcedUnverified.value = false
  stillUnverified.value = !session.emailVerified()
}
</script>

<template>
  <div v-if="starting" class="flex justify-center py-12" data-testid="starting">
    <Spinner size="md" :label="$t('shop.register.loading')" />
  </div>

  <div v-else class="space-y-5">
    <Alert
      v-if="unverified"
      variant="warning"
      :title="$t('shop.unverified.title')"
      :message="$t('shop.unverified.body', { email: session.email() }) + (stillUnverified ? ' ' + $t('shop.unverified.still') : '')"
    >
      <template #action>
        <div class="flex flex-wrap items-center gap-2">
          <Button variant="secondary" size="sm" :disabled="remaining > 0" data-testid="resend" @click="sendAgain">{{ resendLabel }}</Button>
          <Button variant="ghost" size="sm" data-testid="recheck" @click="recheck">{{ $t('shop.unverified.recheck') }}</Button>
        </div>
        <p v-if="resendMessage" role="status" class="mt-2 text-text-muted">{{ resendMessage }}</p>
      </template>
    </Alert>

    <Alert v-if="failed" variant="error" :title="$t('shop.errors.submit')" />

    <form class="space-y-5" novalidate @submit.prevent="submit">
      <Input
        ref="nameInput"
        v-model="name"
        :label="$t('shop.register.name')"
        :hint="$t('shop.register.nameHint')"
        :error="errors.name"
        :disabled="unverified || submitting"
        :maxlength="NAME_MAX"
        autocomplete="off"
        required
        @blur="name && checkName()"
      />

      <div ref="typeGroup" class="space-y-1.5">
        <p class="text-sm font-medium text-text-primary">{{ $t('shop.register.type.label') }}</p>
        <RadioGroup
          v-model="sellerType"
          :label="$t('shop.register.type.label')"
          :options="typeOptions.map((option) => ({ ...option, disabled: unverified || submitting }))"
          @update:model-value="errors.sellerType = ''"
        />
        <p v-if="errors.sellerType" role="alert" class="text-sm text-feedback-error-fg">{{ errors.sellerType }}</p>
        <p v-else class="text-sm text-text-muted">{{ $t('shop.register.type.hint') }}</p>
      </div>

      <Button type="submit" class="w-full" :loading="submitting" :disabled="unverified">{{ $t('shop.register.submit') }}</Button>
      <p class="text-xs text-text-muted">{{ $t('shop.register.next') }}</p>
    </form>
  </div>
</template>
