<script setup lang="ts">
import BrandLogo from './BrandLogo.vue'
import authBrand from './assets/auth/auth-hex.svg'
import gridShape from './assets/shape/grid-01.svg'

/**
 * Full-screen frame for sign-in, register and verify pages (technical-design D-1, ui-design "Shared frame").
 * Presentational only: text arrives as props (or the `subtitle` slot, for a subtitle with a link), the form comes in the default slot, and the corner controls
 * (language, theme) come in the `corner` slot, so this component needs no i18n, router or store.
 */
withDefaults(
  defineProps<{
    title: string
    subtitle?: string
    imageSrc?: string
    imageAlt?: string
    imageHeading?: string
    imageBody?: string
    /** Accessible name of the logo link. */
    homeLabel?: string
    skipLabel?: string
    homeHref?: string
  }>(),
  {
    subtitle: undefined,
    imageSrc: authBrand,
    imageAlt: '',
    imageHeading: undefined,
    imageBody: undefined,
    homeLabel: 'HiveSpace',
    skipLabel: 'Skip to form',
    homeHref: '/',
  },
)
</script>

<template>
  <div class="min-h-screen bg-bg-surface text-text-primary">
    <a
      href="#auth-main"
      class="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-[var(--z-sticky)] focus:rounded-lg focus:bg-bg-surface focus:px-4 focus:py-2 focus:text-sm focus:text-text-link"
    >
      {{ skipLabel }}
    </a>
    <div class="flex min-h-screen flex-col lg:flex-row">
      <main id="auth-main" class="relative flex w-full flex-1 items-center justify-center px-4 py-10 sm:px-6 lg:w-1/2">
        <div class="absolute right-4 top-4 z-[var(--z-raised)] flex items-center gap-2"><slot name="corner" /></div>
        <div class="w-full max-w-md">
          <a :href="homeHref" class="mb-8 inline-flex rounded focus-visible:outline-2 focus-visible:outline-focus-ring" :aria-label="homeLabel">
            <BrandLogo :label="homeLabel" />
          </a>
          <div class="mb-8">
            <h1 id="auth-title" tabindex="-1" class="mb-2 text-xl font-semibold text-text-primary focus:outline-none sm:text-title-sm">
              {{ title }}
            </h1>
            <p v-if="subtitle || $slots.subtitle" class="text-sm text-text-muted"><slot name="subtitle">{{ subtitle }}</slot></p>
          </div>
          <slot />
        </div>
      </main>
      <aside class="relative hidden w-1/2 items-center justify-center overflow-hidden bg-brand-950 lg:flex" data-testid="auth-panel">
        <img :src="gridShape" alt="" aria-hidden="true" class="pointer-events-none absolute inset-0 size-full object-cover opacity-35" />
        <div class="relative z-[var(--z-raised)] flex max-w-md flex-col items-center px-12 py-16 text-center">
          <img :src="imageSrc" :alt="imageAlt" class="mb-8 w-65" />
          <h2 v-if="imageHeading" class="mb-3 text-title-sm font-semibold text-text-on-brand">{{ imageHeading }}</h2>
          <p v-if="imageBody" class="text-md text-text-on-brand opacity-70">{{ imageBody }}</p>
        </div>
      </aside>
    </div>
  </div>
</template>
