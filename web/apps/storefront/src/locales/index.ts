import enAuth from './en/auth.json'
import enCommon from './en/common.json'
import viAuth from './vi/auth.json'
import viCommon from './vi/common.json'

// Feature-namespaced JSON per locale (repo-structure §8); key parity is checked in CI (NFR-L10N-004).
export const messages = {
  vi: { ...viCommon, ...viAuth },
  en: { ...enCommon, ...enAuth },
}
