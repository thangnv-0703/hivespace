import { spawn, execFileSync, type ChildProcess } from 'node:child_process'
import { generateKeyPairSync } from 'node:crypto'
import net from 'node:net'
import path from 'node:path'
import { PostgreSqlContainer, type StartedPostgreSqlContainer } from '@testcontainers/postgresql'
import pg from 'pg'

const repoRoot = path.resolve(import.meta.dirname, '..', '..', '..')

export interface RunningService {
  baseUrl: string
  /** Direct database access for the steps that seed or read state the API does not expose. */
  db: pg.Pool
  /** PEM of the ES256 key Identity signs tokens with (only for identity), so a step can re-sign a token with another audience. */
  signingKeyPem?: string
  /** The fake IdP standing in for Google (only for identity, FEAT-004 D-7). */
  fakeIdpUrl?: string
  stop(): Promise<void>
}

interface ServiceSpec {
  /** Folder name under services/, project and database names derive from it. */
  dir: string
  project: string
  connectionName: string
  extraEnv(): Record<string, string>
  /** A helper process started before the service; its URL is passed to extraEnv. */
  fakeIdp?: boolean
}

// A syntactically valid emulator string pointing nowhere: the outbox relay logs a warning and retries,
// so rows stay unsent and the steps can read them (the .NET factories removed the relay instead).
const deadBroker = 'Endpoint=sb://127.0.0.1:1;SharedAccessKeyName=test;SharedAccessKey=test;UseDevelopmentEmulator=true;'

const specs: Record<string, ServiceSpec> = {
  identity: {
    dir: 'identity',
    project: 'Identity.Api',
    connectionName: 'identitydb',
    fakeIdp: true,
    extraEnv: () => ({
      Identity__Issuer: 'http://localhost:5080',
      // Two OIDC clients, one per app host (ADR-0027).
      Identity__Clients__Storefront__BaseUrl: 'http://localhost:5080',
      Identity__Clients__Seller__BaseUrl: 'http://seller.localhost:5080',
      // Fresh keys per run; production reads them from configuration (identity design D-7).
      Identity__SigningKeyPem: generateKeyPairSync('ec', { namedCurve: 'P-256' }).privateKey.export({ type: 'pkcs8', format: 'pem' }).toString(),
      Identity__EncryptionKeyPem: generateKeyPairSync('rsa', { modulusLength: 2048 }).privateKey.export({ type: 'pkcs8', format: 'pem' }).toString(),
      Identity__Clients__Storefront__ClientSecret: 'test-client-secret',
      Identity__Clients__Seller__ClientSecret: 'test-seller-secret',
      // Google sign-in against the fake IdP; Identity__Google__Authority is added once it is listening.
      Identity__Google__ClientId: fakeGoogle.clientId,
      Identity__Google__ClientSecret: fakeGoogle.clientSecret,
    }),
  },
  catalog: {
    dir: 'catalog',
    project: 'Catalog.Api',
    connectionName: 'catalogdb',
    // Inventory is not running; Catalog treats a failed probe as "availability unknown".
    extraEnv: () => ({ Inventory__GrpcAddress: 'http://127.0.0.1:1' }),
  },
}

/** The Google client the fake IdP accepts, and the public callback Identity registers with it (FEAT-004 §3.2). */
export const fakeGoogle = {
  clientId: 'hivespace-google-test',
  clientSecret: 'google-test-secret', // pragma: allowlist secret - test value
  redirectUri: 'http://localhost:5080/connect/external/google/callback',
}

const running = new Map<string, Promise<RunningService>>()

export function getService(name: string): Promise<RunningService> {
  let started = running.get(name)
  if (!started) {
    started = start(name)
    running.set(name, started)
  }
  return started
}

export async function stopAll(): Promise<void> {
  const all = [...running.values()]
  running.clear()
  await Promise.all(all.map(async (s) => (await s).stop()))
}

async function freePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = net.createServer()
    server.once('error', reject)
    server.listen(0, '127.0.0.1', () => {
      const { port } = server.address() as net.AddressInfo
      server.close(() => resolve(port))
    })
  })
}

function connectionString(c: StartedPostgreSqlContainer): string {
  return `Host=${c.getHost()};Port=${c.getPort()};Database=${c.getDatabase()};Username=${c.getUsername()};Password=${c.getPassword()}`
}

