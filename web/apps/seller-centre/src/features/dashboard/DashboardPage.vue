<script setup lang="ts">
// SCR-SEL-002 placeholder. STUB: setup checklist and order counts arrive with FEAT-009 and FEAT-025.
import { PageHeader } from '@hivespace/ui'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <PageHeader
    :title="t('dashboard.title')"
    :nav-label="t('common.breadcrumb')"
    :crumbs="[{ label: t('common.home'), to: '/' }, { label: t('dashboard.title') }]"
  />
  <section class="rounded-xl border border-border-default bg-bg-surface shadow-sm">
    <h2 class="border-b border-border-default px-6 py-4 font-medium">{{ t('dashboard.title') }}</h2>
    <p class="p-6 text-text-muted">{{ t('dashboard.subtitle') }}</p>
  </section>
</template>
