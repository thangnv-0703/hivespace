<script setup lang="ts">
// SCR-SEL-002: the seller's dashboard. This slice draws the setup checklist only; order counts arrive with FEAT-025.
import { Alert, Badge, Button, Card, PageHeader } from '@hivespace/ui'
import { Circle, CircleCheck } from '@lucide/vue'
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'

import { loadMyShop, type MyShop } from './api'
import { useDashboardSession } from './sessionPort'

const { t } = useI18n()
const router = useRouter()
const session = useDashboardSession()

const shop = ref<MyShop | null>(null)
const loading = ref(true)
const error = ref('')

const rows = computed(() => [
  { key: 'pickupAddress', done: shop.value?.setup.pickupAddress ?? false },
  { key: 'payout', done: shop.value?.setup.payout ?? false },
])
const doneCount = computed(() => rows.value.filter((row) => row.done).length)
const allDone = computed(() => doneCount.value === rows.value.length)

async function load() {
  loading.value = true
  error.value = ''
  const result = await loadMyShop()
  if (result.ok) {
    shop.value = result.shop
  } else if (result.status === 401) {
    session.markSignedOut()
    globalThis.location.assign('/bff/login?returnUrl=%2F')
  } else if (result.status === 403) {
    // The role is gone: refresh the claims and let the route guard reroute (design: SCR-SEL-002 error).
    await session.refresh()
    await router.replace('/')
  } else {
    error.value = result.message ?? t('dashboard.errors.load')
  }
  loading.value = false
}

onMounted(load)
</script>

<template>
  <PageHeader
    :title="t('dashboard.title')"
    :nav-label="t('common.breadcrumb')"
    :crumbs="[{ label: t('common.home'), to: '/' }, { label: t('dashboard.title') }]"
  />

  <div v-if="loading" class="space-y-4" aria-busy="true" data-testid="dashboard-loading">
    <div class="h-5 w-52 animate-pulse rounded bg-bg-subtle" />
    <div class="space-y-3 rounded-xl border border-border-default bg-bg-surface p-6">
      <div class="h-6 w-48 animate-pulse rounded bg-bg-subtle" />
      <div class="h-14 animate-pulse rounded bg-bg-subtle" />
      <div class="h-14 animate-pulse rounded bg-bg-subtle" />
    </div>
  </div>

  <Alert v-else-if="error" variant="error" :title="error">
    <template #action>
      <Button variant="secondary" size="sm" data-testid="retry" @click="load">{{ $t('dashboard.retry') }}</Button>
    </template>
  </Alert>

  <template v-else-if="shop">
    <p class="mb-4 text-sm text-text-muted" data-testid="shop-subtitle">
      {{ shop.name }} · {{ $t(`dashboard.sellerType.${shop.sellerType}`) }}
    </p>

    <Card>
      <div class="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
        <h2 class="text-lg font-semibold text-text-primary">{{ $t('dashboard.setup.title') }}</h2>
        <p class="text-sm text-text-muted" data-testid="setup-progress">{{ $t('dashboard.setup.progress', { done: doneCount, total: rows.length }) }}</p>
      </div>
      <p :class="['mt-1 text-sm', allDone ? 'text-feedback-success-fg' : 'text-text-secondary']">
        {{ allDone ? $t('dashboard.setup.ready') : $t('dashboard.setup.intro') }}
      </p>

      <ul class="mt-4">
        <li
          v-for="row in rows"
          :key="row.key"
          class="flex min-h-14 flex-col gap-3 border-b border-border-default py-4 last:border-b-0 sm:flex-row sm:items-center"
          :data-testid="`setup-${row.key}`"
        >
          <div class="flex flex-1 items-start gap-3">
            <CircleCheck v-if="row.done" class="mt-0.5 size-5 shrink-0 text-feedback-success-icon" aria-hidden="true" />
            <Circle v-else class="mt-0.5 size-5 shrink-0 text-text-muted" aria-hidden="true" />
            <div class="min-w-0">
              <p class="flex items-center gap-2 text-sm font-medium text-text-primary">
                {{ $t(`dashboard.setup.${row.key}.title`) }}
                <Badge v-if="row.done" tone="success" size="sm">{{ $t('dashboard.setup.done') }}</Badge>
              </p>
              <p class="text-sm text-text-muted">{{ $t(`dashboard.setup.${row.key}.description`) }}</p>
            </div>
          </div>
          <Button v-if="!row.done" variant="secondary" size="sm" class="w-full sm:w-auto" href="/settings/pickup-payout">
            {{ $t(`dashboard.setup.${row.key}.action`) }}
          </Button>
        </li>
      </ul>
    </Card>
  </template>
</template>
