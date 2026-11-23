<script setup lang="ts">
import { Button, GoogleIcon } from '@hivespace/ui'
import { ref } from 'vue'

import { goTo } from './navigation'

// SCR-STF-012 / SCR-STF-013 (FEAT-004 ui-design): "Continue with Google" and the "or" divider above the form. A full-page
// navigation to Identity, not an API call (technical-design §5.5); returnUrl is the page's own value, unchanged.
const props = defineProps<{ returnUrl?: string; disabled?: boolean }>()
const emit = defineEmits<{ start: [] }>()

const loading = ref(false)

function start() {
  loading.value = true
  emit('start')
  const query = props.returnUrl ? `?returnUrl=${encodeURIComponent(props.returnUrl)}` : ''
  // Stays loading until the browser leaves: no flash of the form.
  goTo(`/connect/external/google/start${query}`)
}
</script>

<template>
  <div>
    <Button
      variant="secondary"
      size="lg"
      class="w-full gap-3"
      :start-icon="GoogleIcon"
      :loading="loading"
      :disabled="disabled"
      data-testid="google-button"
      @click="start"
    >
      {{ $t('auth.google.continue') }}
    </Button>
    <div class="my-6 flex items-center gap-3" role="separator">
      <span class="h-px flex-1 border-t border-border-default" />
      <span class="text-xs text-text-muted">{{ $t('auth.google.or') }}</span>
      <span class="h-px flex-1 border-t border-border-default" />
    </div>
  </div>
</template>
