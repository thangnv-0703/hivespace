# data/category-tree

Input for `Catalog.Api load-categories --source data/category-tree`, in the v2 layout (`docs/features/FEAT-014-category-tree-with-attributes/technical-design.md` §3.4, D-13). It's the owner's Tiki-derived tree; see [`SOURCE.md`](SOURCE.md). The hand-made 17-category sample used for development is in [`test/`](test/).

| File | Content |
| :-- | :-- |
| `categories.json` | `{categories: [{externalCategoryId, externalParentCategoryId \| null, name: {vi, en}, imageUrl \| null}]}`. 5,266 categories, 25 at the top level. Array order is sibling order. `imageUrl` counts only for top-level categories. |
| `attributes.json` | `{attributes: [{sourceAttributeId, name: {vi, en}, inputType: "Dropdown" \| "Checkbox", maxValueCount, allowsCustomValue, values: [{sourceValueId, name: {vi, en}}]}]}`. 349 attributes, each defined once, with 12,596 options in display order. 8 open lists have `allowsCustomValue: true`. |
| `category-attributes/*.json` | `{categories: [{externalCategoryId, attributes: [{sourceAttributeId, isRequired}]}]}`. These are links only, in display order. A category with no entry inherits its nearest ancestor's attributes (FR-CAT-023). |
| `owner-ids.json` | Owner-added option ids from 100,000,000 up, keyed by attribute and cleaned name. Not read by the loader; the export needs it for stable ids. |
| `SOURCE.md` | Where the data comes from, the owner's acceptance, and the id rules. |

Mapping: `Dropdown` is single choice (max 1); `Checkbox` is multiple choice (max = `maxValueCount`, at least 2); no `values` means free text.

A reload may add and rename, and may loosen: required → optional, single → multiple, a higher maximum, or a list that starts accepting other values. It is refused if it removes or moves anything, or tightens an attribute, including a list that stops accepting other values.

**First real load:** reset the local Catalog database first if it holds the sample, because it would refuse this file (`category.removed`).
