import './main.css'

import { createHiveI18n } from '@hivespace/core'
import { applyInitialTheme } from '@hivespace/ui'
import { createPinia } from 'pinia'
import { createApp } from 'vue'

import { messages } from '../locales'
import App from './App.vue'
import { router } from './router'

applyInitialTheme()

createApp(App).use(createPinia()).use(router).use(createHiveI18n(messages)).mount('#app')
