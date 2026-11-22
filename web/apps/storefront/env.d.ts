/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Origin of the seller centre app, which lives on another host (ADR-0027). */
  readonly VITE_SELLER_ORIGIN?: string
}
