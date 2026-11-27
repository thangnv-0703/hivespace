# Category tree source

**Status: the owner's category file (FEAT-014), adapted from Tiki's seller category taxonomy.** The old hand-made sample now lives in [`test/`](test/).

## Origin (NFR-LEG-002, BR-014-12)
- **Categories and attributes:** Tiki SellerCenter's category taxonomy (categories under parent 2, and each category's select and multiselect attributes with their options), crawled on 2026-11-25/26 into `references/hivespace.crawler/crawler-state/tiki-v2`.
- **Category images:** links to Tiki's own top-level category images on `salt.tikicdn.com`, as the crawl gave them.
- **Owner's acceptance:** the owner has checked and accepts using the Tiki taxonomy and linking Tiki's top-level category images in this project (feature-spec BR-014-12, Q13, Q20, Q23).

## How the file was made
Produced by `python -m hivespace_crawler tiki export-category-tree`; the rules are in `references/hivespace.crawler/PLAN-feat014-crawler-gaps.md`.
- **Filtering (BR-014-1):** Tiki-only groups and operational attributes were dropped, and "Nhà Sách Tiki" became "Nhà Sách". The lists are in `references/hivespace.crawler/export-filters.json`.
- **Cleaning (BR-014-11):** names were cleaned like the loader's `NameCleaner`. When an option name repeated within one list, the first id in Tiki's order was kept.
- **Open lists (BR-014-8, BR-014-14):** 8 lists accept other values, each with at most 1,000 options sorted A–Z.
  - Brand (603) offers the brands found on the most crawled Tiki products.
  - The other 7 offer Tiki's crawled options.
- **English names (BR-014-4):** machine-translated with Claude Haiku 4.5, cached in `references/hivespace.crawler/translations/en.json`. Brand, author, publisher, distributor and supplier options are names, so their English copies the Vietnamese. Wrong English is fixed later by renaming.

## Ids (D-9)
- Category, attribute and option ids are Tiki's integer ids.
- **Owner-added ids start at 100,000,000.** Today they are 984 brand options (100000000–100000983) that the crawled Tiki list did not contain. `owner-ids.json` records them (brand name → id) and must be kept with this folder, so every re-export gives the same brand the same id.
- A category the owner adds by hand takes an id from the same range.

## Changing the file
Once loaded, nothing may be removed or tightened (BR-014-7). Re-export with the same command: it keeps every option already in `attributes.json` and only adds.
