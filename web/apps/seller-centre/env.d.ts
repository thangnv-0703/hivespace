/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Origin of the storefront app, which lives on another host (ADR-0027). */
  readonly VITE_STOREFRONT_ORIGIN?: string
}
