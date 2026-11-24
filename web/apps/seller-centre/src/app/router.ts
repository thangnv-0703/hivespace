import { DashboardPage } from '@/features/dashboard'
import { RegisterShopPage } from '@/features/shop'
import NotFoundPage from '@/shared/pages/NotFoundPage.vue'
import { createRouter, createWebHistory } from 'vue-router'

import { createSellerGuard } from './guards'

// History base comes from vite `base` ('/', the seller host's root since ADR-0027).
export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'dashboard', component: DashboardPage },
    // Outside the sidebar shell, on AuthLayout (ui-design U-1); `onboarding` is read by App.vue.
    { path: '/register-shop', name: 'register-shop', component: RegisterShopPage, meta: { onboarding: true } },
    { path: '/:pathMatch(.*)*', name: 'not-found', component: NotFoundPage },
  ],
})

router.beforeEach(createSellerGuard())
