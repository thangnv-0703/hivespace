<script setup lang="ts">
// SCR-SEL-008: the pickup address and the payout bank details, each saved on its own (FR-SHOP-006, FR-SHOP-007, technical-design D-3).
// The account number is typed, sent and cleared; only its last four digits ever come back (D-4, NFR-SEC-008).
import { Alert, Badge, Button, Card, Input, PageHeader } from '@hivespace/ui'
import { computed, nextTick, onMounted, reactive, ref, useTemplateRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'

import { saveAddress, savePayout, type FieldProblem, type MyShop } from './api'
import { useSettingsSession } from './sessionPort'
import { useShopLoad } from './useShopLoad'

const PHONE = /^(?:0|\+84)[235789]\d{8}$/

const { t } = useI18n()
const session = useSettingsSession()
const { shop, loading, error, load } = useShopLoad('/settings/pickup-payout', () => t('settings.errors.load'))

const addressFields = ['contactName', 'phone', 'province', 'district', 'ward', 'street'] as const
const payoutFields = ['bankName', 'accountNumber', 'accountHolder'] as const
type AddressField = (typeof addressFields)[number]
type PayoutField = (typeof payoutFields)[number]

const address = reactive<Record<AddressField, string>>({ contactName: '', phone: '', province: '', district: '', ward: '', street: '' })
const payout = reactive<Record<PayoutField, string>>({ bankName: '', accountNumber: '', accountHolder: '' })
const addressErrors = reactive<Record<AddressField, string>>({ contactName: '', phone: '', province: '', district: '', ward: '', street: '' })
const payoutErrors = reactive<Record<PayoutField, string>>({ bankName: '', accountNumber: '', accountHolder: '' })
const savingAddress = ref(false)
const savingPayout = ref(false)
const addressFailed = ref(false)
const payoutFailed = ref(false)
const changing = ref(false)
const addressCard = useTemplateRef<HTMLElement>('addressCard')
const payoutCard = useTemplateRef<HTMLElement>('payoutCard')

const showPayoutForm = computed(() => !shop.value?.payout || changing.value)
const addressMessages = computed(() => addressFields.map((field) => addressErrors[field]).filter(Boolean))
const payoutMessages = computed(() => payoutFields.map((field) => payoutErrors[field]).filter(Boolean))
const last4Spoken = computed(() => (shop.value?.payout?.accountNumberLast4 ?? '').split('').join(' '))

function fillAddress(value: MyShop) {
  for (const field of addressFields) address[field] = value.pickupAddress?.[field] ?? ''
}

function fillPayoutDefaults(value: MyShop) {
  payout.bankName = value.payout?.bankName ?? ''
  payout.accountHolder = value.payout?.accountHolder ?? ''
  payout.accountNumber = ''
}

watch(shop, (value) => {
  if (value) {
    fillAddress(value)
    if (!changing.value) fillPayoutDefaults(value)
  }
})

onMounted(load)

const label = (key: string) => t(`settings.pickup.fields.${key}`).toLocaleLowerCase()
const required = (key: string) => t('settings.errors.required', { field: label(key) })

function checkAddress(): boolean {
  for (const field of addressFields) {
    const value = address[field].trim()
    addressErrors[field] = !value ? required(field) : field === 'phone' && !PHONE.test(value.replace(/[ .-]/g, '')) ? t('settings.errors.phone') : ''
  }
  return addressMessages.value.length === 0
}

function checkPayout(): boolean {
  for (const field of payoutFields) {
    const value = payout[field].trim()
    if (!value) payoutErrors[field] = required(field)
    else if (field === 'accountNumber') payoutErrors[field] = /^\d{6,19}$/.test(value.replace(/[ -]/g, '')) ? '' : t('settings.errors.accountNumber')
    else payoutErrors[field] = ''
  }
  return payoutMessages.value.length === 0
}

async function focusFirstInvalid(card: HTMLElement | null) {
  await nextTick()
  card?.querySelector<HTMLElement>('[aria-invalid=true]')?.focus()
}

function applyDetails<K extends string>(details: FieldProblem[], errors: Record<K, string>, fields: readonly K[]) {
  for (const detail of details) {
    if ((fields as readonly string[]).includes(detail.field)) errors[detail.field as K] = detail.message ?? t('settings.errors.invalid')
  }
}

/** Both toasts of §SCR-SEL-008: the second save that completes the checklist says the shop is ready. */
function announce(before: MyShop, after: MyShop, saved: string) {
  const becameReady = !(before.setup.pickupAddress && before.setup.payout) && after.setup.pickupAddress && after.setup.payout
  session.notify({ variant: 'success', title: becameReady ? t('settings.ready') : saved })
}

async function submitAddress() {
  addressFailed.value = false
  if (!checkAddress()) return focusFirstInvalid(addressCard.value)

  const before = shop.value as MyShop
  savingAddress.value = true
  const result = await saveAddress({ ...address })
  savingAddress.value = false
  if (result.ok) {
    announce(before, result.data, t('settings.pickup.saved'))
    shop.value = result.data
    return
  }
  if (result.failure.status === 401) {
    session.markSignedOut()
  } else if (result.failure.status === 400 && result.failure.details.length > 0) {
    applyDetails(result.failure.details, addressErrors, addressFields)
    return focusFirstInvalid(addressCard.value)
  } else {
    addressFailed.value = true
  }
}

async function submitPayout() {
  payoutFailed.value = false
  if (!checkPayout()) return focusFirstInvalid(payoutCard.value)

  const before = shop.value as MyShop
  savingPayout.value = true
  const result = await savePayout({ ...payout })
  savingPayout.value = false
  if (result.ok) {
    // The typed number is dropped as soon as the save is acknowledged; only the last four digits come back.
    payout.accountNumber = ''
    changing.value = false
    announce(before, result.data, t('settings.payout.saved'))
    shop.value = result.data
    return
  }
  if (result.failure.status === 401) {
    session.markSignedOut()
  } else if (result.failure.status === 400 && result.failure.details.length > 0) {
    applyDetails(result.failure.details, payoutErrors, payoutFields)
    return focusFirstInvalid(payoutCard.value)
  } else {
    // The number stays in the form: the person has not left it.
    payoutFailed.value = true
  }
}

function startChange() {
  if (shop.value) fillPayoutDefaults(shop.value)
  for (const field of payoutFields) payoutErrors[field] = ''
  changing.value = true
}

function cancelChange() {
  payout.accountNumber = ''
  changing.value = false
}
</script>

<template>
  <PageHeader
    :title="$t('settings.pickupPayout.title')"
    :nav-label="$t('common.breadcrumb')"
    :crumbs="[{ label: $t('common.home'), to: '/' }, { label: $t('settings.title') }, { label: $t('settings.pickupPayout.crumb') }]"
  />
  <p class="-mt-4 mb-6 text-sm text-text-muted">{{ $t('settings.pickupPayout.subtitle') }}</p>

  <div v-if="loading" class="max-w-3xl space-y-6" aria-busy="true" data-testid="settings-loading">
    <Card v-for="n in 2" :key="n">
      <div class="space-y-3">
        <div class="h-6 w-44 animate-pulse rounded bg-bg-subtle" />
        <div class="h-11 animate-pulse rounded bg-bg-subtle" />
        <div class="h-11 animate-pulse rounded bg-bg-subtle" />
        <div class="h-11 animate-pulse rounded bg-bg-subtle" />
      </div>
    </Card>
  </div>

  <Alert v-else-if="error" variant="error" :title="error" class="max-w-3xl">
    <template #action>
      <Button variant="secondary" size="sm" data-testid="retry" @click="load">{{ $t('settings.retry') }}</Button>
    </template>
  </Alert>

  <div v-else-if="shop" class="max-w-3xl space-y-6">
    <Card data-testid="address-card">
      <template #header>
        <div class="flex items-center justify-between gap-3">
          <h2 class="text-md font-medium text-text-primary">{{ $t('settings.pickup.title') }}</h2>
          <Badge :tone="shop.setup.pickupAddress ? 'success' : 'warning'" data-testid="address-badge">
            {{ shop.setup.pickupAddress ? $t('settings.saved') : $t('settings.missing') }}
          </Badge>
        </div>
      </template>
      <form ref="addressCard" class="space-y-5" novalidate @submit.prevent="submitAddress">
        <p class="text-sm text-text-secondary">{{ $t('settings.pickup.intro') }}</p>
        <Alert v-if="addressFailed" variant="error" :title="$t('settings.pickup.failed')" :message="$t('settings.supportHint')" />
        <Alert v-if="addressMessages.length >= 3" variant="error" :title="$t('settings.errors.summary')" data-testid="address-summary">
          <template #action>
            <ul class="list-disc pl-5">
              <li v-for="message in addressMessages" :key="message">{{ message }}</li>
            </ul>
          </template>
        </Alert>

        <div class="grid gap-5 md:grid-cols-2">
          <Input v-model="address.contactName" :label="$t('settings.pickup.fields.contactName')" :error="addressErrors.contactName" :disabled="savingAddress" :maxlength="80" autocomplete="name" required />
          <Input
            v-model="address.phone"
            type="tel"
            inputmode="tel"
            :label="$t('settings.pickup.fields.phone')"
            :hint="$t('settings.pickup.phoneHint')"
            :error="addressErrors.phone"
            :disabled="savingAddress"
            autocomplete="tel"
            required
          />
        </div>
        <div class="grid gap-5 lg:grid-cols-3">
          <Input v-model="address.province" :label="$t('settings.pickup.fields.province')" :error="addressErrors.province" :disabled="savingAddress" :maxlength="60" required />
          <Input v-model="address.district" :label="$t('settings.pickup.fields.district')" :error="addressErrors.district" :disabled="savingAddress" :maxlength="60" required />
          <Input v-model="address.ward" :label="$t('settings.pickup.fields.ward')" :error="addressErrors.ward" :disabled="savingAddress" :maxlength="60" required />
        </div>
        <Input
          v-model="address.street"
          :label="$t('settings.pickup.fields.street')"
          :hint="$t('settings.pickup.streetHint')"
          :error="addressErrors.street"
          :disabled="savingAddress"
          :maxlength="200"
          autocomplete="street-address"
          required
        />

        <div class="flex justify-end border-t border-border-default pt-4">
          <Button type="submit" class="w-full sm:w-auto" :loading="savingAddress">{{ $t('settings.pickup.save') }}</Button>
        </div>
      </form>
    </Card>

    <Card data-testid="payout-card">
      <template #header>
        <div class="flex items-center justify-between gap-3">
          <h2 class="text-md font-medium text-text-primary">{{ $t('settings.payout.title') }}</h2>
          <Badge :tone="shop.setup.payout ? 'success' : 'warning'" data-testid="payout-badge">
            {{ shop.setup.payout ? $t('settings.saved') : $t('settings.missing') }}
          </Badge>
        </div>
      </template>

      <form v-if="showPayoutForm" ref="payoutCard" class="space-y-5" novalidate @submit.prevent="submitPayout">
        <p class="text-sm text-text-secondary">{{ $t('settings.payout.intro') }}</p>
        <Alert v-if="payoutFailed" variant="error" :title="$t('settings.payout.failed')" :message="$t('settings.supportHint')" />
        <Alert v-if="payoutMessages.length >= 3" variant="error" :title="$t('settings.errors.summary')" data-testid="payout-summary">
          <template #action>
            <ul class="list-disc pl-5">
              <li v-for="message in payoutMessages" :key="message">{{ message }}</li>
            </ul>
          </template>
        </Alert>

        <Input
          v-model="payout.bankName"
          :label="$t('settings.payout.fields.bankName')"
          :placeholder="$t('settings.payout.bankPlaceholder')"
          :error="payoutErrors.bankName"
          :disabled="savingPayout"
          :maxlength="80"
          required
        />
        <div class="grid gap-5 md:grid-cols-2">
          <Input
            v-model="payout.accountNumber"
            inputmode="numeric"
            :label="$t('settings.payout.fields.accountNumber')"
            :hint="$t('settings.payout.accountNumberHint')"
            :error="payoutErrors.accountNumber"
            :disabled="savingPayout"
            :maxlength="23"
            autocomplete="off"
            spellcheck="false"
            required
          />
          <Input
            v-model="payout.accountHolder"
            :label="$t('settings.payout.fields.accountHolder')"
            :hint="$t('settings.payout.accountHolderHint')"
            :error="payoutErrors.accountHolder"
            :disabled="savingPayout"
            :maxlength="80"
            autocomplete="off"
            required
          />
        </div>

        <div class="flex flex-col-reverse justify-end gap-3 border-t border-border-default pt-4 sm:flex-row">
          <Button v-if="changing" variant="secondary" class="w-full sm:w-auto" :disabled="savingPayout" data-testid="cancel-change" @click="cancelChange">
            {{ $t('settings.payout.cancel') }}
          </Button>
          <Button type="submit" class="w-full sm:w-auto" :loading="savingPayout">{{ $t('settings.payout.save') }}</Button>
        </div>
      </form>

      <div v-else class="space-y-5">
        <p class="text-sm text-text-secondary">{{ $t('settings.payout.intro') }}</p>
        <dl class="space-y-3 text-sm" data-testid="payout-summary-list">
          <div class="flex flex-col gap-1 sm:flex-row sm:gap-4">
            <dt class="text-text-muted sm:w-40">{{ $t('settings.payout.summary.bank') }}</dt>
            <dd class="text-text-primary">{{ shop.payout?.bankName }}</dd>
          </div>
          <div class="flex flex-col gap-1 sm:flex-row sm:gap-4">
            <dt class="text-text-muted sm:w-40">{{ $t('settings.payout.summary.accountNumber') }}</dt>
            <dd class="tabular-nums text-text-primary" data-testid="masked-number">
              <span aria-hidden="true">•••• {{ shop.payout?.accountNumberLast4 }}</span>
              <span class="sr-only">{{ $t('settings.payout.summary.ending', { digits: last4Spoken }) }}</span>
            </dd>
          </div>
          <div class="flex flex-col gap-1 sm:flex-row sm:gap-4">
            <dt class="text-text-muted sm:w-40">{{ $t('settings.payout.summary.holder') }}</dt>
            <dd class="text-text-primary">{{ shop.payout?.accountHolder }}</dd>
          </div>
        </dl>
        <div class="flex justify-end border-t border-border-default pt-4">
          <Button variant="secondary" class="w-full sm:w-auto" data-testid="change-payout" @click="startChange">{{ $t('settings.payout.change') }}</Button>
        </div>
      </div>
    </Card>
  </div>
</template>
