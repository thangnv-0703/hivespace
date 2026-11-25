# Technical Design: FEAT-014 — Category tree with attributes

> Feature from: docs/implementation-plan.md · Epic: E3 Catalog
> Business spec: [feature-spec.md](feature-spec.md) (Agreed 2026-11-26)
> Implements: FR-CAT-003, FR-CAT-019, FR-CAT-020 (definition; FEAT-012 applies it to products), FR-LOC-007 (categories, attribute names and values), FR-CAT-021 (top-level category images: data and contract; FEAT-018 shows them); supports FR-CAT-004 (required flag), FR-LOC-008, NFR-LEG-002
> Realizes: UC-020 (main 1–2 for categories, alt 1a), UC-005 (step 1–2: what the seller picks from), UC-007 (the images the home page category bar shows) · Screens: none (operator command); consumed by SCR-SEL-004 (FEAT-012) and SCR-STF-001/002 (FEAT-018)
> Depends on: walking skeleton (Catalog service, outbox, `GET /v1/categories`, storefront `CategoryBar`)
> Status: Draft · Date: 2026-11-26
> Diagram: [design-flow.html](design-flow.html) (interactive flow diagram, html-flow-diagram)

## 1. Intent
The operator runs one Catalog command, `load-categories`, over a committed file directory. The command validates the whole file against the current tree, then either applies every change in one transaction and emits `CategoryTreeChanged`, or changes nothing and lists every problem. Two public read endpoints expose the tree one level at a time, with each top-level category's image link, and a leaf's attributes with names in vi and en. The skeleton's flat category table and its `thoi-trang` row are replaced.

Answers to feature-spec §7:
- **Input format:** the Tiki shape with `name` as an object `{vi, en}` on categories, attributes and values (§3.4, user decision 2026-11-25).
- **Attributes on a non-leaf, and a leaf that gains children:** they are stored and kept, but never exposed or used. Only a leaf's attributes are returned or applied to products (D-5).
- **~4,000 leaves in FEAT-012's picker:** the tree is read lazily, one level per request, and a leaf's attributes in a separate call (D-4). Picker search is FEAT-012's.
- **How other services learn of changes:** the thin event `CategoryTreeChanged/v1`, which says only that the tree changed (D-6). Consumers read the current tree on their side (FEAT-018).
- **Category images (FR-CAT-021):** the file's `imageUrl` is read on top-level categories only and stored on the category; a reload may add, change or remove it. `GET /v1/categories` returns `imageUrl`, `null` when there is none, and the client shows the placeholder for `null` (D-10).
- **All-or-nothing and the report:** diff in memory, apply in one transaction under an advisory lock, print a count table and the problem list, exit code 0 or 2 (§5.3, D-3, D-7).

## 2. Codebase context
Surveyed on 2026-11-25 on `feat/014-category-tree-with-attributes` at `9c49542` (FEAT-001..006, 009, 010 merged). The design conforms to the following.

