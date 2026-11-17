import enCommon from './en/common.json'
import viCommon from './vi/common.json'

// Feature-namespaced JSON per locale (repo-structure §8); key parity is checked in CI (NFR-L10N-004).
export const messages = {
  vi: viCommon,
  en: enCommon,
}
