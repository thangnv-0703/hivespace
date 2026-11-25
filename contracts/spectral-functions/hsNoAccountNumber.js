// Walks a response schema, following local $refs, and reports any property named accountNumber (NFR-SEC-008).
export default function hsNoAccountNumber(schema, _opts, context) {
  const root = context.document.data
  const seen = new Set()
  const results = []
  const resolve = (ref) => ref.startsWith('#/') ? ref.slice(2).split('/').reduce((n, k) => n?.[k.replace(/~1/g, '/').replace(/~0/g, '~')], root) : undefined
  const walk = (node, path) => {
    if (!node || typeof node !== 'object' || seen.has(node)) return
    seen.add(node)
    if (typeof node.$ref === 'string') {
      const target = resolve(node.$ref)
      if (target) walk(target, path)
      return
    }
    for (const [name, child] of Object.entries(node.properties ?? {})) {
      if (name === 'accountNumber') results.push({ message: 'A response schema must not declare accountNumber (NFR-SEC-008).', path })
      walk(child, [...path, 'properties', name])
    }
    for (const key of ['items', 'additionalProperties']) walk(node[key], [...path, key])
    for (const key of ['allOf', 'anyOf', 'oneOf']) (node[key] ?? []).forEach((child, i) => walk(child, [...path, key, i]))
  }
  walk(schema, [])
  return results
}
