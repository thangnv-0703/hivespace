// The only file in this feature that imports the generated clients (dev-standards §8.4). It adds the CSRF header and
// Accept-Language to every call, like the auth feature does for its own calls (technical-design D-2, D-5).
import { changeMyPassword, getMyProfile, setMyPassword, updateMyLanguage, updateMyProfile, type PasswordChange, type PasswordSet, type Profile, type ProfileUpdate } from '@hivespace/api-client/identity'
import { client as identityClient } from '@hivespace/api-client/identity/client'

identityClient.setConfig({ baseUrl: globalThis.location?.origin ?? '', headers: { 'X-HS-CSRF': '1' } })

export type { Profile, ProfileUpdate }

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

interface Result<T> {
  data?: T
  error?: { code?: string; message?: string; details?: FieldProblem[] }
  response?: Response
}

function language(): 'vi' | 'en' {
  return globalThis.document?.documentElement.lang === 'en' ? 'en' : 'vi'
}

const headers = () => ({ 'Accept-Language': language() })

function retryAfterOf(response: Response | undefined): number | undefined {
  const seconds = Number(response?.headers.get('Retry-After'))
  return Number.isFinite(seconds) && seconds > 0 ? Math.ceil(seconds) : undefined
}

async function run<T>(call: () => Promise<Result<T>>): Promise<Outcome<T>> {
  let result: Result<T>
  try {
    result = await call()
  } catch {
    return { ok: false, failure: { status: 0, code: 'network', details: [] } }
  }
  if (result.error || !result.response?.ok) {
    const error = result.error
    return {
      ok: false,
      failure: {
        status: result.response?.status ?? 0,
        code: error?.code ?? 'common.internal',
        message: error?.message,
        details: error?.details ?? [],
        retryAfter: retryAfterOf(result.response),
      },
    }
  }
  return { ok: true, data: result.data as T }
}

export const loadProfile = () => run(() => getMyProfile({ headers: headers() }))

export const saveProfile = (body: ProfileUpdate) => run(() => updateMyProfile({ body, headers: headers() }))

export const saveLanguage = (value: 'vi' | 'en') => run(() => updateMyLanguage({ body: { language: value }, headers: headers() }))

/** The server returns 204; the caller then refreshes the session (technical-design D-3). Never retried by the UI (D-1). */
export const changePassword = (body: PasswordChange) => run(() => changeMyPassword({ body, headers: headers() }))

/** A first password for an account without one, after a recent Google sign-in (FR-AUTH-027); 204, then the caller refreshes the session. */
export const setPassword = (body: PasswordSet) => run(() => setMyPassword({ body, headers: headers() }))
