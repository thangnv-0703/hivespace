import { setLocale, type Locale } from '@hivespace/core'
import { useI18n } from 'vue-i18n'

import { saveLanguage } from './api'
import { useSessionStore } from './session'

/**
 * The header switcher (FR-LOC-002, FEAT-009 AC-14): the UI switches at once; signed in, the choice is then saved to the profile
 * and the session refreshed; a visitor's choice stays in the browser. A failed save keeps the new language for this visit and
 * reports the server message through `onError`.
 */
export function useLanguagePreference(onError: (message: string) => void) {
  const i18n = useI18n()
  const session = useSessionStore()

  async function change(value: string): Promise<void> {
    setLocale(i18n.locale, value as Locale)
    if (session.status !== 'authenticated') return

    const result = await saveLanguage(value as Locale)
    if (result.ok) {
      await session.refresh()
    } else if (result.failure.status === 401) {
      session.markSignedOut()
    } else {
      onError(result.failure.message ?? i18n.t('auth.languageFailed'))
    }
  }

  return { locale: i18n.locale, change }
}
