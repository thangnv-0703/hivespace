<script setup lang="ts">
import logoDark from './assets/logo/logo-dark.svg'
import logoIcon from './assets/logo/logo-icon.svg'
import logoLight from './assets/logo/logo-light.svg'

/** HiveSpace logo, ported from the reference app: wordmark in light/dark variants, icon only when `compact`. */
withDefaults(defineProps<{ label?: string; compact?: boolean }>(), { label: 'HiveSpace', compact: false })
</script>

<template>
  <span class="inline-flex items-center" data-testid="brand">
    <img v-if="compact" :src="logoIcon" :alt="label" class="h-7 w-auto" />
    <template v-else>
      <img :src="logoLight" :alt="label" class="h-7 w-auto dark:hidden" />
      <img :src="logoDark" :alt="label" class="hidden h-7 w-auto dark:block" />
    </template>
  </span>
</template>
