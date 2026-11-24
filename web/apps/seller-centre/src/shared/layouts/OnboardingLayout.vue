<script setup lang="ts">
// SCR-SEL-001's frame (ui-design U-1): AuthLayout with language, theme and the account menu in the corner.
import { AccountMenu, useLanguagePreference } from '@/features/auth'
import { AuthLayout, LanguageSwitcher, ThemeToggle } from '@hivespace/ui'

import { notify } from '../toasts'

const language = useLanguagePreference((title) => notify({ variant: 'error', title }))
</script>

<template>
  <AuthLayout
    :title="$t('shop.onboarding.title')"
    :subtitle="$t('shop.onboarding.subtitle')"
    :image-heading="$t('shop.onboarding.brandHeading')"
    :image-body="$t('shop.onboarding.brandBody')"
  >
    <template #corner>
      <LanguageSwitcher :model-value="language.locale.value" :label="$t('common.language')" @update:model-value="language.change" />
      <ThemeToggle :label="$t('common.toggleTheme')" />
      <AccountMenu />
    </template>
    <slot />
  </AuthLayout>
</template>
