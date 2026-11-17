<script setup lang="ts">
// Walking skeleton: the header's category bar is the one UI call that crosses gateway → Catalog → Postgres.
// Loading, empty and error states per NFR-USE-002. FEAT-014/FEAT-018 make it the real category tree.
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { fetchCategories, type Category } from './api'

const { locale } = useI18n()
const categories = ref<Category[]>([])
const state = ref<'loading' | 'ready' | 'error'>('loading')

async function load() {
  state.value = 'loading'
  try {
    categories.value = await fetchCategories()
    state.value = 'ready'
  } catch {
    state.value = 'error'
  }
}

onMounted(load)
</script>

<template>
  <nav :aria-label="$t('catalog.categories')" class="border-t border-border-default bg-bg-surface">
    <div class="mx-auto flex max-w-[var(--size-container)] items-center gap-4 overflow-x-auto px-4 py-2 text-sm">
      <span v-if="state === 'loading'" class="text-text-muted">{{ $t('common.loading') }}</span>
      <span v-else-if="state === 'error'" class="text-feedback-error-fg">
        {{ $t('catalog.loadFailed') }}
        <button type="button" class="ml-2 underline" @click="load">{{ $t('common.retry') }}</button>
      </span>
      <span v-else-if="categories.length === 0" class="text-text-muted">{{ $t('catalog.empty') }}</span>
      <template v-else>
        <span
          v-for="category in categories"
          :key="category.id"
          class="whitespace-nowrap text-text-secondary"
          data-testid="category"
        >
          {{ locale === 'en' ? category.name.en : category.name.vi }}
        </span>
      </template>
    </div>
  </nav>
</template>
