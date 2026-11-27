# data/category-tree

Input for `Catalog.Api load-categories --source data/category-tree` (see `docs/features/FEAT-014-category-tree-with-attributes/technical-design.md` §3.4). The loader reads the v2 layout only: each attribute is defined once in `attributes.json`, and the category files hold links only (D-13).

| File | Content |
| :-- | :-- |
| `categories.json` | `{categories: [{externalCategoryId, externalParentCategoryId \| null, name: {vi, en}, imageUrl \| null}]}`. Keys are Tiki numeric ids (strings or numbers). Array order is sibling order. A child may appear before its parent. `imageUrl` counts only for top-level categories (absolute `https`, at most 500 characters). |
| `attributes.json` | `{attributes: [{sourceAttributeId, name: {vi, en}, inputType: "Dropdown" \| "Checkbox", maxValueCount, allowsCustomValue (optional, default false), values: [{sourceValueId, name: {vi, en}}]}]}`. Each attribute once, with its values in display order. Without this file there are no attributes. |
| `category-attributes/*.json` | `{categories: [{externalCategoryId, attributes: [{sourceAttributeId, isRequired}]}]}`. Links only, in display order; each must name an attribute in `attributes.json`. Read in file-name order. Other fields on a link (`name`, `values`, `inputType`) are ignored. |
| `SOURCE.md` | Where the data comes from and the owner's acceptance. |

Mapping:
- `Dropdown` is single choice (max 1); `Checkbox` is multiple choice (max = `maxValueCount`, at least 2).
- No `values` means free text; `inputType`, `maxValueCount` and `allowsCustomValue` are then ignored.
- `allowsCustomValue: true` on a list makes it an open list: a seller picks a value or types their own text (FR-CAT-022).
- `isRequired` is set per category link; everything else is set once on the attribute.
- A category with no entry has no attributes of its own. A leaf without attributes uses those of its nearest ancestor that has some (FR-CAT-023).

A reload may add and rename, and may loosen: required → optional, single → multiple, a higher maximum, or a list that starts accepting other values. It is refused if it removes or moves anything, or tightens an attribute, including a list that stops accepting other values.

The sample covers: 3 roots (2 with an image, 1 without), 3 levels, a child listed before its parent (`8062` before `8061`), required and optional attributes, single, multiple and free-text attributes, and attributes and values shared by several categories.
