#!/usr/bin/env node
/**
 * Local Docker deployment gate for Sal's CaterSuite.
 *
 *   node scripts/deploy.mjs            build + start + smoke test
 *   node scripts/deploy.mjs --reset    destroy the local database volume first
 *
 * Exit code 0 means the whole stack answered through nginx: the SPA shell, the
 * seeded administrator's login, an authenticated catalogue read, and Swagger.
 * A non-zero exit is a failing gate, not a warning.
 */
import { spawnSync } from 'node:child_process'
import { copyFileSync, existsSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { setTimeout as sleep } from 'node:timers/promises'
import { fileURLToPath } from 'node:url'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
process.chdir(ROOT)

// docker compose reads .env for substitution; the script reads the same file so
// the URLs it smoke-tests are the ports compose actually published.
if (existsSync('.env')) {
  process.loadEnvFile('.env')
}

const WEB_URL =
  process.env.DEPLOY_WEB_URL ?? `http://localhost:${process.env.WEB_HOST_PORT ?? '8080'}`
const API_URL =
  process.env.DEPLOY_API_URL ?? `http://localhost:${process.env.API_HOST_PORT ?? '5150'}`
const DB_PORT = process.env.DB_HOST_PORT ?? '14330'
const TIMEOUT_MS = Number(process.env.DEPLOY_TIMEOUT_MS ?? 240_000)
const RESET = process.argv.includes('--reset')

/** The seed administrator (appsettings.Development.json -> Seed:Admin). */
const SEED_USER = process.env.DEPLOY_SEED_USER ?? 'admin@salscatersuite.local'
const SEED_PASSWORD = process.env.DEPLOY_SEED_PASSWORD ?? 'Admin@12345'

function log(message) {
  console.log(`[deploy] ${message}`)
}

function docker(args) {
  const result = spawnSync('docker', args, { stdio: 'inherit' })
  if (result.error) {
    throw new Error(`cannot run docker: ${result.error.message}`)
  }
  if (result.status !== 0) {
    throw new Error(`docker ${args.join(' ')} exited with code ${result.status}`)
  }
}

async function request(url, init) {
  const response = await fetch(url, init)
  return { status: response.status, body: await response.text() }
}

async function loginThroughNginx() {
  const deadline = Date.now() + TIMEOUT_MS
  let last = 'no attempt made'

  while (Date.now() < deadline) {
    try {
      const response = await request(`${WEB_URL}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username: SEED_USER, password: SEED_PASSWORD }),
      })
      if (response.status === 200) return JSON.parse(response.body)
      last = `${response.status} ${response.body.slice(0, 300)}`
    } catch (error) {
      last = String(error)
    }
    await sleep(3000)
  }

  throw new Error(`login never succeeded through nginx. Last answer: ${last}`)
}

async function main() {
  if (!existsSync('.env')) {
    if (!existsSync('.env.example')) {
      throw new Error('.env is missing and there is no .env.example to copy.')
    }
    copyFileSync('.env.example', '.env')
    log('created .env from .env.example')
  }

  if (RESET) {
    log('--reset: removing the containers AND the database volume')
    docker(['compose', 'down', '-v', '--remove-orphans'])
  }

  log('building and starting the containers (db, api, web)...')
  docker(['compose', 'up', '-d', '--build', '--wait', '--remove-orphans'])

  log('smoke test 1/4: the SPA shell')
  const shell = await request(`${WEB_URL}/`)
  if (shell.status !== 200 || !shell.body.includes('<div id="root">')) {
    throw new Error(`the SPA shell did not render (status ${shell.status})`)
  }

  log('smoke test 2/4: login through nginx -> api -> db (migrations + seed)')
  const auth = await loginThroughNginx()
  const token = auth.token ?? auth.accessToken ?? auth.access_token
  if (!token) {
    throw new Error(`login succeeded but returned no token: ${JSON.stringify(auth).slice(0, 300)}`)
  }

  log('smoke test 3/4: authenticated catalogue read through nginx')
  const tiers = await request(`${WEB_URL}/api/packages?activeOnly=true`, {
    headers: { Authorization: `Bearer ${token}` },
  })
  if (tiers.status !== 200) {
    throw new Error(`GET /api/packages answered ${tiers.status}: ${tiers.body.slice(0, 300)}`)
  }

  log('smoke test 4/4: Swagger on the API origin')
  const swagger = await request(`${API_URL}/swagger/v1/swagger.json`)
  if (swagger.status !== 200) {
    throw new Error(`GET /swagger/v1/swagger.json answered ${swagger.status}`)
  }

  console.log('')
  log('OK - the stack is up and answered every smoke test')
  console.log(`  UI       ${WEB_URL}`)
  console.log(`  API      ${API_URL}`)
  console.log(`  Swagger  ${API_URL}/swagger`)
  console.log(`  DB       localhost:${DB_PORT} (sa / see .env)`)
  console.log('')
  log('stop it with:  docker compose down        (keeps the database)')
  log('reset it with: node scripts/deploy.mjs --reset   (deletes the database)')
}

try {
  await main()
} catch (error) {
  console.error('')
  console.error(`[deploy] FAILED: ${error.message}`)
  try {
    docker(['compose', 'ps'])
    docker(['compose', 'logs', '--tail', '120', 'api'])
  } catch {
    /* the diagnostic itself failed - the original error is already printed */
  }
  process.exit(1)
}
