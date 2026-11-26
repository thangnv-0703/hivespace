import assert from 'node:assert/strict'
import { readdirSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { Given, Then, When } from '@cucumber/cucumber'
import type { ContractWorld } from '../support/world.ts'

// Steps for contracts/features/catalog/categories.feature. State is seeded by SQL from the valid-v1 fixture of the
// load tests (ADR-0025), so the read endpoints are tested without running the load command.

const fixture = path.resolve(
  import.meta.dirname, '..', '..', '..', 'services', 'catalog', 'tests', 'Catalog.IntegrationTests', 'Fixtures', 'category-tree', 'valid-v1',
)

interface Name { vi: string; en: string }
interface Summary { id: number; parentId: number | null; name: Name; isLeaf: boolean; position: number; imageUrl: string | null }
interface Attribute { id: number; name: Name; required: boolean; kind: string; selection: string | null; maxValues: number | null; values: { id: number; name: Name }[] }

const readJson = (file: string) => JSON.parse(readFileSync(file, 'utf-8'))

Given('a loaded category tree', async function (this: ContractWorld) {
  const db = this.service.db
  await db.query('truncate category_attributes, attribute_values, attributes, categories cascade')

  const categories = readJson(path.join(fixture, 'categories.json')).categories as {
    externalCategoryId: string; externalParentCategoryId: string | null; name: Name; imageUrl: string | null
  }[]
  const parents = new Set(categories.map((c) => c.externalParentCategoryId).filter((p) => p !== null))
  const next = new Map<string | null, number>()
  const ordered = [...categories].sort((a, b) => (a.externalParentCategoryId === null ? 0 : 1) - (b.externalParentCategoryId === null ? 0 : 1))
  const position = new Map<string, number>()
  for (const c of categories) {
    const key = c.externalParentCategoryId
    position.set(c.externalCategoryId, next.get(key) ?? 0)
    next.set(key, (next.get(key) ?? 0) + 1)
  }
  // Parents are inserted before their children (the child may come first in the file).
  const inserted = new Set<string>()
  const pending = [...ordered]
  while (pending.length > 0) {
    const index = pending.findIndex((c) => c.externalParentCategoryId === null || inserted.has(c.externalParentCategoryId))
    const [c] = pending.splice(index, 1)
    await db.query(
      'insert into categories ("Id","ParentId","Position","IsLeaf","NameVi","NameEn","ImageUrl") values ($1,$2,$3,$4,$5,$6,$7)',
      [c.externalCategoryId, c.externalParentCategoryId, position.get(c.externalCategoryId), !parents.has(c.externalCategoryId), c.name.vi, c.name.en, c.imageUrl],
    )
    inserted.add(c.externalCategoryId)
  }

  const attributes = new Map<string, Name>()
  const values = new Map<string, { attribute: string; name: Name }>()
  const links: unknown[][] = []
  const dir = path.join(fixture, 'category-attributes')
  for (const file of readdirSync(dir).sort()) {
    for (const entry of readJson(path.join(dir, file)).categories as { externalCategoryId: string; attributes: any[] }[]) {
      entry.attributes.forEach((a, index) => {
        attributes.set(a.sourceAttributeId, a.name)
        const list = a.values.length > 0
        for (const v of a.values) values.set(v.sourceValueId, { attribute: a.sourceAttributeId, name: v.name })
        const multiple = list && a.inputType === 'Checkbox'
        links.push([entry.externalCategoryId, a.sourceAttributeId, index, a.isRequired, list ? 'list' : 'text', multiple, multiple ? a.maxValueCount : 1, a.values.map((v: any) => Number(v.sourceValueId))])
      })
    }
  }
  for (const [id, name] of attributes) await db.query('insert into attributes ("Id","NameVi","NameEn") values ($1,$2,$3)', [id, name.vi, name.en])
  for (const [id, v] of values) await db.query('insert into attribute_values ("Id","AttributeId","NameVi","NameEn") values ($1,$2,$3,$4)', [id, v.attribute, v.name.vi, v.name.en])
  for (const l of links) {
    await db.query('insert into category_attributes ("CategoryId","AttributeId","Position","IsRequired","Kind","AllowsMultiple","MaxValues","ValueIds") values ($1,$2,$3,$4,$5,$6,$7,$8)', l)
  }
})

When('a client requests the root categories', async function (this: ContractWorld) {
  await this.get('/v1/categories')
})

When('a client requests the children of category {int}', async function (this: ContractWorld, id: number) {
  await this.get(`/v1/categories?parentId=${id}`)
})

When('a client requests the children of category {int} in {string}', async function (this: ContractWorld, id: number, language: string) {
  await this.get(`/v1/categories?parentId=${id}`, language)
})

When('a client requests the children of category {string}', async function (this: ContractWorld, id: string) {
  await this.get(`/v1/categories?parentId=${encodeURIComponent(id)}`)
})

When('a client requests category {int}', async function (this: ContractWorld, id: number) {
  await this.get(`/v1/categories/${id}`)
})

When('a client requests category {string}', async function (this: ContractWorld, id: string) {
  await this.get(`/v1/categories/${encodeURIComponent(id)}`)
})

const items = (world: ContractWorld) => world.body.items as Summary[]
const item = (world: ContractWorld, id: string) => {
  const found = items(world).find((c) => String(c.id) === id)
  assert.ok(found, `category ${id} is not in the response`)
  return found
}
const attribute = (world: ContractWorld, id: string) => {
  const found = (world.body.attributes as Attribute[]).find((a) => String(a.id) === id)
  assert.ok(found, `attribute ${id} is not in the response`)
  return found
}

Then('every category has a Vietnamese name and an English name', function (this: ContractWorld) {
  assert.ok(items(this).length > 0)
  for (const c of items(this)) {
    assert.ok(c.name.vi.trim().length > 0)
    assert.ok(c.name.en.trim().length > 0)
  }
})

Then('the category ids are {string}', function (this: ContractWorld, ids: string) {
  assert.deepEqual(items(this).map((c) => String(c.id)), ids.split(','))
})

Then('there are no categories', function (this: ContractWorld) {
  assert.deepEqual(items(this), [])
})

Then('the category {string} is a leaf', function (this: ContractWorld, id: string) {
  assert.equal(item(this, id).isLeaf, true)
})

Then('the category {string} is not a leaf', function (this: ContractWorld, id: string) {
  assert.equal(item(this, id).isLeaf, false)
})

Then('the category {string} has image {string}', function (this: ContractWorld, id: string, url: string) {
  assert.equal(item(this, id).imageUrl, url)
})

Then('the category {string} has no image', function (this: ContractWorld, id: string) {
  assert.equal(item(this, id).imageUrl, null)
})

Then('the category path is {string}', function (this: ContractWorld, ids: string) {
  assert.deepEqual((this.body.path as { id: number }[]).map((p) => String(p.id)), ids.split(','))
})

Then('the attribute {string} is a(n) {word} {word} list with maximum {int} and values {string}', function (this: ContractWorld, id: string, required: string, selection: string, max: number, values: string) {
  const a = attribute(this, id)
  assert.equal(a.required, required === 'required')
  assert.equal(a.kind, 'list')
  assert.equal(a.selection, selection)
  assert.equal(a.maxValues, max)
  assert.deepEqual(a.values.map((v) => String(v.id)), values.split(','))
})

Then('the attribute {string} is an optional text attribute', function (this: ContractWorld, id: string) {
  const a = attribute(this, id)
  assert.equal(a.required, false)
  assert.deepEqual([a.kind, a.selection, a.maxValues, a.values], ['text', null, null, []])
  assert.ok(a.name.vi.length > 0 && a.name.en.length > 0)
})

Then('the value {string} of attribute {string} is named {string} in both languages', function (this: ContractWorld, valueId: string, id: string, name: string) {
  const v = attribute(this, id).values.find((x) => String(x.id) === valueId)
  assert.ok(v, `value ${valueId} is not in the attribute`)
  assert.deepEqual(v.name, { vi: name, en: name })
})

Then('the category has no attributes', function (this: ContractWorld) {
  assert.deepEqual(this.body.attributes, [])
})
