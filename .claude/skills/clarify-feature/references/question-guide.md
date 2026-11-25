# Question guide (business clarification)

Walk each area for every user story. Ask only what the documents (SRS, use
cases, earlier specs) leave open — never ask something the SRS already
answers; cite it instead.

## Areas

1. **Actors and permissions** — Who can do this? Signed-in only? Which role
   (buyer, seller, admin)? Can one user act on another's data? What happens
   when someone without permission tries?
2. **Main path** — The steps in the user's words. What does the user see when
   it succeeds? Where do they land next?
3. **Alternate and failure paths, from the user's view** — What if the input is
   wrong, the item is gone, they try twice, they give up halfway, a third
   party (payment, email) is slow or fails? What message do they see?
4. **Limits and validation in business terms** — Lengths, counts, ranges,
   uniqueness, allowed characters, time windows, how many attempts.
5. **States and lifecycle** — Which states can the thing be in? Who moves it
   between them? Can it be undone, edited, deleted? What happens to related
   things?
6. **Money** — Amounts always with currency; rounding; who pays fees;
   refunds.
7. **Notifications** — Who is told, when, through which channel (email,
   in-app), and in which language.
8. **Language and locale** — Texts the user sees in vi and en; date, number
   and currency formats.
9. **Data the user sees** — Which fields are shown, sorted how, how many per
   page, what is hidden from whom.
10. **Out of scope** — What looks related but is not in this feature; which
    later feature owns it.

## Rules

- **No technical questions.** Not "which table", "which endpoint", "sync or
  async", "which library". If the only open question in an area is technical,
  put it in the spec's "For detailed design" list.
- **Concrete options.** Each question gives 2–4 real choices with the
  recommended one first, and a one-line business reason.
- **Stand-alone questions.** Name the story or rule in the question itself; the
  user may see only the question dialog.
- **Smallest set.** Group related gaps into one question; don't ask what an
  earlier answer already settles.
