import type { ToastData } from '@hivespace/ui'
import { ref } from 'vue'

/** The seller centre's toast stack (design.md §4.3), rendered once by `ToastHost` in the app. */
export const toasts = ref<ToastData[]>([])

let counter = 0

export function notify(toast: Omit<ToastData, 'id'>): void {
  toasts.value = [...toasts.value, { ...toast, id: `toast-${++counter}` }]
}

export function dismiss(id: string): void {
  toasts.value = toasts.value.filter((toast) => toast.id !== id)
}
