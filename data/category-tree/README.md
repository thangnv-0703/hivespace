# data/category-tree

Input for `Catalog.Api load-categories --source data/category-tree` (see `docs/features/FEAT-014-category-tree-with-attributes/technical-design.md` §3.4).

| File | Content |
| :-- | :-- |
| `categories.json` | `{categories: [{externalCategoryId, externalParentCategoryId \| null, name: {vi, en}, imageUrl \| null}]}`. Keys are Tiki numeric ids (strings or numbers). Array order is sibling order. A child may appear before its parent. `imageUrl` counts only for top-level categories (absolute `https`, at most 500 characters). |
| `category-attributes/*.json` | `{categories: [{externalCategoryId, attributes: [{sourceAttributeId, name: {vi, en}, isRequired, inputType: "Dropdown" \| "Checkbox", maxValueCount, values: [{sourceValueId, name: {vi, en}}]}]}]}`. Read in file-name order. |
| `SOURCE.md` | Where the data comes from and the owner's acceptance. |

Mapping: `Dropdown` is single choice (max 1); `Checkbox` is multiple choice (max = `maxValueCount`, at least 2); no `values` means free text.

The sample covers: 3 roots (2 with an image, 1 without), 3 levels, a child listed before its parent (`8062` before `8061`), required and optional attributes, single, multiple and free-text attributes, and attributes and values shared by several categories.
