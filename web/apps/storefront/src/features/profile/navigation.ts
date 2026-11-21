/** A full-page navigation. Isolated so tests can observe it: the sign-in start is a gateway URL, not an SPA route. */
export function goTo(url: string): void {
  globalThis.location.assign(url)
}
