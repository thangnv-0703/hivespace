import enAuth from './en/auth.json'
import enCommon from './en/common.json'
import enDashboard from './en/dashboard.json'
import enSettings from './en/settings.json'
import enShop from './en/shop.json'
import viAuth from './vi/auth.json'
import viCommon from './vi/common.json'
import viDashboard from './vi/dashboard.json'
import viSettings from './vi/settings.json'
import viShop from './vi/shop.json'

// Feature-namespaced JSON per locale (repo-structure §8); key parity is checked in CI (NFR-L10N-004).
export const messages = {
  vi: { ...viCommon, ...viAuth, ...viDashboard, ...viShop, ...viSettings },
  en: { ...enCommon, ...enAuth, ...enDashboard, ...enShop, ...enSettings },
}
