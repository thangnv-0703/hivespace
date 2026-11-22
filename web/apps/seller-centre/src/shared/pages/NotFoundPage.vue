<script setup lang="ts">
import { ErrorPage } from '@hivespace/ui'
</script>

<template>
  <ErrorPage
    code="404"
    :title="$t('errors.notFound.title')"
    :message="$t('errors.notFound.message')"
    :action-label="$t('errors.backDashboard')"
    action-href="/"
  />
</template>
