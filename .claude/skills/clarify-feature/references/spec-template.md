# feature-spec.md template

The reader is the user (product owner) first, then detailed-design and
acceptance verification. Business language only. IDs use the feature number
`NNN` so they are unique across features.

```markdown
# Feature Spec: FEAT-NNN — <Feature name>

> Feature from: docs/implementation-plan.md · Epic: <epic>
> Implements: FR-… (list) · Realizes: UC-… · Screens: SCR-…
> Status: Draft | Agreed
> Signed off: <date>            ← only when Status is Agreed, set on the user's explicit "Agree"
> Date: <last edit date>

## 1. Summary
Two or three sentences: who this is for, what they can do after it ships,
and why it matters (from the plan's value statement).

## 2. User stories

### US-NNN-1: <short title>
As a <role>, I want <goal>, so that <benefit>.
Traces: FR-…, UC-… (main / alt n / exc n)

Acceptance criteria:
- **AC-NNN-1.1** Given <business state>, when <user action>, then <what the user sees / what the business state becomes>.
- **AC-NNN-1.2** …

### US-NNN-2: …

## 3. Business rules
| ID | Rule | Source |
| :-- | :-- | :-- |
| BR-NNN-1 | A shop name is unique across the marketplace, ignoring case. | FR-SHOP-002 / Q3 |

## 4. Out of scope
What this feature deliberately does not do, and which feature (if any) does it later.

## 5. Q&A log
| # | Asked by | Question | Answer | Date | Change made |
| :-- | :-- | :-- | :-- | :-- | :-- |
| Q1 | Agent | … | … | … | Added BR-NNN-2 |
| Q2 | User | … | … (cites FR-…) | … | None |

Open, deferred: questions the user chose to leave open, in their words.

## 6. SRS amendments
| ID | Change | Before → after | SRS version |
| :-- | :-- | :-- | :-- |
| FR-… | Modified | … → … | 1.4 |

"None" when the Q&A changed no requirement.

## 7. For detailed design
Technical questions that came up during clarification, for detailed-design to
decide (for example "how long is the code valid in storage"). Not answered here.
```

## Rules

- Story and criterion IDs are stable once the spec is Agreed; a later change
  adds new IDs and marks removed ones `(removed)` instead of renumbering.
- Each criterion is one observable outcome. Split "and then" chains.
- Use the project's language for money (amount plus currency) and both
  locales (vi, en) where the user sees text.
