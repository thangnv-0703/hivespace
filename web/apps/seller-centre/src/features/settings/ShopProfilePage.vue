<script setup lang="ts">
// SCR-SEL-007: the shop's name and description (FR-SHOP-005). Seller type is shown, never edited.
import { Alert, Button, Card, Input, PageHeader, TextArea } from '@hivespace/ui'
import { nextTick, onMounted, reactive, ref, useTemplateRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'

import { saveProfile } from './api'
import { useSettingsSession } from './sessionPort'
import { useShopLoad } from './useShopLoad'

const NAME_MIN = 3
const NAME_MAX = 60
const DESCRIPTION_MAX = 1000

const { t } = useI18n()
const session = useSettingsSession()
const { shop, loading, error, load } = useShopLoad('/settings/profile', () => t('settings.errors.load'))

const form = reactive({ name: '', description: '' })
const errors = reactive({ name: '', description: '' })
const saving = ref(false)
const failed = ref(false)
const card = useTemplateRef<HTMLElement>('card')

watch(shop, (value) => {
  if (value) {
    form.name = value.name
    form.description = value.description ?? ''
  }
})

onMounted(load)

function checkName(): boolean {
  const trimmed = form.name.trim().replace(/\s+/g, ' ')
  errors.name = !trimmed ? t('settings.errors.nameRequired') : trimmed.length < NAME_MIN ? t('settings.errors.nameTooShort') : ''
  return !errors.name
}

async function focusFirstInvalid() {
  await nextTick()
  card.value?.querySelector<HTMLElement>('[aria-invalid=true]')?.focus()
}

async function submit() {
  failed.value = false
  errors.description = ''
  if (!checkName()) return focusFirstInvalid()

  saving.value = true
  const result = await saveProfile({ name: form.name, description: form.description })
  saving.value = false
  if (result.ok) {
    shop.value = result.data
    errors.name = ''
    session.notify({ variant: 'success', title: t('settings.profile.saved') })
    return
  }

  const { failure } = result
  if (failure.status === 401) {
    session.markSignedOut()
  } else if (failure.code === 'identity.shop_name_taken') {
    errors.name = failure.message ?? t('settings.errors.nameTaken')
  } else if (failure.status === 400 && failure.details.length > 0) {
    for (const detail of failure.details) {
      if (detail.field === 'name') errors.name = detail.message ?? t('settings.errors.nameInvalid')
      else if (detail.field === 'description') errors.description = detail.message ?? t('settings.errors.descriptionInvalid')
    }
  } else {
    failed.value = true
    return
  }
  await focusFirstInvalid()
}
</script>

<template>
  <PageHeader
    :title="$t('settings.profile.title')"
    :nav-label="$t('common.breadcrumb')"
    :crumbs="[{ label: $t('common.home'), to: '/' }, { label: $t('settings.title') }, { label: $t('settings.profile.title') }]"
  />

  <Card v-if="loading" class="max-w-3xl" aria-busy="true" data-testid="settings-loading">
    <div class="space-y-4">
      <div class="h-6 w-40 animate-pulse rounded bg-bg-subtle" />
      <div class="h-11 animate-pulse rounded bg-bg-subtle" />
      <div class="h-30 animate-pulse rounded bg-bg-subtle" />
      <div class="h-5 w-32 animate-pulse rounded bg-bg-subtle" />
    </div>
  </Card>

  <Alert v-else-if="error" variant="error" :title="error" class="max-w-3xl">
    <template #action>
      <Button variant="secondary" size="sm" data-testid="retry" @click="load">{{ $t('settings.retry') }}</Button>
    </template>
  </Alert>

  <div v-else-if="shop" class="max-w-3xl space-y-4">
    <Alert v-if="failed" variant="error" :title="$t('settings.profile.failed')" :message="$t('settings.supportHint')" />

    <Card :title="$t('settings.profile.card')">
      <form ref="card" class="space-y-5" novalidate @submit.prevent="submit">
        <Input
          v-model="form.name"
          :label="$t('settings.profile.name')"
          :hint="$t('settings.profile.nameHint')"
          :error="errors.name"
          :disabled="saving"
          :maxlength="NAME_MAX"
          autocomplete="off"
          required
          @blur="form.name && checkName()"
        />

        <div>
          <TextArea
            v-model="form.description"
            :label="$t('settings.profile.description')"
            :error="errors.description"
            :hint="$t('settings.profile.descriptionHint')"
            :disabled="saving"
            :rows="5"
            :maxlength="DESCRIPTION_MAX"
          />
          <p class="mt-1 text-right text-xs text-text-muted" data-testid="description-count">
            {{ $t('settings.profile.count', { count: form.description.length, max: DESCRIPTION_MAX }) }}
          </p>
        </div>

        <div>
          <p class="text-sm font-medium text-text-secondary">{{ $t('settings.profile.sellerType') }}</p>
          <p class="text-sm text-text-primary" data-testid="seller-type">{{ $t(`settings.sellerType.${shop.sellerType}`) }}</p>
          <p class="text-xs text-text-muted">{{ $t('settings.profile.sellerTypeHint') }}</p>
        </div>

        <div class="flex justify-end border-t border-border-default pt-4">
          <Button type="submit" class="w-full sm:w-auto" :loading="saving">{{ $t('settings.profile.save') }}</Button>
        </div>
      </form>
    </Card>
  </div>
</template>
