import { RegisterPage, SignInPage, VerifyEmailPage } from '@/features/auth'
import { HomePage } from '@/features/home'
import NotFoundPage from '@/shared/pages/NotFoundPage.vue'
import { createRouter, createWebHistory } from 'vue-router'

declare module 'vue-router' {
  interface RouteMeta {
    /** Full-screen pages (auth) are rendered without the storefront header (ui-design "Shared frame"). */
    fullscreen?: boolean
  }
}

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'home', component: HomePage },
    { path: '/signin', name: 'signin', component: SignInPage, meta: { fullscreen: true } },
    { path: '/register', name: 'register', component: RegisterPage, meta: { fullscreen: true } },
    { path: '/verify-email', name: 'verify-email', component: VerifyEmailPage, meta: { fullscreen: true } },
    { path: '/:pathMatch(.*)*', name: 'not-found', component: NotFoundPage },
  ],
})
