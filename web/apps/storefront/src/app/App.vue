<script setup lang="ts">
import { useSessionStore } from '@/features/auth'
import StorefrontLayout from '@/shared/layouts/StorefrontLayout.vue'
import { onMounted } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const session = useSessionStore()

// One session probe per page load (GET /bff/user); a failure counts as signed out.
onMounted(() => void session.load())
</script>

<template>
  <RouterView v-if="route.meta.fullscreen" />
  <StorefrontLayout v-else>
    <RouterView />
  </StorefrontLayout>
</template>