async function start(name: string): Promise<RunningService> {
  const spec = specs[name]
  if (!spec) throw new Error(`No contract-test service named ${name}`)

  const projectDir = path.join(repoRoot, 'services', spec.dir, 'src', spec.project)
  // Build into our own folder: a running AppHost keeps the service's normal bin/ locked on Windows.
  const outDir = path.join(import.meta.dirname, '..', '.build', spec.dir)
  execFileSync('dotnet', ['build', path.join(projectDir, `${spec.project}.csproj`), '-o', outDir, '-v', 'q', '--nologo'], { stdio: 'inherit' })

  const extraEnv = spec.extraEnv()
  const idp = spec.fakeIdp ? await startFakeIdp() : undefined
  if (idp) extraEnv.Identity__Google__Authority = idp.url
  const postgres = await new PostgreSqlContainer('pgvector/pgvector:pg17').start()
  const db = new pg.Pool({ connectionString: postgres.getConnectionUri() })
  const port = await freePort()
  const baseUrl = `http://127.0.0.1:${port}`

  const output: string[] = []
  const child: ChildProcess = spawn('dotnet', [path.join(outDir, `${spec.project}.dll`)], {
    cwd: projectDir,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      ASPNETCORE_URLS: baseUrl,
      [`ConnectionStrings__${spec.connectionName}`]: connectionString(postgres),
      ConnectionStrings__messaging: deadBroker,
      ...extraEnv,
    },
  })
  for (const stream of [child.stdout, child.stderr]) {
    stream?.on('data', (chunk: Buffer) => {
      output.push(chunk.toString())
      if (output.length > 200) output.shift()
    })
  }
  const exited = new Promise<number | null>((resolve) => child.once('exit', resolve))

  const stop = async () => {
    child.kill()
    await Promise.race([exited, new Promise((r) => setTimeout(r, 5000))])
    await idp?.stop()
    await db.end()
    await postgres.stop()
  }

  try {
    await waitReady(baseUrl, exited)
  } catch (error) {
    await stop()
    throw new Error(`${name} did not become ready: ${String(error)}\n${output.join('')}`)
  }
  return { baseUrl, db, stop, signingKeyPem: extraEnv.Identity__SigningKeyPem, fakeIdpUrl: idp?.url }
}

/** Builds and starts tools/fake-idp on a free loopback port, accepting only the test Google client. */
async function startFakeIdp(): Promise<{ url: string; stop(): Promise<void> }> {
  const projectDir = path.join(repoRoot, 'tools', 'fake-idp', 'src', 'FakeIdp')
  const outDir = path.join(import.meta.dirname, '..', '.build', 'fake-idp')
  execFileSync('dotnet', ['build', path.join(projectDir, 'FakeIdp.csproj'), '-o', outDir, '-v', 'q', '--nologo'], { stdio: 'inherit' })
  const url = `http://127.0.0.1:${await freePort()}`
  const child = spawn('dotnet', [path.join(outDir, 'FakeIdp.dll')], {
    cwd: projectDir,
    env: {
      ...process.env,
      ASPNETCORE_URLS: url,
      FakeIdp__ClientId: fakeGoogle.clientId,
      FakeIdp__ClientSecret: fakeGoogle.clientSecret,
      FakeIdp__RedirectUris__0: fakeGoogle.redirectUri,
    },
  })
  const exited = new Promise<number | null>((resolve) => child.once('exit', resolve))
  const stop = async () => {
    child.kill()
    await Promise.race([exited, new Promise((r) => setTimeout(r, 5000))])
  }
  const deadline = Date.now() + 60_000
  while (Date.now() < deadline) {
    try {
      if ((await fetch(`${url}/health`)).ok) return { url, stop }
    } catch {
      // not listening yet
    }
    await new Promise((r) => setTimeout(r, 300))
  }
  await stop()
  throw new Error('fake-idp did not start')
}

async function waitReady(baseUrl: string, exited: Promise<number | null>): Promise<void> {
  let hasExited = false
  void exited.then(() => { hasExited = true })
  const deadline = Date.now() + 120_000
  while (Date.now() < deadline) {
    if (hasExited) throw new Error('process exited')
    try {
      const response = await fetch(`${baseUrl}/ready`)
      if (response.ok) return
    } catch {
      // not listening yet
    }
    await new Promise((r) => setTimeout(r, 500))
  }
  throw new Error('timed out after 120 s')
}
