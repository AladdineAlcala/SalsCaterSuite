import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

const BOOKINGS_ROOT = join(process.cwd(), 'src', 'features', 'bookings')
const PAX_CLASSIFICATION_LABELS = [
  '15 to 19 Pax',
  '20 to 29 Pax',
  '30 to 49 Pax',
  '80 to 100 Above',
  'Not Applicable to PackMeals',
]

function collectSourceFiles(directory: string): string[] {
  const files: string[] = []
  for (const entry of readdirSync(directory)) {
    if (entry === '__tests__') {
      // Test files may hold a fixture; the rule binds application code only.
      continue
    }
    const fullPath = join(directory, entry)
    if (statSync(fullPath).isDirectory()) {
      files.push(...collectSourceFiles(fullPath))
    } else if (/\.(ts|tsx)$/.test(entry)) {
      files.push(fullPath)
    }
  }
  return files
}

/**
 * Removes comments so the numeric rules only inspect executable code.
 * Documentation section references (e.g. "spec §5.2") are not rendered
 * figures; a literal inside a string literal is, so strings are kept.
 */
function stripComments(source: string): string {
  return source
    .replace(/\/\*[\s\S]*?\*\//g, ' ')
    .replace(/(^|[^:])\/\/.*$/gm, '$1')
}

/**
 * No figure may be hardcoded in the Bookings & Events feature (AC-BKG-02,
 * AC-BKG-17, AC-BKG-18).
 *
 * The check looks for
 *   • the peso sign or a `PHP` currency literal (anywhere in the file),
 *   • a thousands-separated number (`1,425,800`),
 *   • a decimal number (`78.45`) that is not part of a Tailwind class,
 *   • an integer of four digits or more that is not part of a Tailwind class,
 *   • a **percentage literal** (`32%`) — every rate figure comes from the API,
 *   • a **benchmark literal** — a number assigned to a target/benchmark/
 *     ceiling/threshold name.
 *
 * The reference's `30` (the deposit percentage), `400` (the capacity) and
 * `58.2` (the per-event margin) must never appear in this folder.
 */
describe('bookings feature contains no hardcoded figures', () => {
  const files = collectSourceFiles(BOOKINGS_ROOT)

  it('finds the bookings source files', () => {
    expect(files.length).toBeGreaterThan(0)
  })

  it.each(files)('%s has no literal currency or metric', (file) => {
    const source = readFileSync(file, 'utf8')
    const code = stripComments(source)
    const relative = file.slice(process.cwd().length + 1)

    expect(source, `${relative} must not contain the peso sign`).not.toContain('₱')
    expect(source, `${relative} must not contain a PHP currency literal`).not.toMatch(/\bPHP\b/)

    const thousands = code.match(/(?<![a-zA-Z0-9-])\d{1,3}(?:,\d{3})+(?:\.\d+)?/g)
    expect(thousands ?? [], `${relative} must not contain a thousands-separated literal`).toEqual(
      [],
    )

    const decimals = code.match(/(?<![a-zA-Z0-9-])\d+\.\d+(?![a-zA-Z0-9-])/g)
    expect(decimals ?? [], `${relative} must not contain a decimal metric literal`).toEqual([])

    const largeIntegers = code.match(/(?<![a-zA-Z0-9-])\d{4,}(?![a-zA-Z0-9-])/g)
    expect(
      largeIntegers ?? [],
      `${relative} must not contain a four-digit metric literal`,
    ).toEqual([])

    // A percentage literal — every rate and every share is the server's
    // (DEC-002, DEC-103).
    const percentageLiterals = code.match(/(?<![a-zA-Z0-9.])\d+(?:\.\d+)?\s*%/g)
    expect(
      percentageLiterals ?? [],
      `${relative} must not contain a hardcoded percentage literal`,
    ).toEqual([])

    // A number assigned to a benchmark name — e.g. `targetRate = 500`.
    const benchmarkAssignments = code.match(
      /(?:target|benchmark|ceiling|threshold)[A-Za-z]*\s*[:=]\s*\d+/gi,
    )
    expect(
      benchmarkAssignments ?? [],
      `${relative} must not assign a number to a benchmark`,
    ).toEqual([])
  })

  it.each(files)('%s does not hardcode an applicable-pax lookup label', (file) => {
    const code = stripComments(readFileSync(file, 'utf8'))
    const relative = file.slice(process.cwd().length + 1)

    for (const label of PAX_CLASSIFICATION_LABELS) {
      expect(code, `${relative} must read “${label}” from the API`).not.toContain(label)
    }
  })
})
