<script setup lang="ts">
import { ToastContainer } from '@hivespace/ui'

import { dismiss, toasts } from './toasts'
</script>

<template>
  <ToastContainer :toasts="toasts" :close-label="$t('common.close')" @remove="dismiss" />
</template>
