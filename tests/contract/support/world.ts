import { setWorldConstructor, World } from '@cucumber/cucumber'
import type { RunningService } from './service.ts'

/** Per-scenario state shared by the steps of every service. */
export class ContractWorld extends World {
  service!: RunningService
  response?: Response
  body: any = undefined
  token?: string
  earlierToken?: string
  email = 'an@example.com'
  verificationEvents = 0
  /** The signed-in user's access token and id, set by the profile steps. */
  accessToken?: string
  userId?: string
  /** The sign-in code challenge in play and the code emailed for it (FEAT-006), plus the one before it. */
  challengeId?: string
  code?: string
  earlierChallengeId?: string
  earlierCode?: string

  async send(method: string, route: string, payload?: unknown, language?: string, bearer?: string | null): Promise<void> {
    const headers: Record<string, string> = {}
    const token = bearer === undefined ? this.accessToken : bearer
    if (token) headers['authorization'] = `Bearer ${token}`
    if (payload !== undefined) headers['content-type'] = 'application/json'
    if (language) headers['accept-language'] = language
    this.response = await fetch(this.service.baseUrl + route, {
      method,
      headers,
      body: payload === undefined ? undefined : JSON.stringify(payload),
      redirect: 'manual',
    })
    const text = await this.response.text()
    this.body = text.length > 0 ? JSON.parse(text) : undefined
  }

  post(route: string, payload: unknown, language?: string) {
    return this.send('POST', route, payload, language)
  }

  get(route: string, language?: string, bearer?: string | null) {
    return this.send('GET', route, undefined, language, bearer)
  }

  put(route: string, payload: unknown, language?: string) {
    return this.send('PUT', route, payload, language)
  }
}

setWorldConstructor(ContractWorld)
