// The only file in this feature that imports the generated client (dev-standards §8.4).
import { listCategories, type Category } from '@hivespace/api-client/catalog'
import { client } from '@hivespace/api-client/catalog/client'

// Same origin as the page: the gateway serves the SPA and the API (ADR-0008).
client.setConfig({ baseUrl: globalThis.location?.origin ?? '' })

export type { Category }

export async function fetchCategories(): Promise<Category[]> {
  const { data, error } = await listCategories()
  if (error || !data) throw new Error(error?.code ?? 'common.internal')
  return data.items
}
