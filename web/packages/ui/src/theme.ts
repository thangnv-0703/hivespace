const STORAGE_KEY = 'hs-theme'

/** Class-based dark mode (ADR-0021): saved choice, else prefers-color-scheme. Theme persists in the browser only. */
export function applyInitialTheme(): void {
  let saved: string | null = null
  try {
    saved = localStorage.getItem(STORAGE_KEY)
  } catch {
    saved = null
  }
  const dark = saved ? saved === 'dark' : matchMedia('(prefers-color-scheme: dark)').matches
  document.documentElement.classList.toggle('dark', dark)
}

export function setTheme(dark: boolean): void {
  document.documentElement.classList.toggle('dark', dark)
  try {
    localStorage.setItem(STORAGE_KEY, dark ? 'dark' : 'light')
  } catch {
    // storage blocked: the switch still applies for this visit
  }
}
