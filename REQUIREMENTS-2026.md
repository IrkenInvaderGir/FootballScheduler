# Football Scheduler Requirements — 2026 Season

Harvested from `Files/Youth Football League Scheduling Data - 2026 - Program Information.csv`
(a program-intake survey covering 15 schools, submitted May 31 – Jul 3, 2026).

Two derived data files were generated alongside this doc and should be treated as the
2026 replacements for last year's `Cleaned_Home_Hosting_Availability.csv`:
- `Files/2026_ProgramSummary.csv` — one row per school: team counts, night preferences, special requests, travel/start-time constraints.
- `Files/2026_HostingCapacity_Long.csv` — one row per (school, date) with games-per-night capacity, already zero-filtered.

**This survey does not restate away-availability-by-date, or the hard season-structure
rules (games/bye count, cross-grade ban, etc.) the way last year's spreadsheet's Tab 3/5
did.** Where 2026 data is silent, this doc carries forward the 2025 rule and flags it as
an assumption — see **Open Questions** at the end before building anything on it.

---

## Team Roster & Naming Convention

Per your instruction: any school with more than one team at a grade level is named
`School` + `GradeLevel` + a letter (`A`, `B`, ...). Schools with a single team at a
grade level keep the plain `School` + `GradeLevel` form (no letter).

| School | 7th Grade Teams | 8th Grade Teams |
|---|---|---|
| Reedsburg | Reedsburg7 | Reedsburg8 |
| Mt Horeb | MtHoreb7A, MtHoreb7B | MtHoreb8A, MtHoreb8B |
| Stoughton | Stoughton7 | Stoughton8 |
| Waunakee | Waunakee7A, Waunakee7B | Waunakee8A, Waunakee8B |
| Middleton | Middleton7 | Middleton8A, Middleton8B |
| Beaver Dam | BeaverDam7 | BeaverDam8A, BeaverDam8B |
| Sun Pr West | SunPrWest7 | SunPrWest8A, SunPrWest8B |
| Lodi | Lodi7 | Lodi8 |
| Verona | Verona7A, Verona7B | Verona8 |
| Portage | Portage7 | Portage8 |
| Monona Grove | MononaGrove7 | MononaGrove8 |
| Milton | Milton7 | Milton8 |
| Sun Pr East | SunPrEast7A, SunPrEast7B | SunPrEast8 |
| Oregon | Oregon7 | Oregon8 |
| Sauk Prairie | SaukPrairie7 | SaukPrairie8 |

**19 seventh-grade teams + 20 eighth-grade teams = 39 total** (up from 38 in 2025).
Confirmed against the survey's own totals row.

Note the grade counts flipped/shifted for several schools vs. 2025 — e.g. Verona is now
2×7th/1×8th (was 2×7th/2×8th), Sun Pr East is now 2×7th/1×8th (was 1×7th/1×8th),
Middleton and Beaver Dam are now 1×7th/2×8th. Don't reuse the 2025 `Team.Initialize()`
list as-is.

---

## Season Structure

- **9 possible weeks**, Monday–Thursday each week, plus one fixed Saturday special date.
  Week 9 is explicitly labeled **"ONLY AS NEEDED"** — an overflow/makeup week, not a
  guaranteed playing week.

| Week | Dates (Mon–Thu) |
|---|---|
| 1 | Mon 8/24 – Thu 8/27 2026 (+ **Sat 8/22 Verona Jamboree**, off-grid) |
| 2 | Mon 8/31 – Thu 9/3 |
| — | *(Labor Day week, Sep 7–10, has no hosting columns — skipped)* |
| 3 | Mon 9/14 – Thu 9/17 — **SP East @ SP West rivalry week** |
| 4 | Mon 9/21 – Thu 9/24 |
| 5 | Mon 9/28 – Thu 10/1 |
| 6 | Mon 10/5 – Thu 10/8 |
| 7 | Mon 10/12 – Thu 10/15 |
| 8 | Mon 10/19 – Thu 10/22 |
| 9 (as-needed) | Mon 10/26 – Thu 10/29 |

- Per-school, per-date hosting capacity (0/1/2 games) is in `2026_HostingCapacity_Long.csv`.
  Capacity varies week to week per school (not a flat "lights = always 2" rule like the
  2025 model assumed) — e.g. Lodi's capacity swings from 2 down to 0 depending on the date.

---

## Fixed / Required Games

### Verona Jamboree — Saturday, August 22, 2026
- Hosted at Verona High School turf field, **outside** the normal Mon–Thu grid (Verona's
  own Week 1 Mon–Thu capacity is 0 — all their week-1 hosting happens here instead).
- Grades 7–8 time slots (grades 4–6 slots exist but are out of scope):
  - Grade 7, game 1: 3:30–5:00 PM
  - Grade 7, game 2: 5:00–6:30 PM
  - Grade 8: 7:00–8:30 PM
- **Unlike 2025, opponents are not specified in the data.** Last year's code hardcoded
  specific matchups (Milton, Mt Horeb Red) for the Verona kickoff — this year that
  information simply isn't given. The scheduler needs to assign opponents for these 3
  slots through the normal matching logic, honoring this as a fixed hosting date/capacity
  override rather than a fixed matchup.
