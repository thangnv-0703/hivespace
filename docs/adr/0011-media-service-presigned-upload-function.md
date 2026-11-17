# ADR-0011: Media service, presigned uploads and an image-processor Function

**Status:** Accepted
**Date:** 2026-11-15
**Requirements addressed:** FR-MEDIA-001..008, FR-CAT-011, FR-SHOP-009, FR-PROF-001, NFR-SEC-013, NFR-PERF-007, NFR-MNT-010, NFR-REL-008, NFR-COST-004, EIR-SW-007, UC-028; OQ-30, OQ-31

## Context
Avatars, shop logo and cover, and up to five product images per product must be uploaded (FR-MEDIA-001..003), validated by file signature, re-encoded, stripped of metadata and resized, and served fast. The builder wants media to be a service that can grow (more media types later), uploads that go straight to storage, and processing done afterwards. Azure Functions are wanted where they genuinely fit. Event Grid has no local emulator; the Service Bus emulator does (ADR-0015).

## Decision
1. `POST /v1/media/uploads` on **Media** (purpose, content type, size). Media checks allowed types and size limits, records a `pending` item owned by the caller, and returns a short-lived **write-only SAS URL** to the private `incoming` container under a random name.
2. The browser uploads directly to blob storage.
3. `POST /v1/media/{id}/complete` makes Media publish `MediaUploaded`.
4. The **image-processor Azure Function** (Service Bus trigger) validates by signature, rejects SVG and executables, enforces the real size limit (a SAS URL cannot), re-encodes, strips metadata, creates sized variants, writes to the `public` container under random names, deletes the incoming blob and publishes `MediaProcessed` or `MediaRejected`.
5. Media marks the item `ready` or `rejected`.
6. Identity (avatar, shop images) and Catalog (product images) store only `mediaId`. Attaching calls Media to confirm the item is `ready` and uploaded by the same user; they check ownership of the shop or product themselves.
7. Replace or remove: the owner detaches and publishes `MediaDetached`; Media deletes the blobs (FR-MEDIA-005).
8. A lifecycle rule deletes stale `incoming` blobs. Public blobs use random names and cache headers, so image requests do not wake any service.

## Options considered
- **Media service plus presigned upload plus Function (chosen)** — no bytes through services, cheap, room to grow, a good fit for a Function (bursty, stateless, event-driven).
- **Shared library inside Identity and Catalog, upload through the service** — fewer deployables, but bytes pass through gateway and services, and a third consumer duplicates endpoints.
- **Blob Event Grid trigger instead of a "complete" call** — fewer calls, but no local emulator and blob-trigger latency risks the 5 s target.

## Consequences
Easier: upload cost and load, one home for media policy, a real Function to learn. Harder: another service and a Function app to build and run (R-1, with a stated cut order); two-step upload in the UI; function cold start may miss 5 s on the first upload (R-4); local Function support in Aspire must be verified (R-9). Revisit if video or moderation features appear.
