#!/usr/bin/env node
/**
 * Development orchestrator — runs the .NET API and the React UI in one terminal.
 *
 * Why this exists instead of the usual `concurrently` setup:
 *
 *   On Windows, a process tree is *not* torn down when its parent dies, and a
 *   grandchild keeps the inherited stdout pipe open. `concurrently` therefore
 *   never observes a child exiting (the `close` event waits for stdio EOF that
 *   the surviving grandchild holds), so `--kill-others-on-fail` and `--teardown`
 *   never fire. The practical symptom is an orphaned API still holding port
 *   5149, which makes the *next* `npm run dev` fail with
 *   "Failed to bind to address http://127.0.0.1:5149: address already in use".
 *
 *   This orchestrator owns the lifecycle instead: it watches the `exit` event,
 *   and it tears down the whole process tree explicitly (`taskkill /T` on
 *   Windows, process groups elsewhere).
 *
 * Usage:  npm run dev
 */

import { spawn } from 'node:child_process'
import net from 'node:net'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const isWindows = process.platform === 'win32'

const API_PORT = 5149
const WEB_PORT = 5173

const RESET = '\x1b[0m'
const COLORS = { api: '\x1b[36m', web: '\x1b[35m' } // cyan, magenta

/**
 * The two processes to run. `api` serves http://localhost:5149 (the `http`
 * launch profile, which is what the Vite dev proxy targets); `web` serves the
 * Vite dev server on http://localhost:5173.
 */
const TARGETS = [
  {
    name: 'api',
    command: 'dotnet',
    args: [
      'watch',
      'run',
      '--project',
      'src/backend/SalsCaterSuite.WebApi/SalsCaterSuite.WebApi.csproj',
      '--launch-profile',
      'http',
    ],
    cwd: repoRoot,
  },
  {
    name: 'web',
    // `npm` is a .cmd shim on Windows, which cannot be executed without a shell.
    // The command is passed as a single string (no `args`) so Node does not
    // concatenate unescaped arguments, and so the frontend's own package.json
    // script stays the single source of truth for how the UI is started.
    commandLine: 'npm run dev',
    cwd: path.join(repoRoot, 'src', 'frontend', 'salscatersuite-ui'),
    shell: isWindows,
  },
]

/** @type {Map<string, import('node:child_process').ChildProcess>} */
const children = new Map()
let shuttingDown = false

// ── Output ──────────────────────────────────────────────────────────────────

/**
 * Splits a byte stream into lines and writes each one behind a coloured
 * `[api]` / `[web]` tag so the two processes stay readable in one console.
 */
function createLineWriter(stream, label) {
  const tag = `${COLORS[label]}[${label}]${RESET}`
  let pending = ''

  const write = (chunk) => {
    pending += chunk.toString()

    const lines = pending.split(/\r?\n/)
    pending = lines.pop() ?? ''

    for (const line of lines) {
      stream.write(`${tag} ${line}\n`)
    }
  }

  // A process that ends without a trailing newline would otherwise lose its
  // last line.
  write.flush = () => {
    if (pending.length > 0) {
      stream.write(`${tag} ${pending}\n`)
      pending = ''
    }
  }

  return write
}

// ── Lifecycle ───────────────────────────────────────────────────────────────

/**
 * Terminates a child and everything it spawned.
 *
 * `taskkill /T` walks the process tree, which is required on Windows because
 * the API is started as `dotnet watch -> dotnet run -> SalsCaterSuite.WebApi.exe`
 * and killing only the first link leaves the rest running.
 */
function killTree(pid) {
  if (pid == null) return

  if (isWindows) {
    spawn('taskkill', ['/pid', String(pid), '/T', '/F'], { stdio: 'ignore' })
  } else {
    try {
      process.kill(-pid, 'SIGTERM')
    } catch {
      try {
        process.kill(pid, 'SIGTERM')
      } catch {
        /* already gone */
      }
    }
  }
}

function stopAll(exitCode) {
  if (shuttingDown) return
  shuttingDown = true

  for (const child of children.values()) {
    killTree(child.pid)
  }

  // Give the trees a moment to release their ports before the runner exits.
  setTimeout(() => process.exit(exitCode), 700)
}