- Verona explicitly wants their 2 seventh-grade teams to play **back-to-back at the same
  site** when possible (matches this Jamboree's own structure) — treat as a standing soft
  preference, not just a one-off for this date.
- Verona also asked for the overall home/away balance to favor them more than the last two
  years — soft preference, doesn't have to hard-code a game.

### Sun Prairie Rivalry — Week 3 (Sep 14–16), Bank of Sun Prairie Stadium
- **Changed from 2025**: rivalry moved from Week 4 → **Week 3**, and the host direction
  flipped: **Sun Pr West hosts Sun Pr East** (2025 had SP East hosting SP West).
- Capacity is elevated to 2 games/night for SP West that week specifically (confirmed in
  the capacity data: Mon/Tue/Wed all show 2, vs. 1 most other weeks).
- Applies to both 7th and 8th grade, same as 2025.

### New hard rule: SP East / SP West can never both host on the same day
Sun Pr West's submission is explicit: *"due to the shortage of officials, SP West and SP
East teams cannot host games at the same time on a given day. When West is home, East
must be away and vice versa."* Sun Pr East's submission corroborates this independently
("align our home schedule with west sp so we don't host home games the same week they
do"). This is stronger than anything in the 2025 rules and needs to be enforced for
**every** week, not just the rivalry week.

---

## Per-School Constraints Worth Encoding

These come straight from the survey's free-text fields (`Files/2026_ProgramSummary.csv`)
and are new information relative to 2025:

| School | Constraint |
|---|---|
| **Sauk Prairie** | Hard requirement: **must host exactly 2 games per night, never 1** — "our officials won't do just 1 game." Since Sauk has only 1 team per grade, this ties their 7th and 8th grade schedules together on host nights (can't schedule one grade's home game there without the other). |
| **Monona Grove** | Wants 7th and 8th grade home games on the **same night** ("for referee purposes"), Tuesday preferred. Phrased as a preference but functionally similar to Sauk's rule. |
| **Waunakee** | Shared coaching staff (Yes) across both grade pairs. Explicit ask: **stack away games at the same location** when their two same-grade teams travel. Home capacity is only ever offered Mon/Tue (never Wed/Thu), always at 2-game capacity — consistent with both same-grade teams hosting together. |
| **Beaver Dam** | Shared coaching staff (Yes) for the 2 eighth-grade teams. Only hosts Tuesdays. Requires the 2 eighth-grade teams to play **at the same location, back-to-back with staggered start times** — this reads as a hard operational requirement, not just the 2025 "travel same day" preference. Bus/school-program travel constraint on away games too. |
| **Middleton** | Shared coaching staff (Yes) for the 2 eighth-grade teams, but their own hosting capacity never exceeds 1 game/night — so the two Middleton 8th-grade teams can only ever host on separate nights regardless. Hard start-time rule: home games start at exactly 7:00 PM; away games no earlier than 6:00 PM. |
| **Mt Horeb** | No lights — every hosting date is capped at 1 game/night, and games must start early (latest start 5:30 PM). No school hosts a doubleheader here this year (2025 code assumed some multi-game capacity for Mt Horeb; that's gone). |
| **Oregon** | No lights, very tight start window (4:30–5:00 PM only), Mon/Tue-only hosting, 1 game/night. |
| **Portage** | Wants doubleheaders to start at 5:00 & 6:30 PM specifically; single home games can start later; prefers away games stacked at one site. |
| **Milton** | Least constrained program — available Mon–Thu, up to 2 games/night almost every date, latest start 10:00 PM. |
| **Lodi** | Has an unconfirmed "fall tailgate" special date (a Wednesday night, single game ~7:30 PM) — **not yet finalized**, treat as TBD rather than a locked rule. |
| **Verona** | Travel-distance preference: families don't want opponents more than ~45 minutes away for weeknight games once school has started. No distance/geography data exists anywhere else in this system to actually enforce that — flag as currently unenforceable without adding a distance table. |

---

## Decisions (confirmed 2026-07-10)

1. **Games-per-team target: stays 7 games + 1 bye across weeks 1–8.** Week 9 is
   emergency-only for *games* (weather cancellations, etc.) — the scheduler should not
   plan to schedule actual games there by default.
   - **Byes should default to Week 9** wherever possible: since no games are planned
     there, every team is free that week, so it's the natural home for the formal "bye"
     record. Only fall back to placing a team's bye within weeks 1–8 if something about
     that team's situation (e.g. hosting-capacity crunch, a required matchup) makes a
     Week 9 bye infeasible. This keeps weeks 1–8 fully dedicated to real games (7 for
     every team, spread across those 8 weeks) while Week 9 stays clear for makeups.
2. **All 2025 carryover rules hold unchanged**: no cross-grade games, no same-school
   games (except SP East/West), 3-home-game soft minimum for everyone but Sauk.
3. **Verona Jamboree opponents (Sat 8/22) are chosen by the scheduler**, not fixed. Treat
   the Jamboree purely as a fixed date/site/time reservation (2× grade 7 slots, 1× grade 8
   slot) and assign opponents under the normal matching rules.
4. **Lodi's tailgate date is ignored for now.** Schedule Lodi using only its regular
   hosting capacity data; add the tailgate manually once Lodi confirms a date.

I haven't touched `Program.cs` or the `Models/` classes yet — this doc and the two CSVs
are just the harvested data. Next step is wiring the 2026 roster, week grid, and capacity
data into the scheduler, and addressing the correctness issues from the earlier code
review at the same time.
