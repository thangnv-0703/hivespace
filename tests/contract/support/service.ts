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
  stop(): Promise<void>
}

interface ServiceSpec {
  /** Folder name under services/, project and database names derive from it. */
  dir: string
  project: string
  connectionName: string
  extraEnv(): Record<string, string>
}

// A syntactically valid emulator string pointing nowhere: the outbox relay logs a warning and retries,
// so rows stay unsent and the steps can read them (the .NET factories removed the relay instead).
const deadBroker = 'Endpoint=sb://127.0.0.1:1;SharedAccessKeyName=test;SharedAccessKey=test;UseDevelopmentEmulator=true;'

const specs: Record<string, ServiceSpec> = {
  identity: {
    dir: 'identity',
    project: 'Identity.Api',
    connectionName: 'identitydb',
    extraEnv: () => ({
      Identity__Issuer: 'http://localhost:5080',
      Identity__PublicBaseUrl: 'http://localhost:5080',
      // Fresh keys per run; production reads them from configuration (identity design D-7).
      Identity__SigningKeyPem: generateKeyPairSync('ec', { namedCurve: 'P-256' }).privateKey.export({ type: 'pkcs8', format: 'pem' }).toString(),
      Identity__EncryptionKeyPem: generateKeyPairSync('rsa', { modulusLength: 2048 }).privateKey.export({ type: 'pkcs8', format: 'pem' }).toString(),
      Identity__Gateway__ClientSecret: 'test-client-secret',
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
      ...spec.extraEnv(),
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
    await db.end()
    await postgres.stop()
  }

  try {
    await waitReady(baseUrl, exited)
  } catch (error) {
    await stop()
    throw new Error(`${name} did not become ready: ${String(error)}\n${output.join('')}`)
  }
  return { baseUrl, db, stop }
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
