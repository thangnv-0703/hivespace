import { setLocale, type Locale } from '@hivespace/core'
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { saveLanguage } from './api'
import { useProfileSession } from './sessionPort'
import { notify } from './toasts'

/**
 * One behavior for the header switcher and the language card (ui-design D-C, FR-LOC-002, UC-027 3 and 3a): the UI switches
 * at once; signed in, the choice is then saved to the profile and the session refreshed; a visitor's choice stays in the browser.
 */
export function useLanguagePreference() {
  const i18n = useI18n()
  const session = useProfileSession()
  const busy = ref(false)

  async function change(value: string): Promise<void> {
    setLocale(i18n.locale, value as Locale)
    if (!session.isSignedIn()) return

    busy.value = true
    const result = await saveLanguage(value as Locale)
    if (result.ok) {
      await session.refresh()
    } else if (result.failure.status === 401) {
      session.markSignedOut()
    } else {
      // The new language stays for this visit and this browser; only the profile did not get it.
      notify({ variant: 'error', title: result.failure.message ?? i18n.t('profile.toasts.languageFailed') })
    }
    busy.value = false
  }

  return { locale: i18n.locale, busy, change }
}
