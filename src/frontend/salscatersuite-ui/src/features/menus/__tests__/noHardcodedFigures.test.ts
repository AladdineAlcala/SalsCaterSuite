import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

const MENUS_ROOT = join(process.cwd(), 'src', 'features', 'menus')

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
 * Documentation section references (e.g. "spec §4.2") are not rendered
 * figures; a literal inside a string literal is, so strings are kept.
 */
function stripComments(source: string): string {
  return source
    .replace(/\/\*[\s\S]*?\*\//g, ' ')
    .replace(/(^|[^:])\/\/.*$/gm, '$1')
}

/**
 * AC-M2 / F2 — no figure may be hardcoded in the Menu & Food Costing feature.
 *
 * The check looks for
 *   • the peso sign or a `PHP` currency literal (anywhere in the file),
 *   • a thousands-separated number (`1,425,800`),
 *   • a decimal number (`78.45`) that is not part of a Tailwind class,
 *   • an integer of four digits or more that is not part of a Tailwind class,
 *   • a **percentage literal** (`32%`) — every percentage comes from the API,
 *   • a **benchmark literal** — a number assigned to a target/benchmark/
 *     ceiling/threshold name, or one of the benchmark values the approved
 *     specification calls out, used as a bare number.
 */
describe('menus feature contains no hardcoded figures', () => {
  const files = collectSourceFiles(MENUS_ROOT)

  it('finds the menu source files', () => {
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

    // A percentage literal — the benchmark and every margin figure are the
    // server's (DEC-002, DEC-040).
    const percentageLiterals = code.match(/(?<![a-zA-Z0-9.])\d+(?:\.\d+)?\s*%/g)
    expect(
      percentageLiterals ?? [],
      `${relative} must not contain a hardcoded percentage literal`,
    ).toEqual([])

    // A number assigned to a benchmark name — e.g. `targetFoodCostPercentage = 32`.
    const benchmarkAssignments = code.match(
      /(?:target|benchmark|ceiling|threshold)[A-Za-z]*\s*[:=]\s*\d+/gi,
    )
    expect(
      benchmarkAssignments ?? [],
      `${relative} must not assign a number to a benchmark`,
    ).toEqual([])

    // The benchmark values the approved specification calls out, used as a
    // bare number rather than published by the API.
    const bareBenchmarks = code.match(/(?<![a-zA-Z0-9.\-])(?:32|35)(?![a-zA-Z0-9.\-%])/g)
    expect(
      bareBenchmarks ?? [],
      `${relative} must not hardcode a benchmark percentage`,
    ).toEqual([])
  })
})
