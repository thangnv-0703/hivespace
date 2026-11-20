import { ref } from 'vue'
import type { ToastData } from '@hivespace/ui'

/** The storefront's toast stack (design.md §4.3), rendered once by `ProfileToasts` in the shell. */
export const toasts = ref<ToastData[]>([])

let counter = 0

export function notify(toast: Omit<ToastData, 'id'>): void {
  toasts.value = [...toasts.value, { ...toast, id: `toast-${++counter}` }]
}

export function dismiss(id: string): void {
  toasts.value = toasts.value.filter((toast) => toast.id !== id)
}
