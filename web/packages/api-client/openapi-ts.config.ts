import { defineConfig } from '@hey-api/openapi-ts'

// One client per service spec; add an entry when a slice's UI first calls that service.
// Base URL is relative: the SPAs and the API share one origin through the gateway (ADR-0008).
export default defineConfig([
  {
    input: '../../../contracts/openapi/catalog.yaml',
    output: 'src/catalog',
    plugins: ['@hey-api/client-fetch'],
  },
])
