import { RegisterPage, SignInPage, VerifyEmailPage } from '@/features/auth'
import { HomePage } from '@/features/home'
import { ProfilePage } from '@/features/profile'
import NotFoundPage from '@/shared/pages/NotFoundPage.vue'
import { createRouter, createWebHistory } from 'vue-router'

import { createAuthGuard } from './guards'

declare module 'vue-router' {
  interface RouteMeta {
    /** Full-screen pages (auth) are rendered without the storefront header (ui-design "Shared frame"). */
    fullscreen?: boolean
    /** Needs a signed-in user; otherwise the browser is sent to the gateway sign-in and returns here. */
    requiresAuth?: boolean
  }
}

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'home', component: HomePage },
    { path: '/signin', name: 'signin', component: SignInPage, meta: { fullscreen: true } },
    { path: '/register', name: 'register', component: RegisterPage, meta: { fullscreen: true } },
    { path: '/verify-email', name: 'verify-email', component: VerifyEmailPage, meta: { fullscreen: true } },
    { path: '/profile', name: 'profile', component: ProfilePage, meta: { requiresAuth: true } },
    { path: '/:pathMatch(.*)*', name: 'not-found', component: NotFoundPage },
  ],
})

router.beforeEach(createAuthGuard())