- **Catalog is still the walking skeleton.** It is a rich service (Api → Application → Domain, Infrastructure → Application; NetArchTest in `Catalog.ArchitectureTests`). `Catalog.Domain/Categories/Category.cs` is flat (`Id`, `Slug`, `NameVi`, `NameEn`). `ListCategoriesHandler` returns every row ordered by slug, plus a skeleton gRPC `availabilityProbe` to Inventory (removed by FEAT-015). The ports are in `Catalog.Application/Abstractions/Ports.cs` (`ICategoryReadStore`, `IInventoryClient`, Catalog's own `IEventOutbox.WriteAsync`, which ADR-0028 lets Catalog keep).
- **Schema:** one migration, `20261117142656_Initial`. It creates `categories` (PascalCase columns `Id`, `Slug` varchar(120) unique, `NameVi`, `NameEn` varchar(200)), `outbox` and `inbox`, and inserts the `HasData` row `01930000-0000-7000-8000-000000000001` / `thoi-trang`. Catalog uses EF's default PascalCase columns; Identity uses explicit snake_case. This design keeps Catalog's convention.
- **Contracts:** `contracts/openapi/catalog.yaml` has `listCategories` (`security: []`, `CategoryList {items[Category{id, slug, name: LocalizedText{vi,en}}], availabilityProbe}`) and the skeleton `publishPing`. Platform content is returned in both languages and the client picks one: the storefront `CategoryBar.vue` shows `locale === 'en' ? name.en : name.vi`. `Accept-Language` (shared parameter, resolved by `LanguageResolver`) selects only error messages. Errors are `{code, message, details[]}` through `UseApiErrors`. `contracts/features/catalog/categories.feature` asserts the `thoi-trang` slug.
- **Gateway:** route `catalog-categories` (`/v1/categories/{**rest}`, policy `anonymous`) already covers `/v1/categories` and `/v1/categories/{id}`. No change is needed.
- **Events:** schemas live in `contracts/events/<Type>/v1.schema.json` inside `envelope.schema.json` (`eventType`, `eventVersion`, `producer`, …). Integration tests validate the outbox payload with JsonSchema.Net (the Identity `ProfileFlowTests` pattern). No Catalog event exists yet, and no service consumes `CategoryTreeChanged` yet (architecture §5.4: the consumer is Search-AI, built in FEAT-018).
- **Commands and seeding:** no console command, `System.CommandLine` or Aspire resource command exists anywhere. `Program.cs` migrates on start behind `Database:MigrateOnStartup`. The AppHost registers Catalog in `apphost/HiveSpace.AppHost/Resources/DotnetServices.cs`.
- **Tests:** `Catalog.IntegrationTests/PostgresFixture.cs` (Testcontainers `pgvector/pgvector:pg17`), `MigrationTests.cs` (asserts the skeleton row, so it changes here), `Catalog.UnitTests`. Contract tests run the built `Catalog.Api.dll` against a Postgres container and seed state by direct SQL (ADR-0025). `tests/contract/steps/catalog.steps.ts` reads `slug`.
- **Concurrency:** Identity uses `xmin` row versions. Catalog has none. There is no HTTP caching, ETag or Cache-Control anywhere.
- **Input data surveyed:** `references/hivespace.crawler/crawler-state/tiki-v2`. `categories.json` has 6,101 categories, 38 roots, depth up to 8 and parents before children, and no position or leaf field. `category-attributes/chunk-*.json` (62 files) holds attributes per category. Attribute keys (386) and value keys (8,058) are global and carry the same name everywhere. However, `isRequired` differs between categories for 18 attributes and the value list differs for 21. `inputType` is only `Dropdown` (single) or `Checkbox` (`maxValueCount` 5), and, in the attribute files as edited on 2026-11-25, 1,178 categories have attributes: 1,170 leaves and 8 non-leaf categories. 3,727 of the 4,897 leaves have none. In all 8 non-leaf cases the attributes sit on the parent and every child has none (for example `7075` "Loa" with 7 empty child leaves), because Tiki defines them on the parent's product set. 27 of the 38 roots have an `imageUrl` (566 categories in all), every one on `salt.tikicdn.com`, the longest 82 characters.
- **Divergences recorded:** (1) ADR-0018 leaves Catalog's store open; the user chose PostgreSQL on 2026-11-25 (D-1, §8). (2) dev-standards §2 and repo-structure put seeds under `tools/seed/`; the tree loader lives in Catalog instead (D-3). (3) `data/` at the repo root is not in repo-structure.md (§8). (4) UC-020 has no exception flow for a refused load; FR-CAT-019 and the spec define it (§8).

## 3. API contracts

Conventions inherited: JSON, camelCase, `/v1` prefix, shared `Error`, public reads with `security: []`, platform names as `LocalizedText {vi, en}` and the client picks the language. An unknown or malformed id is `404 common.not_found`, which needs no new error code. Exception to the shared `Id` (UUID v7): category, attribute and value ids are the Tiki integer keys (D-9).

### 3.1 Catalog REST (public, through the gateway's existing anonymous route)

| Endpoint | Request | Success | Errors |
| :-- | :-- | :-- | :-- |
| `GET /v1/categories` (`listCategories`, changed) | query `parentId` (optional, integer) | `200 CategoryList` | `404 common.not_found` when `parentId` is not a category or not an integer |
| `GET /v1/categories/{id}` (`getCategory`, new) | path `id` (integer) | `200 CategoryDetail` | `404 common.not_found` |

- **`CategoryList`** = `{items: CategorySummary[], availabilityProbe?: string}`. `availabilityProbe` stays as the skeleton probe until FEAT-015.
  - Without `parentId`, `items` are the root categories, so `CategoryBar` keeps working.
  - With `parentId`, `items` are that category's children (`[]` for a leaf).
  - `items` are ordered by `position`.
  - The list is not paged: one level holds at most a few hundred nodes in the source data, and dev-standards' paging rule has no lint rule that forces otherwise (D-4).
- **`CategorySummary`** = `{id: integer, parentId: integer|null, name: LocalizedText, isLeaf: boolean, position: integer, imageUrl: string (uri)|null}`.
  - `imageUrl` is set only on top-level categories that have an image (FR-CAT-021, BR-014-13); `null` means "show the placeholder" for a top-level category and "no image" below it (D-10).
- **`CategoryDetail`** = `CategorySummary` + `{path: CategoryRef[], attributes: CategoryAttribute[]}`.
  - `path` runs from the root to the category's parent; it is empty for a root. `CategoryRef` = `{id, name}`.
  - `attributes` follow file order and are `[]` for a non-leaf (BR-014-10, D-5) or for a leaf with none (AC-014-4.4).
- **`CategoryAttribute`** = `{id, name: LocalizedText, required: boolean, kind: "list"|"text", selection: "single"|"multiple"|null, maxValues: integer|null, values: AttributeValue[]}`.
  - For `kind: list`: `selection` is set, `maxValues` is 1 for single and ≥ 2 for multiple, and `values` follow file order.
  - For `kind: text`: `selection` and `maxValues` are `null` and `values` is `[]` (BR-014-8, BR-014-9).
- **`AttributeValue`** = `{id, name: LocalizedText}`.
- **`LocalizedText`** stays `{vi, en}`, both required. FR-LOC-008: if a stored `en` were ever blank, the mapper returns `vi` in `en` (the loader never stores a blank, BR-014-4).
- **Removed:** `Category.slug` and the `Category` schema itself (replaced by `CategorySummary`). This breaks the skeleton contract (D-8). `publishPing` is unchanged.
- **Ids:** category, attribute and value ids are the Tiki keys from the file, as integers (`format: int32`), so every row maps back to the source (D-9). FEAT-012 stores these integer ids on products. Products, shops and every other entity keep UUID v7 (ADR-0009).
- **Idempotency:** both endpoints are read-only and have no side effects other than the skeleton probe.

### 3.2 Event `CategoryTreeChanged/v1` (producer: catalog)
`contracts/events/CategoryTreeChanged/v1.schema.json`:
- `{changedAt: date-time}`, required, `additionalProperties: false`.
- Correlation id: the load id (a UUID v7 generated per run).
- Written to the outbox in the load's transaction, and only when the load applied at least one change. A refused or unchanged load writes none (AC-014-1.5).
- It carries no tree content. The tree is too big for one message (Service Bus limit 256 KB), and consumers read the tree on their side (FEAT-018, D-6).
- No version number: the event carries no tree data and a consumer always reads the current tree, so an event that arrives late or twice only causes one extra read, never stale data (D-6).

### 3.3 OpenAPI, Spectral and client
- Edit `catalog.yaml` first (dev-standards §6) and add the `AcceptLanguage` parameter to both operations for error messages.
- Spectral must stay at 0 errors. Regenerate `web/packages/api-client` (catalog) and never hand-edit it.
- Every operation keeps a `.feature`: `contracts/features/catalog/categories.feature` is rewritten for `listCategories` and `getCategory` (§6).

### 3.4 Load command and input file (operator interface)
**Command:**
```
Catalog.Api load-categories --source <dir>
```
- Locally it runs as the AppHost resource `catalog-load-categories` (manual start, D-3), or with `dotnet run --project services/catalog/src/Catalog.Api -- load-categories --source data/category-tree` plus `ConnectionStrings__catalogdb`.
- In the cloud it runs as a one-off job on the Catalog image (FEAT-037 wires it; not needed now).
- Exit codes: `0` applied or unchanged, `2` refused (problems listed), `1` unexpected failure (nothing committed).

**Source directory** (`data/category-tree/`, committed; user decision 2026-11-25):

| File | Shape (fields the loader reads; others ignored) |
| :-- | :-- |
| `categories.json` | `{categories: [{externalCategoryId: string, externalParentCategoryId: string\|null, name: {vi, en}, imageUrl: string\|null}]}`. `imageUrl` is read only for top-level categories (no parent): an absolute `https` URL of at most 500 characters, or `null`/absent for none (BR-014-13). Keys are Tiki's numeric ids written as strings (`"2549"`) or numbers, parsed to `int` and kept as the ids (D-9). Array order is sibling order (BR-014-3). A child may appear before its parent. |
| `category-attributes/*.json` | `{categories: [{externalCategoryId, attributes: [{sourceAttributeId, name: {vi, en}, isRequired: bool, inputType: "Dropdown"\|"Checkbox", maxValueCount: int, values: [{sourceValueId, name: {vi, en}}]}]}]}`. Files are read in file-name order; a category with no entry has no attributes. |
| `SOURCE.md` | Records the source (Tiki seller category taxonomy, adapted), the owner's acceptance of its use and of linking Tiki's top-level category images (NFR-LEG-002, BR-014-12). |

Fields the loader ignores: `path`, `productSetId`, `imageUrl` below the top level (AC-014-5.4), `metadata`, `status`, `errorSummary`, `displayName`, `totalValueCount`, `valuesTruncated`, and the `source`/`crawl` headers.

**Mapping:**
- `Dropdown` → single, maximum 1.
- `Checkbox` → multiple, maximum = `maxValueCount`, which must be ≥ 2.
- No `values` → free text, and `inputType`/`maxValueCount` are ignored (BR-014-8).

**Report** (stdout; problems also go to the log):
```
Category tree load · source data/category-tree · load 0193…
Result: APPLIED                 (or: UNCHANGED · REFUSED, 7 problems)
               added  renamed  loosened  reordered  unchanged   total
categories        12        3         –          1       6085    6101
leaves            10        –         –          –       4887    4897
attributes        40        2         5          –      54945   54992
list values      310        8         –          –     709822  710140
images (top level): 2 added, 1 changed, 0 removed, 24 unchanged
Problems (refused loads only), one per line:
  [category.moved] category 54286 "Các loại thịt khác": parent 54276 → 54280
  [name.missing] value 4146177 of attribute 1799 in category 68492: en is missing
```
- Rows are counted per kind. `attributes` counts category–attribute links: `renamed` counts attribute names (global, per key) and `loosened` counts links. `list values` counts values in category lists: `renamed` counts value names (global, per key). `leaves` added = leaves that are new or that were not leaves before.
- `images` counts top-level images: a new link, a different link, a link left out; all three are allowed (BR-014-6).
- `reordered` is a position change only. It is allowed and needs no business rule because it follows the file (BR-014-3).

### 3.5 Problem catalogue (each problem names its code, the kind, the key, the category where relevant, and the offending detail)

| Code | When | Rule |
| :-- | :-- | :-- |
| `file.unreadable` / `file.malformed` | missing `categories.json`; invalid JSON (file, line, position); wrong type for a read field | — (input integrity) |
| `name.missing` | `name.vi` or `name.en` absent, not a string, or empty after cleaning, on a category, attribute or value | BR-014-4, AC-014-1.3 |
| `name.too_long` | category or attribute name > 200, value name > 300 characters after cleaning | column bounds (§4) |
| `category.key_invalid` / `attribute.key_invalid` / `value.key_invalid` | a key that is not a whole number from 1 to 2,147,483,647 (keys are the Tiki ids, D-9) | BR-014-5 |
| `category.duplicate_key` | one `externalCategoryId` twice | BR-014-5 |
| `category.image_invalid` | a top-level `imageUrl` that is not a string, not an absolute `https` URL, or longer than 500 characters | FR-CAT-021 (input integrity) |
| `category.parent_unknown` | parent key not in the file | BR-014-5 |
| `category.cycle` | a category is its own ancestor | FR-CAT-003 (tree) |
| `attributes.category_unknown` / `attributes.category_duplicate` | an attribute entry for a key not in `categories.json`, or for one key twice | BR-014-5 |
| `attribute.duplicate_in_category` | the same attribute key twice in one category | BR-014-5 |
| `attribute.conflicting_name` | one attribute key with different names in different categories | BR-014-5 |
| `attribute.invalid_selection` | `inputType` not `Dropdown`/`Checkbox`, or `Checkbox` with `maxValueCount` < 2 | BR-014-9 |
| `value.duplicate_key` / `value.conflicting_name` / `value.other_attribute` | a value key twice in one list; one value key with different names; one value key under two attributes | BR-014-5 |
| `value.duplicate_name` | two values in one list equal ignoring case after cleaning (vi compared with vi, en with en) | BR-014-11 |
| `category.removed` | a stored category key absent from the file | BR-014-7 |
| `category.moved` | a stored category under a different parent | BR-014-7 |
| `category.child_under_used_leaf` | a category with products gets a child (named: the parent) | BR-014-7, AC-014-2.5 |
| `attribute.removed` | a stored category–attribute link absent | BR-014-7 |
| `value.removed` | a stored value absent from that category's list | BR-014-7 |
| `attribute.tightened` | optional → required; multiple → single; a lower maximum; list → text or text → list (the detail names which) | BR-014-7 |
| `attribute.required_on_used_category` | a new required link on a category with products | BR-014-7, AC-014-2.7 |

## 4. Schema changes
One migration, `CategoryTree`, after `20261117142656_Initial`. Columns use PascalCase like `Initial`. Category, attribute and value ids are `integer`: the Tiki key from the file, never generated (`ValueGeneratedNever`, D-9). Names use `character varying`. Every table belongs to the Catalog entities "category tree (vi, en) … attributes" (architecture §5.2) and the plan's data "Category (vi/en), category attributes (required flag, list or free text)". No new conceptual entity is introduced.

| Table | Change | Columns / constraints |
| :-- | :-- | :-- |
| `categories` | drop and recreate | The skeleton table (uuid `Id`, `Slug`, one `HasData` row) is dropped, because a uuid key cannot be altered to an integer. New: `Id` int PK (Tiki `externalCategoryId`), `ParentId` int null (FK → `categories.Id`, `ON DELETE RESTRICT`), `Position` int not null, `IsLeaf` bool not null, `NameVi`, `NameEn` varchar(200) not null, `ImageUrl` varchar(500) null with `CK_categories_ImageUrl_TopLevel` (`ImageUrl IS NULL OR ParentId IS NULL`). Index `IX_categories_ParentId_Position` (`ParentId`, `Position`). |
| `attributes` | new | `Id` int PK (Tiki `sourceAttributeId`), `NameVi`, `NameEn` varchar(200) not null. |
| `attribute_values` | new | `Id` int PK (Tiki `sourceValueId`), `AttributeId` int not null FK → `attributes` (restrict), `NameVi`, `NameEn` varchar(300) not null. Index on `AttributeId`. |
| `category_attributes` | new | `CategoryId` int FK → `categories` (restrict), `AttributeId` int FK → `attributes` (restrict), PK (`CategoryId`, `AttributeId`), `Position` int not null, `IsRequired` bool not null, `Kind` varchar(8) not null with `CK_category_attributes_Kind` (`list`/`text`), `AllowsMultiple` bool not null, `MaxValues` int not null with `CK_category_attributes_MaxValues` (`Kind = 'text' AND MaxValues = 1 AND NOT AllowsMultiple OR Kind = 'list' AND (AllowsMultiple AND MaxValues >= 2 OR NOT AllowsMultiple AND MaxValues = 1)`), `ValueIds` int[] not null default `'{}'` (ordered; empty iff `Kind = 'text'`). Index on `AttributeId`. |

- **Why `ValueIds int[]`:** names and keys are global per attribute and value, but each category has its own ordered subset (the survey found differences in 21 attributes). An ordered array on the link keeps 55k rows instead of 710k link rows. FEAT-012 checks `value = ANY(ValueIds)` (D-2).
- **Down:** drop the new tables and recreate the skeleton `categories` table with its `Slug` index and row. Down discards any loaded tree, which is acceptable because Catalog has no released data (dev-standards §7 checks up and down on an empty database).
- **Collation:** default (as `Initial`). Duplicate-name checks run in the domain, not in the database.

## 5. Component design

### 5.1 Flow
```mermaid
sequenceDiagram
    participant Op as "Operator (Aspire dashboard / terminal)"
    participant Cmd as "Catalog.Api load-categories"
    participant H as "LoadCategoryTreeHandler"
    participant R as "JsonCategoryTreeSourceReader"
    participant D as "CategoryTreeDiff (domain)"
    participant DB as "Postgres catalogdb"
    Op->>Cmd: "start --source data/category-tree"
    Cmd->>DB: "migrate"
    Cmd->>H: "HandleAsync(source)"
    H->>R: "read + clean files"
    R-->>H: "CategoryTreeSource + file problems"
    H->>DB: "BEGIN; pg_advisory_xact_lock(tree)"
    H->>DB: "read current tree, categories with products"
    H->>D: "Compute(current, source, used)"
    D-->>H: "problems or change set"
    alt "problems"
        H->>DB: "ROLLBACK"
        H-->>Cmd: "Refused report"
    else "no change"
        H->>DB: "ROLLBACK"
        H-->>Cmd: "Unchanged report"
    else "changes"
        H->>DB: "apply rows, outbox CategoryTreeChanged; COMMIT"
        H-->>Cmd: "Applied report"
    end
    Cmd-->>Op: "report, exit 0 / 2"
```
Reads: `GET /v1/categories[?parentId]` → `ListCategoriesHandler` → `ICategoryReadStore.ListChildrenAsync`; `GET /v1/categories/{id}` → `GetCategoryHandler` → `ICategoryReadStore.GetDetailAsync`.

### 5.2 Domain (`Catalog.Domain/Categories`, pure)
- **`LocalizedName(Vi, En)`** value object.
- **`NameCleaner.Clean(string?)`** (BR-014-11):
  - NFC-normalizes.
  - Removes Unicode `Cc` and `Cf` characters (control, zero-width, BOM). A tab or newline inside a name becomes a space.
  - Collapses runs of whitespace and trims.
  - `null` or empty → missing.
- **Entities**, created through factories that take the file's integer key as `Id`:
  - `Category` (`Id`, `ParentId`, `Position`, `Name`, `IsLeaf`, `ImageUrl`; `Rename`, `Reposition`, `SetLeaf`, `SetImage`). `SetImage` accepts a value only when `ParentId` is null.
  - `AttributeDefinition` (`Id`, `Name`; `Rename`).
  - `AttributeValue` (`Id`, `AttributeId`, `Name`; `Rename`).
  - `CategoryAttribute` (`CategoryId`, `AttributeId`, `Position`, `IsRequired`, `Kind`, `AllowsMultiple`, `MaxValues`, `ValueIds`; `Loosen(...)`, `AppendValues(...)`, `Reorder(...)`).
- **`CategoryTreeSource`**: the parsed, cleaned file, as source records keyed by their integer id, in file order. A key that is not a whole number from 1 to 2,147,483,647 is a `*.key_invalid` problem. It is built by `CategoryTreeSource.Build(rawRecords)`, which reports the file-integrity problems (§3.5, first block) and does not throw.
- **`CategoryTreeDiff.Compute(CurrentTree current, CategoryTreeSource source, IReadOnlySet<int> categoriesWithProducts)`** returns `TreeDiffResult`: either `Problems` (every problem, not the first) or a `ChangeSet` (entities to add, renames, loosenings, reorders, leaf flips, new value lists, image changes) plus `LoadCounts`.
  - Top-level images: added, changed or removed per the file, never a problem except `category.image_invalid` (BR-014-6, BR-014-13); `imageUrl` below the top level is dropped while building the source (AC-014-5.4).
  - It applies BR-014-6/7 per §3.5, BR-014-2 (`IsLeaf` = no children in the source) and BR-014-3 (`Position` = index among siblings, attributes and values in file order).
  - It is pure, so all of AC-014-1.3/1.4/1.5 and 2.x are unit-testable.
  - Value-list rules per link:
    - The new list must contain every stored value id (else `value.removed`). Kept values take their file order, and new values are added where the file puts them.
    - A link that becomes `list` from `text`, or the reverse, is `attribute.tightened`. Text → list is refused too, because FR-CAT-019 lists "list ↔ free text" as tightening.
- **Attributes on non-leaf categories** are stored like any link (and the removal rules apply to them), but `IsLeaf = false` keeps them out of reads and out of FEAT-012 (D-5).

### 5.3 Application (`Catalog.Application/Features/Categories`)
- **`LoadCategoryTreeHandler.HandleAsync(string sourcePath, CancellationToken)`** returns `LoadReport {Outcome: Applied|Unchanged|Refused, LoadId, Counts, Problems}`. Steps:
  1. `ICategoryTreeSourceReader.ReadAsync(path)` returns raw records, or file problems.
  2. `CategoryTreeSource.Build`.
  3. `ICategoryTreeStore.BeginLoadAsync()` opens a transaction and takes `pg_advisory_xact_lock(CategoryTreeLock.Key)`.
  4. `LoadCurrentAsync()` returns the whole tree.
  5. `ICategoryUsage.CategoriesWithProductsAsync()`.
  6. `CategoryTreeDiff.Compute`.
  7. If there are problems or no change: dispose (rollback) and return the report.
  8. Otherwise `ApplyAsync(changeSet)`, `IEventOutbox` stage of `CategoryTreeChanged`, and commit.
  - File problems are reported together with the diff problems when the file could be parsed at all. A file that is unreadable stops at step 1 and is still reported in full (each file's errors).
- **New ports** in `Abstractions/Ports.cs`:
  - `ICategoryTreeSourceReader`.
  - `ICategoryTreeStore` (load unit of work).
  - `ICategoryUsage`, which FEAT-014's adapter answers with an empty set because there is no products table yet. FEAT-012 replaces it with a query on its products (§8).
  - `ICategoryReadStore` gains `ListChildrenAsync(int? parentId)` and `GetDetailAsync(int id)`; `ListAsync` is removed.
  - `IEventOutbox.WriteAsync` today saves immediately. A new `Stage<T>(…)` adds the row without saving, so the event commits with the load (ADR-0002). `WriteAsync` stays for Ping.
- **`ListCategoriesHandler`** (changed): `HandleAsync(int? parentId)` returns `Result<CategoryListDto>`. An unknown parent gives `common.not_found`. The probe stays as it is.
- **`GetCategoryHandler`** (new): `HandleAsync(int id)` returns `Result<CategoryDetailDto>`. It maps `en` blank → `vi` (FR-LOC-008).
- **`CategoryTreeEvents.Changed = "CategoryTreeChanged"`**; payload record `CategoryTreeChangedData(DateTimeOffset ChangedAt)`.
- **`TimeProvider`** is injected for `now`.

### 5.4 Infrastructure
- **`JsonCategoryTreeSourceReader`:** System.Text.Json `JsonDocument` per file (the largest Tiki chunk is 13 MB; the trimmed file is smaller).json` and the attribute files in name order. JSON errors are reported with file, line and byte position.
- **`EfCategoryTreeStore`:**
  - Uses one `CatalogDbContext` and `Database.BeginTransactionAsync()`, then `ExecuteSqlRaw("SELECT pg_advisory_xact_lock({0})", CategoryTreeLock.Key)`.
  - `LoadCurrentAsync` tracks entities so updates are diffs, `AddRange` inserts, and `SaveChangesAsync` uses EF batching.
  - Target: a full Tiki-size first load (6k categories, 55k links, 8k values) completes in under 2 minutes on the local stack (TAC-014-5).
- **`CategoryTreeLock.Key`:** a fixed `long` constant in Application (documented in §8 for FEAT-012).
- **`EmptyCategoryUsage : ICategoryUsage`** (FEAT-014 only).
- **`CatalogDbContext` mapping:**
  - `Category` with `LocalizedName` as a complex property (`NameVi`, `NameEn`). Remove `HasData` and `SkeletonCategory`.
  - `AttributeDefinition` → `attributes`; `AttributeValue` → `attribute_values`.
  - `CategoryAttribute` → `category_attributes`, with `ValueIds` as `List<int>` → `int[]` and `Kind` as a string with a check.
- **`CategoryReadStore`:** `AsNoTracking` queries.
  - Children are ordered by `Position`.
  - Detail loads the category, then its ancestors (a recursive CTE on `ParentId`, depth ≤ 8 in the source), then its links ordered by `Position` joined to attribute names, then all values whose id is in the links' arrays (one query), ordered per array.

### 5.5 Api and AppHost
- **`Program.cs`:** if `args` starts with `load-categories`, it builds the same host (`AddServiceDefaults`, `AddCatalogInfrastructure`) and runs migrations if enabled. It then resolves `LoadCategoryTreeHandler` in a scope, prints the report through `LoadReportPrinter`, and returns the exit code without mapping endpoints or starting Kestrel.
  - `--source` is required. A missing `--source` prints usage and exits 1.
  - The outbox relay hosted service is not started, so the run ends promptly. The event leaves the outbox the next time the Catalog service runs (ADR-0002 delivery is at-least-once, and delay is fine).
- **`CategoryEndpoints`:** `GET /v1/categories` reads `parentId` as a string and parses it (malformed → `404 common.not_found`). `GET /v1/categories/{id}` is named `getCategory`. Both use `.OrThrow()` then `Results.Ok`.
- **AppHost (`DotnetServices.cs`):**
  ```csharp
  builder.AddProject<Projects.Catalog_Api>("catalog-load-categories")
      .WithArgs("load-categories", "--source", <repo>/data/category-tree)
      .WithReference(catalogDb)
      .WaitFor(catalog)
      .WithExplicitStart()
  ```
  - It waits for `catalog` so that migrations ran. It gets no messaging reference, because the outbox row is relayed by the running Catalog. The operator clicks Start in the Aspire dashboard and reads the report in the resource's console log.
  - It is excluded from the deployed profile (dev-standards §3: heavy jobs stay out).

### 5.6 Web (storefront, wiring only)
`features/catalog/api.ts` keeps `fetchCategories()` (roots, no parameter) and the regenerated type `CategorySummary`. `CategoryBar.vue` is unchanged except for the type name and keys on `id`; it stays text-only, and FEAT-018 adds the images and placeholder to the home page (D-10). `App.spec.ts` mocks drop `slug`. There is no UI for the tree in this feature: FEAT-012 builds the picker and FEAT-018 builds browse.

## 6. Acceptance criteria mapping

| AC (from feature-spec.md) | Technical checks that prove it |
| :-- | :-- |
| AC-014-1.1 | Integration: on an empty DB, loading fixture `valid-v1` → report APPLIED; every category has `Id` = its file key, `NameVi`/`NameEn`, `ParentId` of its file parent and `Position` = sibling index; every attribute, link and value exists with its names and file order; one `CategoryTreeChanged` outbox row. Contract: `GET /v1/categories` returns the roots in file order; `GET /v1/categories/{leaf}` returns its attributes and values. |
| AC-014-1.2 | Unit: `LoadCounts` for a diff with known adds, renames, loosenings and unchanged items equals the expected table. Integration: the printed report for `valid-v1` then `v2-grow` shows those counts. |
| AC-014-1.3 | Unit: a source where a category lacks `en`, an attribute lacks `vi` and a value has `en: "  "` → three `name.missing` problems, each naming kind, key and owner (attribute's category, value's attribute and category). Integration: tables unchanged, exit code 2, no outbox row. |
| AC-014-1.4 | Unit: one source with a removed category, a moved category, a removed attribute, a removed value, a tightened attribute and a duplicate value name → six problems, all listed. Integration: nothing changes. |
| AC-014-1.5 | Integration: loading `valid-v1` twice → second report UNCHANGED with every count in "unchanged", no change to any row, and still exactly one `CategoryTreeChanged` outbox row. |
| AC-014-2.1 | Integration: `valid-v1` then `v2-grow` (new category, new link, new value) → APPLIED; the new rows exist; a second outbox row. |
| AC-014-2.2 | Unit + integration: renaming a category, an attribute and a value under the same key changes only `NameVi`/`NameEn`; `Id`, `ParentId`, `Position`, links and `ValueIds` are unchanged. |
| AC-014-2.3 | Unit: required → optional, single → multiple (max 5), max 5 → 8 each counted as loosened and applied; integration shows the stored flags. |
| AC-014-2.4 | Unit: each of omitted category, moved category, omitted link, omitted value, optional → required, multiple → single, max 5 → 3, list → text, text → list → its own §3.5 problem; integration: the whole load is refused and nothing changes. |
| AC-014-2.5 | Unit with `categoriesWithProducts = {leaf}`: a child under that leaf → `category.child_under_used_leaf` naming the leaf. End-to-end once FEAT-012 implements `ICategoryUsage` (§8). |
| AC-014-2.6 | Unit + integration: a child under a leaf without products → APPLIED; the old leaf has `IsLeaf = false`, `GET /v1/categories/{old}` shows `isLeaf: false` and `attributes: []`; the child is a leaf. |
| AC-014-2.7 | Unit with `categoriesWithProducts = {leaf}`: a new required link → `attribute.required_on_used_category`; the same link as optional → applied. End-to-end once FEAT-012 lands (§8). |
| AC-014-3.1 | Contract: `GET /v1/categories` items each carry `name.vi` and `name.en` equal to the seeded names (the client shows the one for its language, as `CategoryBar` does). |
| AC-014-3.2 | Contract: children of a seeded parent come back ordered by `position`, which is the seeded file order, with the same order whatever `Accept-Language` is sent. |
| AC-014-3.3 | Contract: each item has `isLeaf`; a leaf's `GET /v1/categories?parentId={leaf}` returns `[]`. FEAT-012 rejects a non-leaf product category (its design). |
| AC-014-3.4 | Unit: the mapper given a stored blank `en` returns `en = vi` in both `CategorySummary` and `CategoryDetail`. |
| AC-014-4.1 | Contract: `GET /v1/categories/{leaf}` → each attribute has `name {vi,en}`, `required`, `kind`. |
| AC-014-4.2 | Contract: a list attribute has `selection` (`single`/`multiple`), `maxValues` (1, or the maximum when multiple), and `values` with `name {vi,en}` in seeded order. |
| AC-014-4.3 | Unit: a source attribute with `values: []` → link `Kind = text`. Contract: it is returned with `kind: "text"`, `selection: null`, `maxValues: null`, `values: []`. |
| AC-014-4.4 | Contract: a leaf with no links → `attributes: []`. |
| AC-014-4.5 | Unit: a value `{vi: "Samsung", en: "Samsung"}` is accepted (no problem). Contract: returned with identical `vi` and `en`. |
| AC-014-5.1 | Integration: a top-level category with `imageUrl` in `valid-v1` has it stored. Contract: `GET /v1/categories` returns that `imageUrl` on the category. The image on the home page is checked by FEAT-018 (§8). |
| AC-014-5.2 | Contract: a top-level category with no image returns `imageUrl: null`, which the contract defines as "show the placeholder" (D-10). The placeholder on screen is checked by FEAT-018 (§8). |
| AC-014-5.3 | Unit: a changed link and a left-out link on top-level categories → image changes, no problem, counted in `images`. Integration: `valid-v1` then `v2-images` → APPLIED; the new link is stored and the left-out one is `null`. |
| AC-014-5.4 | Unit: an `imageUrl` on a non-top-level category is dropped and raises no problem. Contract: children returned by `?parentId=` have `imageUrl: null`. |

Technical criteria:
- **TAC-014-1 (all-or-nothing, FR-CAT-019, ADR-0002).** Given a valid change set, when the apply fails midway (a test store that throws after the first `SaveChanges` batch), then no category, attribute, link or outbox row from the load is committed.
- **TAC-014-2 (event, ADR-0002, dev-standards §7).** Given an applied load, then exactly one outbox row `CategoryTreeChanged` v1, producer `catalog`, whose `data` validates against `contracts/events/CategoryTreeChanged/v1.schema.json` Given a refused or unchanged load, there is no row.
- **TAC-014-3 (serialized loads).** Given two loads started at once on the same DB, then they run one after the other (advisory lock): the second sees the first's result (UNCHANGED for the same file) and exactly one outbox row is written.
- **TAC-014-4 (cleaning, BR-014-11).** Given names with `\u0000`–`\u001F`, `​`, `﻿`, surrounding spaces and decomposed Vietnamese (NFD), then they are stored cleaned and in NFC. Two values `"Đỏ"` and `" đỏ​"` in one list → `value.duplicate_name`.
- **TAC-014-5 (load time, NFR-REL-007).** Given the full Tiki-size source (6,101 categories, 54,992 links), a first load on the local stack completes in under 2 minutes. This is measured once and recorded in the acceptance report.
- **TAC-014-6 (read latency, NFR-PERF-001).** With the full tree loaded, 95% of `GET /v1/categories?parentId=…` and `GET /v1/categories/{leaf}` (the leaf with the most attributes) complete within 300 ms at 50 concurrent users on the local stack. Acceptance verification measures this.
- **TAC-014-7 (contract hygiene, dev-standards §6, ADR-0025).** Spectral shows 0 errors on `catalog.yaml`; `listCategories` and `getCategory` each have `@operation` scenarios covering 200 and 404; the regenerated client compiles in storefront lint, typecheck and tests.
- **TAC-014-8 (boundaries).** `Catalog.ArchitectureTests` stay green: `CategoryTreeDiff` and `NameCleaner` live in Domain with no framework reference, and System.Text.Json and EF are used only in Infrastructure.

## 7. Decisions

**D-1 Catalog stores its data in PostgreSQL.**
- Driver: user decision 2026-11-25 on ADR-0018's open Catalog store. The tree is relational (parent links, unique keys); FEAT-012 must check product attributes and write the outbox in one transaction; the skeleton already runs EF and Npgsql with Testcontainers.
- Rejected: MongoDB (new outbox, fixtures and migration story); one jsonb document per leaf (weak per-value checks).
- Consequence: ADR-0018 gets an amendment recording Catalog = PostgreSQL (§8).

**D-2 Global attribute and value definitions, per-category links with an ordered `int[]` of allowed values.**
- Driver: survey data. Keys and names are global, but required and value lists differ per category for 18 and 21 attributes. A rename under one key is one change (AC-014-2.2).
- Rejected: per-category copies of attributes (a rename touches thousands of rows, and names drift); a link row per category value (710k rows).
- Consequence: FEAT-012 validates a product's value with `= ANY(ValueIds)`.

**D-3 The loader is a Catalog.Api subcommand, started from a manual-start Aspire resource.**
- Driver: the loader needs Catalog's domain, DbContext and outbox, and nothing exists for commands yet. The user had no preference, so the recommended option was taken. The same image serves as a cloud job.
- Rejected: a `tools/seed` console (would reference Catalog's projects from outside the service); an admin HTTP endpoint (no operator role, and a 100 MB upload).
- Consequence: diverges from dev-standards §2 "seed under tools/". FEAT-039's product seed can still live in `tools/seed` and call this command first.

**D-4 The tree is read one level at a time, with both languages returned.**
- Driver: ~6,100 categories (≈ 1 MB as one payload) against roughly 40 roots and small sibling sets. The codebase convention is `LocalizedText {vi, en}` chosen by the client (`CategoryBar`), so the existing root call keeps working.
- Rejected: a whole-tree endpoint (large payload on every picker open); server-side language selection (a second convention, and it breaks `CategoryBar`).
- Consequence: the lists are not paged (bounded per level). Picker search is FEAT-012's to add.

**D-5 Attributes on a non-leaf are stored and kept, but never exposed or applied.**
- Driver: feature-spec §7 asks. The source has 8 such categories, and their children have no attributes of their own. BR-014-10 says only a leaf's attributes count, and BR-014-7 refuses removing an attribute, so a leaf that gains children (AC-014-2.6) keeps its links in the file.
- Rejected: refusing attributes on non-leaves (it would make AC-014-2.6 impossible without breaking the removal rule); inheriting them into children (contradicts BR-014-10).
- Consequence: the report shows them as ordinary links, and the owner may simply leave them.

**D-6 Thin `CategoryTreeChanged` with no version, and no tree-state table.**
- Driver: message size; and since the event carries no tree data and consumers read the current tree, ordering does not matter, so ADR-0002's snapshot version is not needed (user agreed 2026-11-26 to drop the `category_tree` table).
- Rejected: the full tree, or per-node events (thousands of messages per load); a `category_tree` row with version, load time and file fingerprint (no requirement needs them).
- Consequence: Search-AI pulls the tree when it gets the event and on first start (FEAT-018, §8).

**D-7 Advisory transaction lock on the tree.**
- Driver: TAC-014-3, and FEAT-012 races: a product created in a leaf while a load gives that leaf a child or a required attribute.
- Rejected: row locks on each changed category (the load reads the whole tree anyway); `SERIALIZABLE` (retries on a 100k-row write).
- Consequence: FEAT-012's product create must take `pg_advisory_xact_lock_shared(CategoryTreeLock.Key)` before checking the category (§8).

**D-8 Drop the skeleton slug and row.**
- Driver: no requirement needs a slug. The source has none. The skeleton row's key is not in the file, so it would read as a removed category on the first load.
- Rejected: generating slugs from vi names (FEAT-018 decides browse URLs).
- Consequence: breaking change to the skeleton contract, its `.feature`, the generated client and `MigrationTests`, all updated in this feature (tasks T1, T2, T7).

**D-9 Category, attribute and value ids are the Tiki integer keys.**
- Driver: user decision 2026-11-26, "use the id from tiki to make sure everything can be mapped". BR-014-5 already makes the file key the stable identity, so every row maps back to the Tiki source with no lookup table.
- Rejected: UUID v7 with a `SourceKey` column (a second identity to keep in step, and every log or problem would need both).
- Consequence: an exception to ADR-0009 for platform reference data; the ADR's concern (opaque ids, no auto-increment) does not apply, because these ids are assigned in the file and the tree is public anyway. Keys are never reused or changed. A category the owner adds that has no Tiki id needs a number that cannot clash with Tiki's (for example from 100,000,000 up, recorded in `SOURCE.md`). Products, shops and all other entities keep UUID v7.

**D-10 Category images are external links; the placeholder belongs to the client.**
- Driver: FR-CAT-021 and BR-014-12/13 (user decision 2026-11-26: Tiki's links as given, top level only, placeholder when missing). Catalog serves no files, and the placeholder is a visual asset of the design system.
- Rejected: copying the images into HiveSpace storage (the Media service is FEAT-035, and the owner chose links); Catalog returning a placeholder URL (couples the API to a front-end asset path).
- Consequence: the API returns `imageUrl` or `null`; FEAT-018 renders the image or the placeholder and falls back to the placeholder when an image fails to load. The visible part of AC-014-5.1/5.2 is checked there (§8).

## 8. Escalations & open items
- **Architecture amendment (ADR-0018):** record "Catalog: PostgreSQL, relational tables, decided 2026-11-25 in FEAT-014" and close the open item for Catalog. The user made the decision; the ADR edit belongs to the architecture owner (software-architecture amendment).
- **Architecture amendment (ADR-0009):** add the exception of D-9 (platform reference data from the category file keeps its integer source keys). Owner: architecture.
- **FEAT-012 obligations** (cite this section in its design):
  - Store the product's `CategoryId` and attribute value ids as `int` (D-9).
  - Implement `ICategoryUsage` over its products table and replace `EmptyCategoryUsage`.
  - Take `pg_advisory_xact_lock_shared(CategoryTreeLock.Key)` in product create before reading the category.
  - Accept only `IsLeaf` categories.
  - Validate values against `ValueIds` and `MaxValues`.
  - Until then, AC-014-2.5 and AC-014-2.7 are proved at unit level with a fake usage set; their end-to-end check moves with FEAT-012.
- **FEAT-018 obligations:**
  - Subscribe to `CategoryTreeChanged`.
  - Read the tree through a Catalog read path it designs (gRPC per ADR-0019).
  - Read it once on start, because a subscription created after the first load misses that event (same issue FEAT-010 §8 noted).
- **The owner's final file is pending** (feature-spec §5). Implementation uses test fixtures and a Tiki-size conversion for TAC-014-5. `data/category-tree/` holds `SOURCE.md` and the format README until the file arrives. A reminder for the owner: a leaf that gains children keeps its attributes in the file (D-5), and lists must not drop values once loaded.
- **Repository layout:** `data/category-tree/` at the repo root is new. Suggest repo-structure.md add `data/` ("operator data files committed with the code").
- **Requirements:** UC-020 has no exception flow for a refused load. Suggest the requirements owner add "2a. The file breaks a loading rule: nothing changes and each problem is listed (FR-CAT-019)". Not blocking.
- **Value list size:** the Tiki source has lists of up to 1,000 values, which makes `getCategory` responses large (worst case about 1 MB). The owner's trimmed file should keep lists reasonable. If not, FEAT-012 may need a value search; TAC-014-6 will show it.
- **FEAT-018 obligations (images):** show each top-level category's `imageUrl` on the home page category bar, and the placeholder (a design-system asset, ui-design's to choose) for `null` or when the image fails to load. The images are served by Tiki's servers, so a failed image must never break the page (NFR-REL-008). There is no Content-Security-Policy today; if one is added, `img-src` must allow `https://salt.tikicdn.com`. FEAT-018 is also where AC-014-5.1 and 5.2 are checked on screen.
- **Plan:** the implementation plan's FEAT-014 and FEAT-018 rows do not list FR-CAT-021 yet (implementation-planning to add).
- **E2E:** the architecture names no critical E2E flows that include UC-020 or UC-005 step 1, and FEAT-014 has no screen, so tasks.md has no E2E task.
