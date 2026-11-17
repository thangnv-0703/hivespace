// Generates src/theme.css from docs/tokens.json (ADR-0021). Do not edit theme.css by hand.
//   node scripts/build-theme.mjs          write src/theme.css
//   node scripts/build-theme.mjs --check  fail if theme.css or the docs/design.md block is stale
import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const repo = resolve(here, '../../../..')
const tokensPath = resolve(repo, 'docs/tokens.json')
const designPath = resolve(repo, 'docs/design.md')
const outPath = resolve(here, '../src/theme.css')

const tokens = JSON.parse(readFileSync(tokensPath, 'utf8'))

const isToken = (node) => node && typeof node === 'object' && '$value' in node
const entries = (node) => Object.entries(node).filter(([k]) => !k.startsWith('$'))

function leaves(node, path = []) {
  if (isToken(node)) return [[path, node]]
  return entries(node).flatMap(([k, v]) => leaves(v, [...path, k]))
}

function lookup(ref) {
  let node = tokens
  for (const part of ref.split('.')) node = node[part]
  if (!isToken(node)) throw new Error(`Unresolved token reference {${ref}}`)
  return node
}

const fontStack = (list) => list.map((f) => (/\s/.test(f) ? `"${f}"` : f)).join(', ')
const shadow = (layers) =>
  layers.map((l) => `${l.offsetX} ${l.offsetY} ${l.blur} ${l.spread} ${l.color}`).join(', ')

// Primitive colour reference -> var(--color-...); literal value -> as is.
function colorRef(value) {
  const m = /^\{color\.(.+)\}$/.exec(value)
  return m ? `var(--color-${m[1].replaceAll('.', '-')})` : value
}

function themeBlock() {
  const out = []
  for (const [path, t] of leaves(tokens.color)) out.push(`  --color-${path.join('-')}: ${t.$value};`)
  for (const [name, t] of entries(tokens.font.family)) out.push(`  --font-${name}: ${fontStack(t.$value)};`)
  for (const [name, t] of entries(tokens.font.weight)) out.push(`  --font-weight-${name}: ${t.$value};`)
  for (const [name, t] of entries(tokens.text)) {
    out.push(`  --text-${name}: ${t.$value.fontSize};`)
    out.push(`  --text-${name}--line-height: ${t.$value.lineHeight};`)
  }
  out.push(`  --spacing: ${tokens.space['1'].$value}; /* space.N = N x 4px */`)
  for (const [name, t] of entries(tokens.radius)) out.push(`  --radius-${name}: ${t.$value};`)
  for (const [name, t] of entries(tokens.size)) out.push(`  --size-${name}: ${t.$value};`)
  for (const [name, t] of entries(tokens.shadow)) out.push(`  --shadow-${name}: ${shadow(t.$value)};`)
  for (const [name, t] of entries(tokens.breakpoint)) out.push(`  --breakpoint-${name}: ${t.$value};`)
  for (const [name, t] of entries(tokens.z)) out.push(`  --z-${name}: ${t.$value};`)
  for (const [name, t] of entries(tokens.duration)) out.push(`  --duration-${name}: ${t.$value};`)
  for (const [name, t] of entries(tokens.easing)) out.push(`  --ease-${name}: cubic-bezier(${t.$value.join(', ')});`)
  return out
}

function roleNames() {
  return leaves(tokens.theme.light).map(([path]) => path.join('-'))
}

function roleValues(theme) {
  return leaves(tokens.theme[theme]).map(([path, t]) => {
    // Validate that every alias resolves, so a typo in tokens.json fails the build.
    const m = /^\{(.+)\}$/.exec(t.$value)
    if (m) lookup(m[1])
    return `  --hs-${path.join('-')}: ${colorRef(t.$value)};`
  })
}

export function render() {
  return [
    '@custom-variant dark (&:where(.dark, .dark *));',
    '',
    '@theme {',
    ...themeBlock(),
    '}',
    '',
    '/* Semantic roles -> Tailwind utilities, e.g. bg-bg-surface, text-text-muted, bg-action-primary-bg */',
    '@theme inline {',
    ...roleNames().map((n) => `  --color-${n}: var(--hs-${n});`),
    '}',
    '',
    ':root {',
    ...roleValues('light'),
    '}',
    '',
    '.dark {',
    ...roleValues('dark'),
    '}',
    '',
  ].join('\n')
}

const header = '/* GENERATED from docs/tokens.json by scripts/build-theme.mjs (ADR-0021). Do not edit. */\n'
const css = render()

function designBlock() {
  const md = readFileSync(designPath, 'utf8').replaceAll('\r\n', '\n')
  const m = /<!-- tokens-css:start -->\n```css\n([\s\S]*?)```/.exec(md)
  if (!m) throw new Error('docs/design.md has no tokens-css block')
  return m[1]
}

if (process.argv.includes('--check')) {
  const problems = []
  const current = readFileSync(outPath, 'utf8').replaceAll('\r\n', '\n')
  if (current !== header + css) problems.push('src/theme.css is stale: run `pnpm --filter @hivespace/ui build:theme`')
  if (designBlock() !== css) problems.push('docs/design.md §2.4 CSS block differs from docs/tokens.json')
  for (const p of problems) console.error(p)
  process.exit(problems.length ? 1 : 0)
} else {
  writeFileSync(outPath, header + css)
  console.log(`wrote ${outPath}`)
}
