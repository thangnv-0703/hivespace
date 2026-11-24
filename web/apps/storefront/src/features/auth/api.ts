// The only file in this feature that imports the generated clients (dev-standards §8.4). It is also the one place
// that adds the CSRF header and Accept-Language to identity calls (technical-design D-2, D-5).
import { getSession, refreshSession } from '@hivespace/api-client/gateway'
import { client as gatewayClient } from '@hivespace/api-client/gateway/client'
import {
  registerUser,
  requestSignInCode,
  resendVerificationEmail,
  signIn,
  signInWithCode,
  verifyEmail,
} from '@hivespace/api-client/identity'
import { client as identityClient } from '@hivespace/api-client/identity/client'

// Same origin as the page: the gateway serves the SPA and the API (ADR-0008). Every call carries the CSRF header.
for (const client of [identityClient, gatewayClient]) {
  client.setConfig({ baseUrl: globalThis.location?.origin ?? '', headers: { 'X-HS-CSRF': '1' } })
}

export interface FieldProblem {
  field: string
  code: string
  message?: string
}

/** `status` 0 means the request never got an answer (network failure). */
export interface Failure {
  status: number
  code: string
  message?: string
  details: FieldProblem[]
  /** Seconds from the `Retry-After` header of a `429`, when it carries one (FEAT-005). */
  retryAfter?: number
}

export type Outcome<T> = { ok: true; data: T } | { ok: false; failure: Failure }

export interface SessionUser {
  userId: string
  email: string
  displayName: string
  emailVerified: boolean
  roles: string[]
  language: 'vi' | 'en'
}

const NETWORK: Failure = { status: 0, code: 'network', details: [] }

function language(): 'vi' | 'en' {
  return globalThis.document?.documentElement.lang === 'en' ? 'en' : 'vi'
}

const headers = () => ({ 'Accept-Language': language() })

interface Result<T> {
  data?: T
  error?: { code?: string; message?: string; details?: FieldProblem[] }
  response?: Response
}

function retryAfterOf(response: Response | undefined): number | undefined {
  const seconds = Number(response?.headers.get('Retry-After'))
  return Number.isFinite(seconds) && seconds > 0 ? Math.ceil(seconds) : undefined
}

async function run<T>(call: () => Promise<Result<T>>): Promise<Outcome<T>> {
  let result: Result<T>
  try {
    result = await call()
  } catch {
    return { ok: false, failure: NETWORK }
  }
  if (result.error || !result.response?.ok) {
    const error = result.error
    return {
      ok: false,
      failure: {
        status: result.response?.status ?? 0,
        code: error?.code ?? 'common.internal_error',
        message: error?.message,
        details: error?.details ?? [],
        retryAfter: retryAfterOf(result.response),
      },
    }
  }
  return { ok: true, data: result.data as T }
}

export const register = (input: { email: string; displayName: string; password: string }) =>
  run(() => registerUser({ body: input, headers: headers() }))

export const verify = (token: string) => run(() => verifyEmail({ body: { token }, headers: headers() }))

export const resend = (email: string) => run(() => resendVerificationEmail({ body: { email }, headers: headers() }))

export const signInWithPassword = (input: { email: string; password: string; returnUrl?: string }) =>
  run(() => signIn({ body: input, headers: headers() }))

/** SCR-STF-021: emails a 6-digit code (FEAT-006). The same `201` answers any well-formed email. */
export const requestCode = (email: string) => run(() => requestSignInCode({ body: { email }, headers: headers() }))

/** SCR-STF-022: checks the code and starts the Identity session, like a password sign-in. */
export const signInWithEmailCode = (input: { challengeId: string; code: string; returnUrl?: string }) =>
  run(() => signInWithCode({ body: input, headers: headers() }))

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

/** The session probe: a failure is treated as signed out (ui-design "Header account area"). */
export async function fetchSession(): Promise<SessionUser | null> {
  try {
    return toSessionUser((await getSession()).data)
  } catch {
    return null
  }
}

/**
 * Forces a token refresh so the header name and language reflect a profile change at once (technical-design D-4).
 * Returns the new session user, or null when the session is gone.
 */
export async function refreshSessionUser(): Promise<SessionUser | null> {
  try {
    return toSessionUser((await refreshSession({ headers: { 'X-HS-CSRF': '1' } })).data)
  } catch {
    return null
  }
}
