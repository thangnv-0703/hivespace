// The only file in this feature that imports the generated clients (dev-standards §8.4). It is also the one place
// that adds the CSRF header and Accept-Language to identity and gateway calls (FEAT-009 technical-design §5.5).
import { getSession, refreshSession } from '@hivespace/api-client/gateway'
import { client as gatewayClient } from '@hivespace/api-client/gateway/client'
import { updateMyLanguage } from '@hivespace/api-client/identity'
import { client as identityClient } from '@hivespace/api-client/identity/client'

// Same origin as the page: the gateway serves the SPA and the API on this host (ADR-0027). Every call carries the CSRF header.
for (const client of [identityClient, gatewayClient]) {
  client.setConfig({ baseUrl: globalThis.location?.origin ?? '', headers: { 'X-HS-CSRF': '1' } })
}

export interface SessionUser {
  userId: string
  email: string
  displayName: string
  emailVerified: boolean
  roles: string[]
  language: 'vi' | 'en'
}

/** `status` 0 means the request never got an answer (network failure). */
export interface Failure {
  status: number
  code: string
  message?: string
}

export type Outcome = { ok: true } | { ok: false; failure: Failure }

function toSessionUser(data: Awaited<ReturnType<typeof getSession>>['data']): SessionUser | null {
  if (!data?.isAuthenticated || !data.userId) return null
  return {
    userId: data.userId,
    email: data.email ?? '',
    displayName: data.displayName ?? '',
    emailVerified: data.emailVerified ?? false,
    roles: data.roles ?? [],
    language: data.language ?? 'vi',
  }
}

/** The session probe: a failure is treated as signed out. */
export async function fetchSession(): Promise<SessionUser | null> {
  try {
    return toSessionUser((await getSession()).data)
  } catch {
    return null
  }
}

/** Forces a token refresh so the roles, name and language are current, for example right after a shop is registered (D-2). */
export async function refreshSessionUser(): Promise<SessionUser | null> {
  try {
    return toSessionUser((await refreshSession({ headers: { 'X-HS-CSRF': '1' } })).data)
  } catch {
    return null
  }
}

/** Saves the preferred language to the profile (FR-LOC-002). */
export async function saveLanguage(language: 'vi' | 'en'): Promise<Outcome> {
  const accept = globalThis.document?.documentElement.lang === 'en' ? 'en' : 'vi'
  try {
    const result = await updateMyLanguage({ body: { language }, headers: { 'Accept-Language': accept } })
    if (result.response?.ok) return { ok: true }
    const error = result.error as { code?: string; message?: string } | undefined
    return {
      ok: false,
      failure: { status: result.response?.status ?? 0, code: error?.code ?? 'common.internal_error', message: error?.message },
    }
  } catch {
    return { ok: false, failure: { status: 0, code: 'network' } }
  }
}
