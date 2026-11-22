import { DashboardPage } from '@/features/dashboard'
import NotFoundPage from '@/shared/pages/NotFoundPage.vue'
import { createRouter, createWebHistory } from 'vue-router'

// History base comes from vite `base` ('/', the seller host's root since ADR-0027).
export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'dashboard', component: DashboardPage },
    { path: '/:pathMatch(.*)*', name: 'not-found', component: NotFoundPage },
  ],
})
