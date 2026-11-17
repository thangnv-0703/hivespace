import { createI18n } from 'vue-i18n'

export type Locale = 'vi' | 'en'
export const SUPPORTED_LOCALES: readonly Locale[] = ['vi', 'en']
/** Vietnamese is the default and the fallback (FR-LOC-001, FR-LOC-008). */
export const DEFAULT_LOCALE: Locale = 'vi'
const STORAGE_KEY = 'hs-locale'

/**
 * Saved choice first (FR-LOC-002, signed-out users keep it in the browser), else English only when the
 * browser prefers English, else Vietnamese (FR-LOC-003). The profile-backed choice arrives with FEAT-002.
 */
export function detectLocale(saved: string | null, browserLanguages: readonly string[]): Locale {
  if (saved === 'vi' || saved === 'en') return saved
  return browserLanguages[0]?.toLowerCase().startsWith('en') ? 'en' : DEFAULT_LOCALE
}

export function createHiveI18n<M extends Record<Locale, object>>(messages: M) {
  let saved: string | null = null
  try {
    saved = globalThis.localStorage?.getItem(STORAGE_KEY) ?? null
  } catch {
    saved = null
  }
  const locale = detectLocale(saved, globalThis.navigator?.languages ?? [])
  if (globalThis.document) globalThis.document.documentElement.lang = locale
  // vue-i18n's message typing is stricter than plain JSON imports; the parity check guards the shape.
  return createI18n({ legacy: false, locale, fallbackLocale: DEFAULT_LOCALE, messages: messages as never })
}

/** Switches the UI at once and remembers the choice in the browser (FR-LOC-002). */
export function setLocale(active: { value: string }, locale: Locale): void {
  active.value = locale
  try {
    globalThis.localStorage?.setItem(STORAGE_KEY, locale)
  } catch {
    // storage blocked: the switch still applies for this visit
  }
  if (globalThis.document) globalThis.document.documentElement.lang = locale
}