/** Keeps the exit code in the 0-255 range that a shell can report. */
function normaliseExitCode(code) {
  if (typeof code !== 'number' || !Number.isInteger(code)) return 1
  if (code === 0) return 0
  return code >= 0 && code <= 255 ? code : 1
}

// ── Pre-flight: is a dev port already taken? ─────────────────────────────────

function isPortInUse(port) {
  return new Promise((resolve) => {
    const sockets = []
    let pending = 0
    let inUse = false

    const finish = () => {
      for (const socket of sockets) socket.destroy()
      resolve(inUse)
    }

    for (const host of ['127.0.0.1', '::1']) {
      pending += 1
      const socket = net.connect({ port, host })
      sockets.push(socket)

      let settled = false
      const done = (result) => {
        if (settled) return
        settled = true
        if (result === 'in-use') inUse = true
        pending -= 1
        if (pending === 0) finish()
      }

      socket.setTimeout(500)
      socket.once('connect', () => done('in-use'))
      socket.once('error', () => done('failed'))
      socket.once('timeout', () => done('failed'))
    }
  })
}

/** Looks up the PID holding a port (Windows only) to make the error actionable. */
function findPortOwner(port) {
  return new Promise((resolve) => {
    if (!isWindows) return resolve(null)

    const child = spawn('netstat', ['-ano', '-p', 'TCP'], { stdio: ['ignore', 'pipe', 'ignore'] })
    let output = ''
    child.stdout.on('data', (chunk) => (output += chunk.toString()))
    child.on('close', () => {
      const match = output
        .split(/\r?\n/)
        .find((line) => new RegExp(`[:.]${port}\\s`).test(line) && /LISTENING/i.test(line))
      if (!match) return resolve(null)
      resolve(match.trim().split(/\s+/).pop() ?? null)
    })
  })
}

async function reportPortConflict(label, port) {
  if (!(await isPortInUse(port))) return false

  const owner = await findPortOwner(port)
  const ownerText = owner ? ` (PID ${owner})` : ''

  if (label === 'api') {
    process.stderr.write(
      `\n[dev] Port ${port} is already in use${ownerText}.\n` +
        `[dev] Another copy of the API is still running. Stop it, then start again:\n` +
        `[dev]   taskkill /PID ${owner ?? '<pid>'} /T /F\n\n`,
    )
    return true
  }

  process.stderr.write(
    `\n[dev] Port ${port} is already in use${ownerText}. Vite will use the next free port.\n\n`,
  )
  return false
}

// ── Start ───────────────────────────────────────────────────────────────────

async function main() {
  if (await reportPortConflict('api', API_PORT)) {
    process.exit(1)
  }
  await reportPortConflict('web', WEB_PORT)

  for (const target of TARGETS) {
    const child = spawn(target.command ?? target.commandLine, target.args ?? [], {
      cwd: target.cwd,
      shell: target.shell ?? false,
      stdio: ['inherit', 'pipe', 'pipe'],
      // A dedicated process group on POSIX so the whole tree can be signalled.
      detached: !isWindows,
    })

    children.set(target.name, child)

    const writeOut = createLineWriter(process.stdout, target.name)
    const writeErr = createLineWriter(process.stderr, target.name)

    child.stdout.on('data', writeOut)
    child.stderr.on('data', writeErr)

    // `exit` fires as soon as the process is gone. `close` is unreliable here
    // because a surviving grandchild keeps the inherited stdout pipe open.
    child.on('exit', (code, signal) => {
      writeOut.flush()
      writeErr.flush()

      const reason = signal ? `signal ${signal}` : `code ${code}`
      process.stdout.write(
        `${COLORS[target.name]}[${target.name}]${RESET} process exited (${reason}).\n`,
      )
      stopAll(normaliseExitCode(code))
    })

    child.on('error', (error) => {
      process.stderr.write(
        `${COLORS[target.name]}[${target.name}]${RESET} failed to start: ${error.message}\n`,
      )
      stopAll(1)
    })
  }

  process.on('SIGINT', () => {
    process.stdout.write('\n[dev] Shutting down…\n')
    stopAll(0)
  })

  process.on('SIGTERM', () => {
    stopAll(0)
  })
}

main()
