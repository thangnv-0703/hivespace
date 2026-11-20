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

  async send(method: string, route: string, payload?: unknown, language?: string): Promise<void> {
    const headers: Record<string, string> = {}
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
}

setWorldConstructor(ContractWorld)
