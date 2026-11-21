# Requirements Traceability Matrix

> Source: docs/srs.md v0.16, docs/use-cases.md · Last updated: 2026-11-16
> Design, Plan, and Test columns are filled by downstream phases as they produce their artifacts (append, never overwrite).

| Req ID | Requirement (short) | Priority | Status | Source | Use case(s) | Design ref | Plan ref | Test ref |
| :----- | :------------------ | :------- | :----- | :----- | :---------- | :--------- | :------- | :------- |
| FR-AUTH-001 | a visitor submits registration with a valid email address, a display name and a … | Must | Active | P0-1, US-17 | UC-001 | ADR-0008, features/FEAT-001-register-and-sign-in/technical-design.md, features/FEAT-001-register-and-sign-in/ui-design.md | FEAT-001 | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-AUTH-002 | the submitted email address is already registered, then reject the registration … | Must | Active | P0-1 | UC-001 | ADR-0008, features/FEAT-001-register-and-sign-in/technical-design.md, features/FEAT-001-register-and-sign-in/ui-design.md | FEAT-001 | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-AUTH-003 | the submitted password does not satisfy the password policy, then reject the reg… | Must | Active | P0-1 | UC-001, UC-026 | ADR-0008, features/FEAT-001-register-and-sign-in/technical-design.md, features/FEAT-001-register-and-sign-in/ui-design.md | FEAT-001, FEAT-003 | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-AUTH-004 | a registration field is missing or malformed, then reject the request and identi… | Must | Active | P0-1 | UC-001 | ADR-0008, features/FEAT-001-register-and-sign-in/technical-design.md, features/FEAT-001-register-and-sign-in/ui-design.md | FEAT-001 | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-AUTH-005 | a user opens a valid verification link, mark the account's email as verified. | Must | Active | P0-14, US-17 | UC-001 | ADR-0008, ADR-0012, features/FEAT-001-register-and-sign-in/technical-design.md, features/FEAT-001-register-and-sign-in/ui-design.md | FEAT-001 | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-AUTH-006 | a verification link is expired, already used, or invalid, then reject it and off… | Must | Active | P0-14 | UC-001 | ADR-0008, features/FEAT-001-register-and-sign-in/technical-design.md, features/FEAT-001-register-and-sign-in/ui-design.md | FEAT-001 | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-AUTH-007 | a user with an unverified account requests a new verification email, issue a new… | Should | Active | Inferred | UC-001 | ADR-0008, features/FEAT-001-register-and-sign-in/technical-design.md, features/FEAT-001-register-and-sign-in/ui-design.md | FEAT-001 | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-AUTH-008 | a user's account is unverified, allow browsing and search but reject order place… | Must | Active | P0-14 | UC-001, UC-011 | ADR-0008, features/FEAT-001-register-and-sign-in/technical-design.md, features/FEAT-001-register-and-sign-in/ui-design.md | FEAT-001, FEAT-022 | features/FEAT-001-register-and-sign-in/acceptance-report.md (partial) |
| FR-AUTH-009 | a user submits correct credentials, sign the user in and issue an access token a… | Must | Active | P0-1 | UC-002 | ADR-0008, features/FEAT-001-register-and-sign-in/technical-design.md, features/FEAT-001-register-and-sign-in/ui-design.md | FEAT-001 | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-AUTH-010 | the submitted credentials are incorrect, then reject the sign-in with a message … | Must | Active | P0-1 | UC-002 | ADR-0008, features/FEAT-001-register-and-sign-in/technical-design.md, features/FEAT-001-register-and-sign-in/ui-design.md | FEAT-001 | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-AUTH-011 | five consecutive sign-in attempts for one account fail within 15 minutes, then l… | Should | Active | Inferred | UC-002 | ADR-0008 | FEAT-005 | _TBD_ |
| FR-AUTH-012 | an account is locked, reject sign-in attempts without evaluating the submitted p… | Should | Active | Inferred | UC-002 | ADR-0008 | FEAT-005 | _TBD_ |
| FR-AUTH-013 | a signed-in user signs out, revoke that user's refresh token. | Should | Active | Inferred | UC-002 | ADR-0008, features/FEAT-001-register-and-sign-in/technical-design.md, features/FEAT-001-register-and-sign-in/ui-design.md | FEAT-001 | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-AUTH-014 | a valid, unexpired refresh token is presented, issue a new access token. | Should | Active | Inferred | UC-002 | ADR-0008, features/FEAT-001-register-and-sign-in/technical-design.md | FEAT-001 | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-AUTH-015 | a request to a protected endpoint carries a missing, malformed, expired or invalid-signature access token… | Must | Active | P0-1, CR-3 | UC-002 | ADR-0008, features/FEAT-001-register-and-sign-in/technical-design.md | Foundations | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-AUTH-016 | a request carries a valid token, the gateway shall forward the request with the … | Must | Active | P0-1 | UC-002 | ADR-0008, features/FEAT-001-register-and-sign-in/technical-design.md | Foundations | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-AUTH-017 | a user requests a password reset for an email address, send a reset link if an a… | Could | Active | P1-6 | UC-022 | ADR-0008 | FEAT-007 | _TBD_ |
| FR-AUTH-018 | a user submits a new compliant password through a valid reset link, update the p… | Could | Active | P1-6 | UC-022 | ADR-0008 | FEAT-007 | _TBD_ |
| FR-AUTH-019 | a password-reset link is expired, already used, or invalid, then reject it and o… | Could | Active | P1-6 | UC-022 | ADR-0008 | FEAT-007 | _TBD_ |
| FR-AUTH-020 | a visitor completes Google sign-in with a verified Google email address that mat… | Must | Active | CR-1 | UC-025 | ADR-0008 | FEAT-004 | _TBD_ |
| FR-AUTH-021 | a visitor completes Google sign-in with a verified Google email address that mat… | Must | Active | CR-1 | UC-025 | ADR-0008 | FEAT-004 | _TBD_ |
| FR-AUTH-022 | Google reports the email as unverified, the user denies consent, or the Google r… | Must | Active | CR-1 | UC-025 | ADR-0008 | FEAT-004 | _TBD_ |
| FR-AUTH-023 | Google is unavailable, then tell the user and keep email-and-password sign-in av… | Must | Active | CR-1 | UC-025 | ADR-0008 | FEAT-004 | _TBD_ |
| FR-AUTH-024 | a signed-in user submits the correct current password and a new password that sa… | Must | Active | CR-1 | UC-026 | ADR-0008 | FEAT-003 | _TBD_ |
| FR-AUTH-025 | the current password submitted for a password change is incorrect, then reject t… | Must | Active | CR-1 | UC-026 | ADR-0008 | FEAT-003 | _TBD_ |
| FR-AUTH-026 | an email-and-password sign-in is attempted for an account that has no password, … | Must | Active | CR-1 | UC-002 | ADR-0008 | FEAT-004 | _TBD_ |
| FR-AUTH-027 | a user whose account has no password signs in with Google and submits a password… | Should | Active | CR-1, Inferred | UC-026 | ADR-0008 | FEAT-005 | _TBD_ |
| FR-AUTH-028 | a visitor requests a sign-in code for an email address that belongs to an account… | Should | Active | CR-7 | UC-029 | ADR-0008 | FEAT-006 | _TBD_ |
| FR-AUTH-029 | a visitor requests a sign-in code, give the same response whether or not an acco… | Should | Active | CR-7 | UC-029 | ADR-0008 | FEAT-006 | _TBD_ |
| FR-AUTH-030 | a user submits the correct code for an active challenge before it expires, sign … | Should | Active | CR-7 | UC-029 | ADR-0008 | FEAT-006 | _TBD_ |
| FR-AUTH-031 | the submitted code is wrong or expired, or the challenge has ended, then reject t… | Should | Active | CR-7 | UC-029 | ADR-0008 | FEAT-006 | _TBD_ |
| FR-AUTH-032 | five wrong codes are submitted for one challenge, then end the challenge and req… | Should | Active | CR-7 | UC-029 | ADR-0008 | FEAT-006 | _TBD_ |
| FR-AUTH-033 | a sign-in code is requested less than 60 seconds after the previous request, the… | Should | Active | CR-7 | UC-029 | ADR-0008 | FEAT-006 | _TBD_ |
| FR-AUTH-034 | a new sign-in code is issued for an account, invalidate every earlier code for th… | Should | Active | CR-7 | UC-029 | ADR-0008 | FEAT-006 | _TBD_ |
| FR-AUTH-035 | an account is locked, reject code sign-in without evaluating the submitted code. | Should | Active | CR-7 | UC-029 | ADR-0008 | FEAT-006 | _TBD_ |
| FR-AUTH-036 | a user whose account is unverified signs in with a correct code, mark the email … | Should | Active | CR-7 | UC-029 | ADR-0008 | FEAT-006 | _TBD_ |
| FR-ROLE-001 | support the roles Buyer and Seller, and every registered user shall hold the Buy… | Must | Active | P0-1 | UC-001 | ADR-0008, features/FEAT-001-register-and-sign-in/technical-design.md | FEAT-001 | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-ROLE-002 | a buyer completes shop registration, grant the Seller role while retaining the B… | Must | Active | P0-1, US-11 | UC-003 | ADR-0001, ADR-0008 | FEAT-009 | _TBD_ |
| FR-ROLE-003 | a user without the Seller role requests a seller-only function, then reject the … | Must | Active | P0-1 | UC-003, UC-005, UC-014 | ADR-0008 | FEAT-009 | _TBD_ |
| FR-ROLE-004 | a user requests to view or change a cart, order, address, shop or product owned … | Must | Active | Inferred | UC-004, UC-006, UC-013, UC-014, UC-028 | ADR-0008 | Foundations | _TBD_ |
| FR-ROLE-005 | evaluate authorization on the server for every protected request. | Must | Active | Inferred | UC-002 | ADR-0008 | Foundations | _TBD_ |
| FR-SHOP-001 | a verified buyer submits shop registration with a shop name and a seller type (i… | Must | Active | P0-1, US-11 | UC-003 | _TBD_ | FEAT-009 | _TBD_ |
| FR-SHOP-002 | a user who already owns a shop submits shop registration, then reject it. | Should | Active | Inferred | UC-003 | _TBD_ | FEAT-009 | _TBD_ |
| FR-SHOP-003 | the submitted shop name is already used by another shop, then reject it. | Should | Active | Inferred | UC-003 | _TBD_ | FEAT-009 | _TBD_ |
| FR-SHOP-004 | record the seller type without verifying it. | Must | Active | PRD §3 | UC-003 | _TBD_ | FEAT-009 | _TBD_ |
| FR-SHOP-005 | a shop owner submits shop profile changes (name, description), save them. | Must | Active | P0-2, US-12 | UC-004 | _TBD_ | FEAT-010 | _TBD_ |
| FR-SHOP-006 | a shop owner submits a pickup address (contact name, phone, province, district, … | Must | Active | P0-2, US-12 | UC-004 | _TBD_ | FEAT-010 | _TBD_ |
| FR-SHOP-007 | a shop owner submits payout bank details (bank name, account number, account hol… | Must | Active | P0-2, US-12 | UC-004 | _TBD_ | FEAT-010 | _TBD_ |
| FR-SHOP-008 | required shop fields are missing or invalid, then reject the submission and iden… | Must | Active | P0-2 | UC-003, UC-004 | _TBD_ | FEAT-010 | _TBD_ |
| FR-SHOP-009 | any user opens a shop page, show the shop name, logo, cover image, description, … | Must | Active | P0-2, US-10 | UC-004, UC-009 | ADR-0011 | FEAT-011 | _TBD_ |
| FR-SHOP-010 | a shop owner opens shop settings, show the profile (including logo and cover ima… | Must | Active | P0-2 | UC-004 | _TBD_ | FEAT-010 | _TBD_ |
| FR-SHOP-011 | a seller attempts to publish a product before the shop has a pickup address and … | Should | Active | Inferred, US-12 | UC-004, UC-005 | _TBD_ | FEAT-012 | _TBD_ |
| FR-ADDR-001 | a buyer submits a delivery address (recipient name, phone, province, district, w… | Should | Active | Inferred | UC-011 | _TBD_ | FEAT-008 | _TBD_ |
| FR-ADDR-002 | a buyer edits or deletes a saved address, apply the change without altering the … | Should | Active | Inferred | UC-011 | _TBD_ | FEAT-008 | _TBD_ |
| FR-ADDR-003 | a buyer marks an address as default, preselect it at checkout. | Could | Active | Inferred | UC-011 | _TBD_ | FEAT-008 | _TBD_ |
| FR-ADDR-004 | an address field is missing or invalid, then reject the submission and identify … | Should | Active | Inferred | UC-011 | _TBD_ | FEAT-008 | _TBD_ |
| FR-CAT-001 | a seller submits a new product with title, description, category, attribute valu… | Must | Active | P0-3, US-13 | UC-005 | _TBD_ | FEAT-012 | _TBD_ |
| FR-CAT-002 | represent each product as one or more variants (SKUs), each with option values (… | Must | Active | P0-3, US-2 | UC-005 | ADR-0010 | FEAT-012 | _TBD_ |
| FR-CAT-003 | provide a predefined category tree from which the seller selects exactly one lea… | Must | Active | P0-3 | UC-005 | _TBD_ | FEAT-014 | _TBD_ |
| FR-CAT-004 | a category defines attributes, require the seller to enter values for the attrib… | Must | Active | P0-3 | UC-005 | _TBD_ | FEAT-012 | _TBD_ |
| FR-CAT-005 | product data is invalid (missing title, price not a positive amount valid for it… | Must | Active | P0-3 | UC-005 | ADR-0010 | FEAT-012 | _TBD_ |
| FR-CAT-006 | a seller submits changes to their own product, save them. | Must | Active | P0-3, US-13 | UC-006 | _TBD_ | FEAT-013 | _TBD_ |
| FR-CAT-007 | a seller sets the stock quantity of a variant, update the variant's on-hand stoc… | Must | Active | P0-3, US-13 | UC-005, UC-006 | _TBD_ | FEAT-013 | _TBD_ |
| FR-CAT-008 | a seller deactivates a product, hide it from browse and search and prevent new c… | Should | Active | Inferred | UC-006 | _TBD_ | FEAT-013 | _TBD_ |
| FR-CAT-009 | a seller reactivates a deactivated product, show it in browse and search again. | Should | Active | Inferred | UC-006 | _TBD_ | FEAT-013 | _TBD_ |
| FR-CAT-010 | a seller opens the product list, show the seller's own products with status, var… | Should | Active | Inferred | UC-006 | _TBD_ | FEAT-013 | _TBD_ |
| FR-CAT-011 | a seller attaches images to a product, store up to five images per product and s… | Must | Active | Inferred, CR-1 | UC-005 | ADR-0011 | FEAT-036 | _TBD_ |
| FR-CAT-012 | a buyer opens a product page, show title, description, images, attributes, varia… | Must | Active | P0-3, US-2, US-10 | UC-009 | _TBD_ | FEAT-015 | _TBD_ |
| FR-CAT-013 | a buyer selects a variant, show that variant's price and availability. | Must | Active | US-2 | UC-009 | _TBD_ | FEAT-015 | _TBD_ |
| FR-CAT-014 | a variant has no available stock, show it as sold out and prevent adding it to t… | Must | Active | US-15 | UC-009 | _TBD_ | FEAT-015 | _TBD_ |
| FR-CAT-015 | show the platform's return window, in days, on every product page. | Must | Active | US-10 | UC-009 | _TBD_ | FEAT-015 | _TBD_ |
| FR-CAT-016 | the operator runs the seed command, load at least 200 Vietnamese products, with … | Must | Active | P0-3 | UC-020 | _TBD_ | FEAT-014 | _TBD_ |
| FR-CAT-017 | the operator runs the seed command more than once, not create duplicate records. | Should | Active | Inferred | UC-020 | _TBD_ | FEAT-014 | _TBD_ |
| FR-CAT-018 | a product is created, changed, deactivated or reactivated, reflect the change in… | Should | Active | Inferred | UC-005, UC-006 | ADR-0002, ADR-0003 | FEAT-018 | _TBD_ |
| FR-SRCH-001 | a user submits a keyword query, return active products whose title or descriptio… | Must | Active | P0-4 | UC-007 | ADR-0003 | FEAT-018 | _TBD_ |
| FR-SRCH-002 | a user submits a query written without Vietnamese diacritics, also match product… | Should | Active | Inferred | UC-007 | ADR-0003 | FEAT-018 | _TBD_ |
| FR-SRCH-003 | a user browses a category, list the active products in that category and its des… | Must | Active | P0-4, US-2 | UC-007 | ADR-0003 | FEAT-018 | _TBD_ |
| FR-SRCH-004 | a user applies a category filter to a result set, restrict the results to that c… | Must | Active | P0-4 | UC-007 | ADR-0003 | FEAT-018 | _TBD_ |
| FR-SRCH-005 | a user applies a minimum price, a maximum price, or both, restrict results to pr… | Must | Active | P0-4 | UC-007 | ADR-0003 | FEAT-018 | _TBD_ |
| FR-SRCH-006 | a user selects a sort order (relevance for keyword queries, price ascending, pri… | Must | Active | P0-4 | UC-007 | ADR-0003 | FEAT-018 | _TBD_ |
| FR-SRCH-007 | a user applies several filters and a sort order together, apply all of them to t… | Must | Active | P0-4 | UC-007 | ADR-0003 | FEAT-018 | _TBD_ |
| FR-SRCH-008 | return result lists in pages of 20 with the total result count. | Should | Active | Inferred | UC-007 | ADR-0003 | FEAT-018 | _TBD_ |
| FR-SRCH-009 | a search returns no products, then say so and suggest broadening the query or fi… | Should | Active | Inferred | UC-007 | ADR-0003 | FEAT-018 | _TBD_ |
| FR-SRCH-010 | show on each result the image, title, price (or price range), rating summary and… | Should | Active | Inferred, US-2 | UC-007 | ADR-0003 | FEAT-018 | _TBD_ |
| FR-SRCH-011 | allow browsing, keyword search and natural-language search without authenticatio… | Should | Active | Inferred | UC-007, UC-008 | ADR-0003, ADR-0008 | FEAT-018 | _TBD_ |
| FR-NLS-001 | a buyer submits a free-text Vietnamese query, interpret it into structured const… | Must | Active | P0-11, US-1 | UC-008 | ADR-0003, ADR-0014 | FEAT-019 | _TBD_ |
| FR-NLS-002 | interpret Vietnamese price shorthand in a query (for example "300k", "dưới 300k"… | Must | Active | P0-11, US-1 | UC-008 | ADR-0003, ADR-0014 | FEAT-019 | _TBD_ |
| FR-NLS-003 | constraints have been interpreted, return the active products satisfying the har… | Must | Active | P0-11 | UC-008 | ADR-0003, ADR-0014 | FEAT-019 | _TBD_ |
| FR-NLS-004 | the system answers a natural-language query, it shall show the buyer the constra… | Should | Active | Inferred | UC-008 | ADR-0003, ADR-0014 | FEAT-019 | _TBD_ |
| FR-NLS-005 | query interpretation fails, times out (NFR-PERF-003), or returns output that doe… | Must | Active | P0-11, NFR-REL-008 | UC-008 | ADR-0003, ADR-0014 | FEAT-019 | _TBD_ |
| FR-NLS-006 | the interpreted constraints match no product, then return the semantically close… | Should | Active | Inferred | UC-008 | ADR-0003, ADR-0014 | FEAT-019 | _TBD_ |
| FR-NLS-007 | a product is created or changed, refresh its semantic embedding within 60 second… | Must | Active | P0-11 | UC-005, UC-006 | ADR-0002, ADR-0003, ADR-0014 | FEAT-019 | _TBD_ |
| FR-NLS-008 | seed data is loaded, generate embeddings for all seeded products. | Must | Active | P0-11 | UC-020 | ADR-0003, ADR-0014 | FEAT-019 | _TBD_ |
| FR-NLS-009 | a user exceeds the natural-language search rate limit, then reject the query wit… | Should | Active | Inferred | UC-008 | ADR-0003, ADR-0014 | FEAT-019 | _TBD_ |
| FR-NLS-010 | the operator runs the evaluation script, execute each query in the evaluation se… | Must | Active | P0-11 | UC-021 | ADR-0014 | FEAT-020 | _TBD_ |
| FR-NLS-011 | The repository shall contain an evaluation set of at least 50 Vietnamese queries… | Must | Active | P0-11 | UC-021 | ADR-0014 | FEAT-020 | _TBD_ |
| FR-NLS-012 | an evaluation run completes, record the hit rates in a results file in the repos… | Must | Active | P0-11 | UC-021 | ADR-0014 | FEAT-020 | _TBD_ |
| FR-CART-001 | a signed-in buyer adds a variant with a quantity to the cart, add the line, or i… | Must | Active | P0-5, US-3 | UC-010 | ADR-0017, ADR-0018 | FEAT-021 | _TBD_ |
| FR-CART-002 | the requested quantity plus the quantity already in the cart exceeds the variant… | Must | Active | P0-5, US-15 | UC-010 | ADR-0017, ADR-0018 | FEAT-021 | _TBD_ |
| FR-CART-003 | the variant is sold out or its product is deactivated, then reject adding it to … | Must | Active | US-15 | UC-010 | ADR-0017, ADR-0018 | FEAT-021 | _TBD_ |
| FR-CART-004 | show cart lines grouped by shop, with a subtotal per shop and a cart total. | Must | Active | P0-5, US-3 | UC-010 | ADR-0017, ADR-0018 | FEAT-021 | _TBD_ |
| FR-CART-005 | a buyer changes a line's quantity, update it subject to FR-CART-002. | Must | Active | P0-5 | UC-010 | ADR-0017, ADR-0018 | FEAT-021 | _TBD_ |
| FR-CART-006 | a buyer removes a line, remove it from the cart. | Must | Active | P0-5 | UC-010 | ADR-0017, ADR-0018 | FEAT-021 | _TBD_ |
| FR-CART-007 | keep a buyer's cart across sign-outs and devices. | Should | Active | Inferred | UC-010 | ADR-0017, ADR-0018 | FEAT-021 | _TBD_ |
| FR-CART-008 | a cart line's variant has become unavailable, its quantity exceeds available sto… | Must | Active | US-15 | UC-010 | ADR-0017, ADR-0018 | FEAT-021 | _TBD_ |
| FR-CART-009 | a seller attempts to add a variant from their own shop to the cart, then reject … | Should | Active | Inferred | UC-010 | ADR-0017, ADR-0018 | FEAT-021 | _TBD_ |
| FR-CART-010 | an unauthenticated visitor attempts to add to the cart, then prompt the visitor … | Should | Active | Inferred | UC-010 | ADR-0017, ADR-0018 | FEAT-021 | _TBD_ |
| FR-CHK-001 | a signed-in buyer starts checkout with selected cart lines, show a summary group… | Must | Active | P0-5, US-4 | UC-011 | ADR-0006 | FEAT-022 | _TBD_ |
| FR-CHK-002 | a buyer attempts to place an order without a delivery address or a payment metho… | Must | Active | P0-5 | UC-011 | ADR-0006 | FEAT-022 | _TBD_ |
| FR-CHK-003 | the buyer confirms checkout, create exactly one order per distinct seller among … | Must | Active | P0-5, US-4 | UC-011 | ADR-0001, ADR-0006 | FEAT-022 | _TBD_ |
| FR-CHK-004 | the buyer confirms checkout, reserve stock for every selected line before any or… | Must | Active | P0-5 | UC-011 | ADR-0001, ADR-0006, ADR-0007, ADR-0010, ADR-0019 | FEAT-022 | _TBD_ |
| FR-CHK-005 | stock cannot be reserved for any selected line, then fail the whole checkout, re… | Must | Active | P0-5, US-15 | UC-011 | ADR-0001, ADR-0006, ADR-0007, ADR-0019 | FEAT-023 | _TBD_ |
| FR-CHK-006 | any checkout step fails (stock reservation, order creation, payment initiation),… | Must | Active | P0-5, P0-12 | UC-011 | ADR-0001, ADR-0006, ADR-0012, ADR-0019 | FEAT-023 | _TBD_ |
| FR-CHK-007 | a checkout step does not complete within the checkout step timeout, then treat t… | Must | Active | P0-5 | UC-011 | ADR-0006, ADR-0012, ADR-0019 | FEAT-023 | _TBD_ |
| FR-CHK-008 | the buyer submits the same checkout request more than once, then create the orde… | Must | Active | P0-5, US-16 | UC-011 | ADR-0006, ADR-0009, ADR-0019 | FEAT-023 | _TBD_ |
| FR-CHK-009 | record each order line's unit price, quantity and shipping fee as they were at c… | Must | Active | P0-5 | UC-011 | ADR-0006, ADR-0010 | FEAT-022 | _TBD_ |
| FR-CHK-010 | checkout succeeds, remove the purchased lines from the cart. | Must | Active | P0-5 | UC-011 | ADR-0006, ADR-0017, ADR-0018 | FEAT-022 | _TBD_ |
| FR-CHK-011 | an item's current price differs from the price shown at confirmation, then rejec… | Should | Active | Inferred | UC-011 | ADR-0006 | FEAT-022 | _TBD_ |
| FR-CHK-012 | checkout completes, show the buyer the outcome and the orders created, one per s… | Must | Active | P0-5, US-4 | UC-011 | ADR-0006 | FEAT-022 | _TBD_ |
| FR-CHK-013 | a buyer with an unverified account attempts to place an order, then reject it an… | Must | Active | P0-14 | UC-011 | ADR-0006 | FEAT-022 | _TBD_ |
| FR-INV-001 | maintain, for each variant, the on-hand quantity, the reserved quantity and the … | Must | Active | P0-6 | UC-019 | ADR-0007 | FEAT-012 | _TBD_ |
| FR-INV-002 | a reservation of quantity q is requested for a variant with available quantity o… | Must | Active | P0-6 | UC-011 | ADR-0006, ADR-0007, ADR-0019 | FEAT-017 | _TBD_ |
| FR-INV-003 | a reservation of quantity q is requested for a variant with available quantity b… | Must | Active | P0-6 | UC-011 | ADR-0006, ADR-0007 | FEAT-017 | _TBD_ |
| FR-INV-004 | never let a variant's available quantity fall below zero, under any interleaving… | Must | Active | P0-6, P0-10 | UC-011 | ADR-0006, ADR-0007 | FEAT-017 | _TBD_ |
| FR-INV-005 | a reservation request repeats for the same checkout line, then return the existi… | Must | Active | P0-6, P0-5 | UC-011 | ADR-0006, ADR-0007, ADR-0009, ADR-0019 | FEAT-017 | _TBD_ |
| FR-INV-006 | an order is cancelled before shipment, or checkout is compensated, release the o… | Must | Active | P0-6, US-8 | UC-011, UC-015 | ADR-0006, ADR-0007 | FEAT-023, FEAT-026 | _TBD_ |
| FR-INV-007 | a reservation is not converted to a placed order or confirmed payment within the… | Must | Active | P0-6 | UC-011, UC-012, UC-019 | ADR-0006, ADR-0007, ADR-0012 | FEAT-017 | _TBD_ |
| FR-INV-008 | a release request repeats for the same reservation, then return the units to sto… | Must | Active | P0-6 | UC-011, UC-015, UC-019 | ADR-0006, ADR-0007 | FEAT-017 | _TBD_ |
| FR-INV-009 | an order ships, mark its reservations as consumed, so the units stay deducted pe… | Should | Active | Inferred | UC-014 | ADR-0007 | FEAT-025 | _TBD_ |
| FR-INV-010 | a seller sets a variant's on-hand quantity below its reserved quantity, then rej… | Should | Active | Inferred | UC-006 | ADR-0007 | FEAT-013 | _TBD_ |
| FR-INV-011 | record each stock movement (reserve, release, consume, adjust) with timestamp, q… | Should | Active | Inferred | UC-019 | ADR-0007 | FEAT-013 | _TBD_ |
| FR-INV-012 | keep the reservation hold time configurable without code change. | Should | Active | Inferred | UC-011 | ADR-0007 | FEAT-017 | _TBD_ |
| FR-PAY-001 | the buyer selects cash on delivery, place the orders with payment status "COD pe… | Must | Active | P0-7, US-5 | UC-011 | ADR-0006 | FEAT-022 | _TBD_ |
| FR-PAY-002 | a COD order is marked delivered, record its payment as collected. | Must | Active | P0-7 | UC-014 | _TBD_ | FEAT-027 | _TBD_ |
| FR-PAY-003 | the buyer selects online payment, create a payment for the checkout total with t… | Must | Active | P0-7, US-5 | UC-011, UC-012 | ADR-0006 | FEAT-029 | _TBD_ |
| FR-PAY-004 | the mock payment provider is in use, let the operator simulate success, failure,… | Must | Active | P0-7 | UC-012 | _TBD_ | FEAT-029 | _TBD_ |
| FR-PAY-005 | a provider callback reports success for a pending payment, mark the payment paid… | Must | Active | P0-7 | UC-012 | ADR-0004, ADR-0006 | FEAT-029 | _TBD_ |
| FR-PAY-006 | a provider callback reports failure for a pending payment, mark the payment fail… | Must | Active | P0-7 | UC-012 | _TBD_ | FEAT-029 | _TBD_ |
| FR-PAY-007 | the same provider callback is delivered more than once, then apply its effects o… | Must | Active | P0-7, US-16 | UC-012 | ADR-0002, ADR-0004 | FEAT-029 | _TBD_ |
| FR-PAY-008 | a provider callback fails authenticity verification, then reject it and make no … | Must | Active | P0-7 | UC-012 | ADR-0004 | FEAT-029 | _TBD_ |
| FR-PAY-009 | a provider callback refers to an unknown payment, then reject it and log it. | Should | Active | Inferred | UC-012 | ADR-0004 | FEAT-029 | _TBD_ |
| FR-PAY-010 | an online payment is not completed within the payment window, then cancel the ch… | Must | Active | P0-7, P0-6 | UC-012, UC-019 | ADR-0004, ADR-0006, ADR-0007, ADR-0012 | FEAT-029 | _TBD_ |
| FR-PAY-011 | a success callback arrives after the payment window has expired and the orders a… | Should | Active | Inferred | UC-012 | ADR-0004 | FEAT-030 | _TBD_ |
| FR-PAY-012 | an order with a paid online payment is cancelled, refund the order's amount thro… | Should | Active | Inferred | UC-015 | _TBD_ | FEAT-030 | _TBD_ |
| FR-PAY-013 | an order reaches completed, mark its payment as released to the seller, as a led… | Must | Active | US-7, P0-8 | UC-016 | _TBD_ | FEAT-027 | _TBD_ |
| FR-PAY-014 | record each payment's status history (pending, paid, failed, refunded, released)… | Must | Active | P0-7 | UC-012 | ADR-0004 | FEAT-029 | _TBD_ |
| FR-PAY-015 | a buyer opens an order, show the order's payment method and status. | Should | Active | Inferred | UC-012, UC-013 | _TBD_ | FEAT-024 | _TBD_ |
| FR-PAY-016 | the VNPay sandbox is enabled, initiate and verify online payments through the VN… | Could | Active | P1-5 | UC-012 | _TBD_ | FEAT-031 | _TBD_ |
| FR-ORD-001 | an order is created, set its status to placed for COD, or awaiting payment for o… | Must | Active | P0-8 | UC-011, UC-012 | ADR-0006 | FEAT-022 | _TBD_ |
| FR-ORD-002 | allow only these transitions: awaiting payment to placed or cancelled; placed to… | Must | Active | P0-8 | UC-014 | ADR-0006 | FEAT-025 | _TBD_ |
| FR-ORD-003 | a requested status change is not an allowed transition or the requester may not … | Must | Active | P0-8 | UC-014 | _TBD_ | FEAT-025 | _TBD_ |
| FR-ORD-004 | record every status change with its time and actor, and show the history as a ti… | Must | Active | P0-8, US-6 | UC-013, UC-014, UC-016 | _TBD_ | FEAT-024 | _TBD_ |
| FR-ORD-005 | a seller confirms a placed order, change its status to confirmed. | Must | Active | P0-8, US-14 | UC-014 | _TBD_ | FEAT-025 | _TBD_ |
| FR-ORD-006 | a seller ships a confirmed order, change its status to shipped and record the mo… | Must | Active | P0-8, US-14 | UC-014 | _TBD_ | FEAT-025 | _TBD_ |
| FR-ORD-007 | the mocked shipping status reports a shipped order delivered, change its status … | Must | Active | P0-8 | UC-014 | _TBD_ | FEAT-025 | _TBD_ |
| FR-ORD-008 | a buyer confirms receipt of a delivered order, change its status to completed. | Must | Active | P0-8, US-7 | UC-016 | _TBD_ | FEAT-027 | _TBD_ |
| FR-ORD-009 | a delivered order is not confirmed by the buyer within the auto-completion perio… | Must | Active | P0-8, US-7 | UC-016 | ADR-0012 | FEAT-027 | _TBD_ |
| FR-ORD-010 | a buyer cancels an order that is placed or confirmed, change its status to cance… | Must | Active | P0-8, US-8 | UC-015 | _TBD_ | FEAT-026 | _TBD_ |
| FR-ORD-011 | a seller cancels an order that is placed or confirmed, giving a reason, change i… | Must | Active | P0-8, US-14 | UC-014, UC-015 | _TBD_ | FEAT-026 | _TBD_ |
| FR-ORD-012 | a cancellation is requested for an order that is shipped, delivered or completed… | Must | Active | P0-8, US-8 | UC-014, UC-015 | _TBD_ | FEAT-026 | _TBD_ |
| FR-ORD-013 | a buyer opens the order list or an order, show orders newest first in pages, fil… | Must | Active | P0-8, US-6 | UC-013 | _TBD_ | FEAT-024 | _TBD_ |
| FR-ORD-014 | a seller opens the order list or an order of their shop, show it filterable by s… | Must | Active | P0-8, US-14 | UC-014 | _TBD_ | FEAT-025 | _TBD_ |
| FR-ORD-015 | show a buyer's orders from the same checkout grouped together. | Should | Active | Inferred, US-3 | UC-011, UC-013 | _TBD_ | FEAT-024 | _TBD_ |
| FR-ORD-016 | assign each order a unique, human-readable order number. | Should | Active | Inferred | UC-011, UC-013 | ADR-0009 | FEAT-022 | _TBD_ |
| FR-SHIP-001 | an order is priced at checkout, add a mocked shipping fee obtained through a shi… | Must | Active | P0-8 | UC-011 | _TBD_ | FEAT-022 | _TBD_ |
| FR-SHIP-002 | an order is shipped, generate a mocked tracking number and record mocked shippin… | Must | Active | P0-8 | UC-013, UC-014 | _TBD_ | FEAT-025 | _TBD_ |
| FR-SHIP-003 | the mock shipping provider is in use, move shipped orders to delivered after a c… | Must | Active | P0-8 | UC-014 | _TBD_ | FEAT-025 | _TBD_ |
| FR-REV-001 | a buyer submits a rating from 1 to 5, with optional text, for an item of their o… | Must | Active | P0-9, US-9 | UC-017 | _TBD_ | FEAT-033 | _TBD_ |
| FR-REV-002 | the item is not in a completed order of the submitting buyer, then reject the re… | Must | Active | P0-9 | UC-017 | _TBD_ | FEAT-033 | _TBD_ |
| FR-REV-003 | a review already exists for that order item, then reject a second review. | Must | Active | P0-9 | UC-017 | _TBD_ | FEAT-033 | _TBD_ |
| FR-REV-004 | the rating is missing or outside 1 to 5, or the text exceeds 2000 characters, th… | Must | Active | P0-9 | UC-017 | _TBD_ | FEAT-033 | _TBD_ |
| FR-REV-005 | a user opens a product page, list its reviews newest first in pages, each with r… | Must | Active | P0-9, US-9 | UC-009, UC-017 | _TBD_ | FEAT-033 | _TBD_ |
| FR-REV-006 | show each product's average rating and review count on the product page and in r… | Should | Active | Inferred, US-9 | UC-009, UC-017 | _TBD_ | FEAT-033 | _TBD_ |
| FR-REV-007 | a review is created, update the product's average rating and count within one mi… | Should | Active | Inferred | UC-017 | _TBD_ | FEAT-033 | _TBD_ |
| FR-REV-008 | the review-summary batch job runs, generate a text summary of the reviews for ea… | Could | Active | P1-1 | UC-009 | ADR-0014 | FEAT-034 | _TBD_ |
| FR-REV-009 | a review summary exists for a product, show it on the product page labelled as A… | Could | Active | P1-1 | UC-009 | _TBD_ | FEAT-034 | _TBD_ |
| FR-REV-010 | summary generation fails for a product, then keep any previous summary and conti… | Could | Active | P1-1 | UC-009 | _TBD_ | FEAT-034 | _TBD_ |
| FR-NOTIF-001 | an account-registered, order-placed, order-shipped or order-cancelled event occu… | Must | Active | P0-14 | UC-001, UC-018 | ADR-0016, features/FEAT-001-register-and-sign-in/technical-design.md | FEAT-001, FEAT-032 | features/FEAT-001-register-and-sign-in/acceptance-report.md (partial) |
| FR-NOTIF-002 | an account is created, send the user a verification email containing the verific… | Must | Active | P0-14, US-17 | UC-001 | ADR-0016, features/FEAT-001-register-and-sign-in/technical-design.md | FEAT-001 | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-NOTIF-003 | an order enters the placed status, send the buyer an order-placed email for that… | Must | Active | P0-14, US-18 | UC-011 | ADR-0016 | FEAT-032 | _TBD_ |
| FR-NOTIF-004 | an order enters the placed status, send the order's seller a new-order email. | Must | Active | P0-14, US-19 | UC-011 | ADR-0016 | FEAT-032 | _TBD_ |
| FR-NOTIF-005 | an order is shipped, send the buyer an order-shipped email. | Must | Active | P0-14, US-18 | UC-013, UC-014 | ADR-0016 | FEAT-032 | _TBD_ |
| FR-NOTIF-006 | an order is cancelled, send the buyer an order-cancelled email. | Must | Active | P0-14, US-18 | UC-014, UC-015 | ADR-0016 | FEAT-032 | _TBD_ |
| FR-NOTIF-007 | a buyer cancels an order, send the order's seller an order-cancelled email. | Should | Active | Inferred | UC-015 | ADR-0016 | FEAT-032 | _TBD_ |
| FR-NOTIF-008 | render emails from templates in the recipient's language (Vietnamese or English,… | Must | Active | P0-14 | UC-001, UC-018 | ADR-0016 | FEAT-032 | _TBD_ |
| FR-NOTIF-009 | sending an email fails, then retry it per NFR-REL-004 and shall not block or fai… | Must | Active | P0-14 | UC-018 | ADR-0002, ADR-0016 | FEAT-032 | _TBD_ |
| FR-NOTIF-010 | the same event is delivered more than once, then send exactly one email per reci… | Must | Active | P0-14 | UC-018 | ADR-0002, ADR-0016 | FEAT-032 | _TBD_ |
| FR-NOTIF-011 | all retries for an email are exhausted, then mark it failed and log it for inspe… | Should | Active | Inferred | UC-018 | ADR-0016 | FEAT-032 | _TBD_ |
| FR-NOTIF-012 | record each email's status (queued, sent, failed) per recipient and event. | Should | Active | Inferred | UC-018 | ADR-0016 | FEAT-032 | _TBD_ |
| FR-NOTIF-013 | a password reset is requested for an existing account, send the reset email thro… | Could | Active | P1-6 | UC-022 | ADR-0016 | FEAT-007 | _TBD_ |
| FR-NOTIF-014 | a user's password is changed, send the user a password-changed security email. | Should | Active | CR-1, Inferred | UC-018, UC-026 | ADR-0016 | FEAT-003 | _TBD_ |
| FR-NOTIF-015 | a Google identity is linked to an existing account, send the account owner a not… | Should | Active | CR-1, Inferred | UC-018, UC-025 | ADR-0016 | FEAT-004 | _TBD_ |
| FR-NOTIF-016 | a sign-in code is issued, send the account's email address a sign-in code email … | Should | Active | CR-7 | UC-029 | ADR-0016 | FEAT-006 | _TBD_ |
| FR-LAS-001 | a seller requests an AI draft with short input text (keywords, attributes), gene… | Could | Active | P1-2 | UC-023 | ADR-0014 | FEAT-016 | _TBD_ |
| FR-LAS-002 | present the draft as editable text and shall not save it to the product until th… | Could | Active | P1-2 | UC-023 | _TBD_ | FEAT-016 | _TBD_ |
| FR-LAS-003 | draft generation fails or times out, then tell the seller and keep manual entry … | Could | Active | P1-2 | UC-023 | _TBD_ | FEAT-016 | _TBD_ |
| FR-RTN-001 | a buyer requests a return for a delivered order within the return window, giving… | Could | Active | P1-3 | UC-024 | _TBD_ | FEAT-028 | _TBD_ |
| FR-RTN-002 | the order is not delivered or the return window has passed, then reject the retu… | Could | Active | P1-3 | UC-024 | _TBD_ | FEAT-028 | _TBD_ |
| FR-RTN-003 | a seller approves a return request, mark the order returned and refund the buyer… | Could | Active | P1-3 | UC-024 | _TBD_ | FEAT-028 | _TBD_ |
| FR-GEN-001 | the operator runs the data generator with a target order count, generate synthet… | Could | Active | P1-4 | — | _TBD_ | FEAT-038 | _TBD_ |
| FR-GEN-002 | the operator supplies a random seed, generate the same dataset on each run. | Could | Active | P1-4 | — | _TBD_ | FEAT-038 | _TBD_ |
| FR-GEN-003 | the generator is pointed at the deployed demo environment, then it shall refuse … | Could | Active | P1-4 | — | _TBD_ | FEAT-038 | _TBD_ |
| FR-PROF-001 | a signed-in user opens their profile, show display name, email, phone, gender, d… | Must | Active | CR-1 | UC-027 | ADR-0011, features/FEAT-002-profile-and-language/technical-design.md, features/FEAT-002-profile-and-language/ui-design.md | FEAT-002 | features/FEAT-002-profile-and-language/acceptance-report.md (partial) |
| FR-PROF-002 | a signed-in user submits changes to display name, phone, gender or date of birth… | Must | Active | CR-1 | UC-027 | features/FEAT-002-profile-and-language/technical-design.md, features/FEAT-002-profile-and-language/ui-design.md | FEAT-002 | features/FEAT-002-profile-and-language/acceptance-report.md |
| FR-PROF-003 | a profile field is invalid (display name empty or over 80 characters, phone not … | Must | Active | CR-1 | UC-027 | features/FEAT-002-profile-and-language/technical-design.md, features/FEAT-002-profile-and-language/ui-design.md | FEAT-002 | features/FEAT-002-profile-and-language/acceptance-report.md |
| FR-PROF-004 | show the account email as read-only on the profile. | Must | Active | CR-1 | UC-027 | features/FEAT-002-profile-and-language/technical-design.md, features/FEAT-002-profile-and-language/ui-design.md | FEAT-002 | features/FEAT-002-profile-and-language/acceptance-report.md |
| FR-PROF-005 | any user views another user's information (for example a review author), show on… | Must | Active | CR-1 | UC-009 | _TBD_ | FEAT-033 | _TBD_ |
| FR-PROF-006 | a signed-in user selects a preferred language, save it to the profile. | Must | Active | CR-1 | UC-027 | features/FEAT-002-profile-and-language/technical-design.md, features/FEAT-002-profile-and-language/ui-design.md | FEAT-002 | features/FEAT-002-profile-and-language/acceptance-report.md |
| FR-MEDIA-001 | a signed-in user uploads an avatar image, store it and show it on the user's pro… | Must | Active | CR-1 | UC-027, UC-028 | ADR-0011 | FEAT-035 | _TBD_ |
| FR-MEDIA-002 | a shop owner uploads a shop logo or shop cover image, store it and show it on th… | Must | Active | CR-1 | UC-004, UC-028 | ADR-0011 | FEAT-036 | _TBD_ |
| FR-MEDIA-003 | a seller uploads product images, store up to five per product in a seller-chosen… | Must | Active | CR-1 | UC-005, UC-006, UC-028 | ADR-0011 | FEAT-036 | _TBD_ |
| FR-MEDIA-004 | an uploaded file is not a JPEG, PNG or WebP image, exceeds its size limit, or is… | Must | Active | CR-1 | UC-004, UC-005, UC-028 | ADR-0011 | FEAT-035 | _TBD_ |
| FR-MEDIA-005 | a user replaces or removes an image, make the previous file inaccessible. | Must | Active | CR-1 | UC-004, UC-006, UC-028 | ADR-0011 | FEAT-035 | _TBD_ |
| FR-MEDIA-006 | an image is shown in a list or card, serve a downsized version of it. | Should | Active | CR-1, Inferred | UC-028 | ADR-0011 | FEAT-035 | _TBD_ |
| FR-MEDIA-007 | the system stores an uploaded image, it shall remove embedded metadata such as l… | Should | Active | CR-1, Inferred | UC-028 | ADR-0011 | FEAT-035 | _TBD_ |
| FR-MEDIA-008 | image storage is unavailable, then reject the upload, keep the existing image, a… | Must | Active | CR-1 | UC-028 | ADR-0011 | FEAT-035 | _TBD_ |
| FR-LOC-001 | provide the UI, system messages and emails in Vietnamese (vi) and English (en), … | Must | Active | CR-1 | UC-027 | _TBD_ | Foundations | _TBD_ |
| FR-LOC-002 | a user selects a language, switch the UI at once and save the choice to the prof… | Must | Active | CR-1 | UC-027 | features/FEAT-002-profile-and-language/technical-design.md, features/FEAT-002-profile-and-language/ui-design.md | FEAT-002 | features/FEAT-002-profile-and-language/acceptance-report.md (partial) |
| FR-LOC-003 | a visitor with no saved language opens the site, use English if the browser pref… | Must | Active | CR-1 | UC-027 | _TBD_ | Foundations | _TBD_ |
| FR-LOC-004 | the system rejects a request, it shall return a stable error code and a message … | Must | Active | CR-1 | UC-027 | ADR-0019, features/FEAT-001-register-and-sign-in/technical-design.md | Foundations | features/FEAT-001-register-and-sign-in/acceptance-report.md |
| FR-LOC-005 | the system sends an email, it shall use the recipient's saved language, and for … | Must | Active | CR-1 | UC-001, UC-018 | ADR-0016, features/FEAT-001-register-and-sign-in/technical-design.md | FEAT-001, FEAT-032 | features/FEAT-001-register-and-sign-in/acceptance-report.md (partial) |
| FR-LOC-006 | format dates, numbers and money according to the active language. | Must | Active | CR-1 | UC-027 | ADR-0010 | Foundations | _TBD_ |
| FR-LOC-007 | provide Vietnamese and English names for platform-owned content (categories, att… | Must | Active | CR-1 | UC-005, UC-007 | _TBD_ | FEAT-014, Foundations | _TBD_ |
| FR-LOC-008 | a translation is missing for the active language, then show the Vietnamese text. | Must | Active | CR-1 | UC-027 | _TBD_ | Foundations | _TBD_ |
| FR-CUR-001 | keep a currency table (code, symbol, minor-unit exponent) and shall enable only … | Must | Active | CR-1 | UC-005 | ADR-0010 | Foundations | _TBD_ |
| FR-CUR-002 | the system shows a money amount, it shall format it according to the currency an… | Must | Active | CR-1 | UC-005, UC-009, UC-011 | ADR-0010 | Foundations | _TBD_ |
| FR-CUR-003 | store every monetary amount (price, shipping fee, order total, payment, refund) … | Must | Active | CR-1 | UC-005, UC-011 | ADR-0010 | FEAT-012, Foundations | _TBD_ |
| FR-CUR-004 | selected checkout lines or an order would combine more than one currency, then r… | Must | Active | CR-1 | UC-011 | ADR-0010 | FEAT-022 | _TBD_ |
| FR-CUR-005 | the system calls a payment provider, it shall send the amount together with the … | Must | Active | CR-1 | UC-012 | ADR-0010 | FEAT-029 | _TBD_ |
| EIR-UI-001 | provide a web buyer storefront covering browse, search, product page, cart, chec… | Must | Active | P0, US-1..10 | — | _TBD_ | FEAT-011, FEAT-015, FEAT-018, FEAT-021, FEAT-022, FEAT-024, FEAT-033 | _TBD_ |
| EIR-UI-002 | provide a web seller centre covering shop setup, products, stock and orders. | Must | Active | P0, US-11..14 | — | _TBD_ | FEAT-009, FEAT-010, FEAT-012, FEAT-013, FEAT-025 | _TBD_ |
| EIR-UI-003 | The web UIs shall work on mobile browsers at 360 px width and on desktop. | Should | Active | Inferred | — | _TBD_ | Foundations | _TBD_ |
| EIR-UI-004 | The web UIs shall be available in Vietnamese and English (FR-LOC-001). | Must | Active | CR-1 | — | _TBD_ | Foundations | _TBD_ |
| EIR-HW-001 | The system has no hardware interfaces. | Must | Active | n/a | — | _TBD_ | Foundations | _TBD_ |
| EIR-SW-001 | call an LLM through a provider interface for query interpretation, and for listi… | Must | Active | P0-11 | — | ADR-0014 | FEAT-019 | _TBD_ |
| EIR-SW-002 | generate multilingual text embeddings through a provider interface using a model… | Must | Active | P0-11 | — | ADR-0014 | FEAT-019 | _TBD_ |
| EIR-SW-003 | send email through an email-provider interface, through Resend for the demo and … | Must | Active | P0-14, CR-1 | — | ADR-0015 | FEAT-001, FEAT-032 | _TBD_ |
| EIR-SW-004 | access payment through a payment-provider interface implemented by a mock provid… | Must | Active | P0-7, P1-5 | — | _TBD_ | FEAT-029 | _TBD_ |
| EIR-SW-005 | obtain shipping fees and status through a shipping interface implemented by a mo… | Must | Active | P0-8 | — | _TBD_ | FEAT-022, FEAT-025 | _TBD_ |
| EIR-SW-006 | authenticate Google sign-ins through Google's OpenID Connect interface. | Must | Active | CR-1 | UC-025 | ADR-0008 | FEAT-004 | _TBD_ |
| EIR-SW-007 | store uploaded images through an object-storage interface (local volume or emula… | Must | Active | CR-1 | — | ADR-0011, ADR-0015 | FEAT-035 | _TBD_ |
| EIR-COM-001 | Clients shall reach all backend capabilities through a single HTTPS API gateway. | Must | Active | P0-1 | — | ADR-0008, ADR-0019 | Foundations | _TBD_ |
| EIR-COM-002 | accept inbound payment-provider callbacks over HTTPS and authenticate each by si… | Must | Active | P0-7 | — | _TBD_ | FEAT-029 | _TBD_ |
| EIR-COM-003 | The backend capabilities shall exchange domain events asynchronously through a m… | Must | Active | P0-5, P0-14 | — | ADR-0002, ADR-0015 | Foundations | _TBD_ |
| EIR-COM-004 | export traces, metrics and logs in OpenTelemetry format. | Must | Active | P0-12 | — | ADR-0013 | Foundations | _TBD_ |
| EIR-COM-005 | make outbound HTTPS calls to Google, Resend and the object store, each authentic… | Must | Active | CR-1 | — | _TBD_ | Foundations | _TBD_ |
| NFR-PERF-001 | 95% of product-page and category-browse requests complete within 300 ms at 50 co… | Should | Active | Inferred (OQ-21) | — | _TBD_ | FEAT-015 | _TBD_ |
| NFR-PERF-002 | 95% of keyword searches complete within 500 ms at 20 concurrent users with 10,00… | Should | Active | Inferred (OQ-21) | — | ADR-0003 | FEAT-018 | _TBD_ |
| NFR-PERF-003 | 95% of natural-language searches, including query interpretation, return within … | Should | Active | Inferred (OQ-21) | — | ADR-0003, ADR-0014 | FEAT-019 | _TBD_ |
| NFR-PERF-004 | 95% of checkout confirmations for carts with up to 5 sellers complete within 3 s… | Should | Active | Inferred (OQ-21) | — | ADR-0006, ADR-0019 | FEAT-022 | _TBD_ |
| NFR-PERF-005 | The inventory reservation path shall record its p95 latency in the k6 report at … | Must | Active | P0-10 | — | ADR-0007, ADR-0019 | FEAT-017 | _TBD_ |
| NFR-PERF-006 | After scale-to-zero idle, the first request on the deployed demo shall respond w… | Could | Active | Inferred | — | ADR-0005 | FEAT-037 | _TBD_ |
| NFR-PERF-007 | 95% of image uploads up to 5 MB shall be processed and visible within 5 s. | Must | Active | CR-1 | UC-028 | ADR-0011 | FEAT-035 | _TBD_ |
| NFR-SCAL-001 | With 10 units of one variant and 200 concurrent buyers each attempting to buy 1,… | Must | Active | P0-6, P0-10 | — | ADR-0001, ADR-0007 | FEAT-017 | _TBD_ |
| NFR-SCAL-002 | meet NFR-PERF-001..004 with up to 10,000 products and 100,000 orders in the data… | Should | Active | Inferred | — | ADR-0003 | FEAT-037 | _TBD_ |
| NFR-SCAL-003 | the generator is built, the load test shall run against 1,000,000 orders, or 100… | Could | Active | P1-4 | — | _TBD_ | FEAT-038 | _TBD_ |
| NFR-REL-001 | Across all automated and load tests, the count of oversold units shall be 0. | Must | Active | P0-6, P0-10 | — | ADR-0007 | FEAT-017 | _TBD_ |
| NFR-REL-002 | An automated test shall inject a failure at each checkout step (stock, order cre… | Must | Active | P0-5 | UC-011 | ADR-0001, ADR-0006, ADR-0019 | FEAT-023 | _TBD_ |
| NFR-REL-003 | An automated test shall deliver the same payment callback 3 times and verify exa… | Must | Active | P0-7 | UC-012 | ADR-0004 | FEAT-029 | _TBD_ |
| NFR-REL-004 | A failed email send shall be retried up to 5 times with exponential backoff, wit… | Must | Active | P0-14 | UC-018 | ADR-0016 | FEAT-032 | _TBD_ |
| NFR-REL-005 | Every event consumer shall tolerate at-least-once delivery, producing no duplica… | Must | Active | P0-7, P0-14 | UC-012, UC-018 | ADR-0002, ADR-0004, ADR-0016 | Foundations | _TBD_ |
| NFR-REL-006 | The deployed demo has best-effort availability with no uptime target; it shall b… | Should | Active | Inferred | — | ADR-0005, ADR-0012 | Foundations | _TBD_ |
| NFR-REL-007 | The full system state shall be restorable from scripts (migrations plus seed) in… | Should | Active | Inferred | UC-020 | ADR-0015, ADR-0018 | Foundations | _TBD_ |
| NFR-REL-008 | Failure of the LLM, embedding, email (Resend), image storage, Google sign-in or … | Must | Active | P0-11, P0-14 | UC-008 | ADR-0008, ADR-0011, ADR-0014 | Foundations | _TBD_ |
| NFR-SEC-001 | All traffic to the deployed demo shall use TLS 1.2 or higher. | Must | Active | Inferred | — | ADR-0005, ADR-0008 | Foundations | _TBD_ |
| NFR-SEC-002 | Passwords shall be stored only as salted hashes using argon2id or bcrypt, never … | Must | Active | P0-1 | — | ADR-0008 | FEAT-001 | _TBD_ |
| NFR-SEC-003 | The password policy requires at least 8 characters including at least one letter… | Must | Active | P0-1 (proposed) | — | ADR-0008 | FEAT-001 | _TBD_ |
| NFR-SEC-004 | Access tokens shall expire within 15 minutes and refresh tokens within 7 days. | Must | Active | P0-1 (proposed), CR-3 | — | ADR-0008 | FEAT-001 | _TBD_ |
| NFR-SEC-005 | Rate limits: register, sign-in, resend-verification and sign-in code at most 10 … | Should | Active | Inferred, CR-7 | UC-029 | ADR-0008 | FEAT-005 | _TBD_ |
| NFR-SEC-006 | Automated tests shall verify that a user cannot read or change another user's re… | Should | Active | Inferred | — | ADR-0009, ADR-0019 | Foundations | _TBD_ |
| NFR-SEC-007 | Secrets shall not be stored in the repository; CI shall fail on detected secrets… | Must | Active | P0-12 | — | ADR-0005, ADR-0015 | Foundations | _TBD_ |
| NFR-SEC-008 | Bank account numbers shall be encrypted at rest and shown masked except the last… | Should | Active | Inferred | UC-004 | _TBD_ | FEAT-010 | _TBD_ |
| NFR-SEC-009 | keep an audit log, retained at least 90 days, of sign-in success and failure, lo… | Should | Active | Inferred, CR-7 | — | ADR-0008, ADR-0013 | FEAT-005 | _TBD_ |
| NFR-SEC-010 | pass user text to the LLM only as data, shall validate LLM output against a fixe… | Must | Active | P0-11 | UC-008 | ADR-0014 | FEAT-019 | _TBD_ |
| NFR-SEC-011 | neither store nor process payment card numbers. | Must | Active | P0-7 | UC-012 | _TBD_ | FEAT-029 | _TBD_ |
| NFR-SEC-012 | CI shall fail on known high-severity vulnerabilities in dependencies. | Should | Active | Inferred | — | _TBD_ | Foundations | _TBD_ |
| NFR-SEC-013 | Uploaded images shall be validated by file signature rather than extension, re-e… | Must | Active | CR-1 | UC-028 | ADR-0011 | FEAT-035 | _TBD_ |
| NFR-SEC-014 | Google sign-in shall use the authorization-code flow with PKCE, verify state and… | Must | Active | CR-1 | UC-025 | ADR-0008 | FEAT-004 | _TBD_ |
| NFR-SEC-015 | After a password change or reset, refresh tokens issued before the change shall … | Must | Active | CR-1 | UC-026 | ADR-0008 | FEAT-003 | _TBD_ |
| NFR-SEC-016 | Sign-in codes shall be generated with a secure random generator, stored only as … | Must | Active | CR-7 | UC-029 | ADR-0008 | FEAT-006 | _TBD_ |
| NFR-USE-001 | The hero flow (search, cart, split checkout, pay, track, review) shall be comple… | Must | Active | P0-13, §7 | — | _TBD_ | FEAT-037 | _TBD_ |
| NFR-USE-002 | Every list and asynchronous action shall have defined loading, empty and error s… | Should | Active | Inferred | — | _TBD_ | Foundations | _TBD_ |
| NFR-USE-003 | The buyer hero-flow screens shall meet WCAG 2.1 level AA. | Could | Active | Inferred | — | _TBD_ | Foundations | _TBD_ |
| NFR-COMPAT-001 | The UIs shall work on the latest two versions of Chrome, Edge, Firefox and Safar… | Should | Active | Inferred | — | _TBD_ | Foundations | _TBD_ |
| NFR-MNT-001 | CI shall build, test and lint on every push, and main shall stay green. | Must | Active | P0-12 | — | _TBD_ | Foundations | _TBD_ |
| NFR-MNT-002 | The repository shall contain at least 5 ADRs: service boundaries, message broker… | Must | Active | P0-12, §7 | — | ADR-0001, ADR-0002, ADR-0003, ADR-0004, ADR-0005 | Foundations | _TBD_ |
| NFR-MNT-003 | The repository shall contain an architecture diagram and a README with run steps… | Must | Active | P0-12 | — | ADR-0013 | Foundations | _TBD_ |
| NFR-MNT-004 | The deployed slice shall be defined as infrastructure as code. | Must | Active | P0-12 | — | ADR-0005 | FEAT-037 | _TBD_ |
| NFR-MNT-005 | Automated tests from NFR-REL-002, NFR-REL-003 and the oversell concurrency test … | Must | Active | P0-10, P0-12 | — | ADR-0015 | FEAT-017, FEAT-023, FEAT-029, Foundations | _TBD_ |
| NFR-MNT-006 | Payment, email, shipping, LLM, embedding, identity (Google) and object-storage p… | Must | Active | P0-7, P0-14, P2 | — | ADR-0002, ADR-0014, ADR-0016, ADR-0019 | Foundations | _TBD_ |
| NFR-MNT-007 | The order-total calculation shall be isolated so adding discounts later does not… | Should | Active | P2 | — | ADR-0006, ADR-0010 | FEAT-022 | _TBD_ |
| NFR-MNT-008 | Every monetary amount in storage, APIs, events and logs shall carry an amount an… | Must | Active | CR-1 | — | ADR-0010 | Foundations | _TBD_ |
| NFR-MNT-009 | Adding a currency shall require only currency configuration, formatting rules an… | Must | Active | CR-1 | — | ADR-0010 | Foundations | _TBD_ |
| NFR-MNT-010 | Image storage shall sit behind an interface so the local and cloud implementatio… | Must | Active | CR-1 | UC-028 | ADR-0011 | FEAT-035 | _TBD_ |
| NFR-PORT-001 | A fresh clone shall run the full stack with one command through the .NET Aspire AppHost and no manual st… | Must | Active | P0-12 | — | ADR-0015 | Foundations | _TBD_ |
| NFR-PORT-002 | The demo slice shall run on Azure container hosting that scales to zero. | Must | Active | P0-13 | — | ADR-0005, ADR-0012 | FEAT-037 | _TBD_ |
| NFR-OBS-001 | A checkout shall produce one OpenTelemetry trace with spans across the gateway a… | Must | Active | P0-12 | UC-011 | ADR-0006, ADR-0013, ADR-0019 | FEAT-022 | _TBD_ |
| NFR-OBS-002 | Every service shall write structured logs carrying a correlation identifier and … | Should | Active | Inferred | — | ADR-0013, ADR-0015 | Foundations | _TBD_ |
| NFR-OBS-003 | The k6 load-test reports shall be committed to the repository. | Must | Active | P0-10 | — | ADR-0007, ADR-0013 | FEAT-017 | _TBD_ |
| NFR-COST-001 | Azure plus AI cost shall stay under $5 per month, with a budget alert configured… | Must | Active | G4, §7 | — | ADR-0003, ADR-0005, ADR-0012, ADR-0013, ADR-0014 | FEAT-037, Foundations | _TBD_ |
| NFR-COST-002 | LLM and embedding usage shall be bounded by a configurable cap, and degrade to k… | Should | Active | Inferred | — | ADR-0014 | FEAT-019 | _TBD_ |
| NFR-COST-003 | Batch embeddings, data generation and load tests shall run in the local environm… | Must | Active | G4 | — | ADR-0005, ADR-0014, ADR-0015 | Foundations | _TBD_ |
| NFR-COST-004 | Image storage and email sending shall stay within the $5 per month budget and wi… | Must | Active | CR-1 | UC-018 | ADR-0005, ADR-0011, ADR-0016 | FEAT-032, FEAT-035 | _TBD_ |
| NFR-L10N-001 | Money shall be handled as an amount plus currency code and shown according to th… | Must | Active | A-1, CR-1 | — | ADR-0010 | Foundations | _TBD_ |
| NFR-L10N-002 | Timestamps shall be stored in UTC and shown in Asia/Ho_Chi_Minh, with date and n… | Should | Active | Inferred | — | _TBD_ | Foundations | _TBD_ |
| NFR-L10N-003 | store, search and email Vietnamese text with full diacritic support (UTF-8 throu… | Must | Active | P0-11, P0-14 | — | ADR-0003, ADR-0009, ADR-0016 | FEAT-018, Foundations | _TBD_ |
| NFR-L10N-004 | CI shall fail when the Vietnamese and English translation sets contain different… | Must | Active | CR-1 | — | _TBD_ | Foundations | _TBD_ |
| NFR-LEG-001 | collect only email, name, phone, address, payout bank fields and the optional ge… | Should | Active | Inferred (OQ-18) | — | _TBD_ | Foundations | _TBD_ |
| NFR-LEG-002 | Seed data shall come from a source whose terms permit its use, recorded in the r… | Must | Active | OQ-1 | UC-020 | _TBD_ | FEAT-014 | _TBD_ |
| NFR-DEM-001 | The deployed URL shall let a user complete a checkout. | Must | Active | P0-13 | — | ADR-0005 | FEAT-037 | _TBD_ |
| NFR-DEM-002 | A recorded demo video of the hero flow shall be 3 minutes or less. | Must | Active | P0-13 | — | _TBD_ | FEAT-037 | _TBD_ |
| NFR-DEM-003 | The hero flow shall run with no manual database edits, and 10 orders from 3 sell… | Must | Active | §7 | — | _TBD_ | FEAT-037 | _TBD_ |
