// The only file in this feature that imports the generated clients (dev-standards §8.4). It adds the CSRF header and
// Accept-Language to every call, like the auth feature does for its own calls (technical-design D-2, D-5).
import { getMyProfile, updateMyLanguage, updateMyProfile, type Profile, type ProfileUpdate } from '@hivespace/api-client/identity'
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
      },
    }
  }
  return { ok: true, data: result.data as T }
}

export const loadProfile = () => run(() => getMyProfile({ headers: headers() }))

export const saveProfile = (body: ProfileUpdate) => run(() => updateMyProfile({ body, headers: headers() }))

export const saveLanguage = (value: 'vi' | 'en') => run(() => updateMyLanguage({ body: { language: value }, headers: headers() }))
