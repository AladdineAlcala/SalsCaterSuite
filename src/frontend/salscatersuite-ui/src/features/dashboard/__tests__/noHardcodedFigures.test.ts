import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

const DASHBOARD_ROOT = join(process.cwd(), 'src', 'features', 'dashboard')

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
 * Documentation section references (e.g. "spec §9.2") are not rendered
 * figures; a literal inside a string literal is, so strings are kept.
 */
function stripComments(source: string): string {
  return source
    .replace(/\/\*[\s\S]*?\*\//g, ' ')
    .replace(/(^|[^:])\/\/.*$/gm, '$1')
}

/**
 * AC-F4 — no figure may be hardcoded in the dashboard feature.
 *
 * The check looks for
 *   • the peso sign or a `PHP` currency literal (anywhere in the file),
 *   • a thousands-separated number (`1,425,800`),
 *   • a decimal number (`78.45`) that is not part of a Tailwind class,
 *   • an integer of four digits or more that is not part of a Tailwind class.
 */
describe('dashboard feature contains no hardcoded figures', () => {
  const files = collectSourceFiles(DASHBOARD_ROOT)

  it('finds the dashboard source files', () => {
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
  })
})
