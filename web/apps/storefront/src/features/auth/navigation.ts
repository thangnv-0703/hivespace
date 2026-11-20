/** A full-page navigation. Isolated so tests can observe it: Identity's redirect targets are not SPA routes. */
export function goTo(url: string): void {
  globalThis.location.assign(url)
}

/** Moves focus to the page's h1 after a view switch (ui-design "Focus management"). */
export function focusTitle(): void {
  globalThis.document?.getElementById('auth-title')?.focus()
}
