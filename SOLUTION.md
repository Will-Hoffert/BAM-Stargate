# Stargate ACTS: solution notes

This document covers how to run the solution, the assumptions it makes, what was wrong in the provided code and how it was fixed, the design decisions and their reasons, how it's tested, and what would come next. The exercise text is in [README.md](README.md), unchanged.

## Contents

1. [Summary](#summary)
2. [Run it on a new machine](#run-it-on-a-new-machine)
3. [Try it out](#try-it-out)
4. [Assumptions and interpretations](#assumptions-and-interpretations)
5. [What was wrong, and the fixes](#what-was-wrong-and-the-fixes)
6. [Design decisions](#design-decisions)
7. [Testing](#testing)
8. [Before deploying to an existing database](#before-deploying-to-an-existing-database)
9. [Follow-ups](#follow-ups)
10. [Commit history](#commit-history)

## Summary

**API (required).** All five endpoints work. Person update was missing and is now a rename, `PUT /Person/{name}`. The seven rules are enforced in code and, where it matters, by the database. The review found 25 flaws in the provided code. 23 are fixed, one per commit, starting with SQL injection, a broken endpoint, crashes and unenforced rules; the other 2 are follow-ups (see the [fix table](#what-was-wrong-and-the-fixes)).

| Method | Path | What it does | Errors it can return |
|---|---|---|---|
| GET | `/Person` | Everyone, with their astronaut details where they have any | |
| GET | `/Person/{name}` | One person | 404 |
| POST | `/Person` | Adds a person. Body: a JSON string, e.g. `"John Doe"` | 400, 409 |
| PUT | `/Person/{name}` | Renames a person. Body: the new name as a JSON string | 400, 404, 409 |
| GET | `/AstronautDuty/{name}` | A person and their duties, newest first | 404 |
| POST | `/AstronautDuty` | Adds a duty: `{ "name", "rank", "dutyTitle", "dutyStartDate" }` | 400, 404, 409 |
| GET | `/ProcessLog?take=50` | The newest process-log entries (1–200, default 50) | |

Every response, success or error, uses the existing envelope `{ success, message, responseCode }`. Unexpected failures return 500 with a generic message; the details go only to the server log.

**Tasks.**
- **Database:** generated from EF Core migrations: the provided initial one, plus five that each make one schema change.
- **Rules:** enforced (see [the seven rules](#the-seven-rules)).
- **Defensive coding:** input validation, parameterized SQL, cancellation, and database constraints that close race conditions.
- **Tests:** 120 API tests with **99.61% line / 96.42% branch coverage** (migrations excluded; the task asked for over 50%). There are also 107 UI tests.
- **Process logging:** every request's outcome (success, rejection or exception) goes to a `ProcessLog` table.

**UI (encouraged).** An Angular 22 app in `ui/`:
- **Pages:** a Possible Astronauts list (search, sort, status) and a timeline for each person's duties.
- **Editing:** dialogs to add a person, rename them, and add a duty.
- **Progress:** a progress bar while the API is working.
- **Errors:** clear messages from the API's own rules.

**How it was built.**
- **One problem per commit:** each fix or feature came with its test, written first and seen failing.
- **Mutation checks:** most changes were also checked by temporarily breaking the code to confirm the test catches it.
- **Migrations:** each was rehearsed on a populated copy of the database before being committed.

## Run it on a new machine

### Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 8.0.100 or later 8.0.x | Pinned by `global.json` (`rollForward: latestFeature`), so a .NET 9 or 10 SDK won't be used by accident. |
| Node.js | 24.15 or later 24.x | `ui/package.json` declares it, and `ui/.npmrc` sets `engine-strict`, so `npm ci` stops on any other Node version. `ui/.nvmrc` names 24 for version managers. |
| npm | 11 | Named in `ui/package.json` (`packageManager`). Comes with Node 24. |
| Git | any recent | |

No global Angular CLI or global `dotnet-ef` is needed; both are pinned inside the repo. Optional: VS Code with C# Dev Kit, Angular Language Service and SQLite Viewer (for looking at `api/starbase.db`).

### API

From the repository root:

```bash
dotnet tool restore                              # installs the pinned dotnet-ef (8.0.4)
dotnet ef database update --project api          # creates api/starbase.db from the migrations
dotnet run --project api --launch-profile http   # http://localhost:5204, Swagger at /swagger
```

Use the `http` profile. With the `https` profile, `UseHttpsRedirection` redirects the UI's `http` calls, and browsers refuse to follow a redirect during a CORS preflight. The database isn't created or migrated at startup (see [operations follow-ups](#operations-and-tooling)).

### UI

In a second terminal, with the API running:

```bash
cd ui
npm ci       # installs exactly what package-lock.json lists
npm start    # http://localhost:4200
```

The UI reads the API's address from `ui/public/config.json` when it starts (`"apiBaseUrl": "http://localhost:5204"`). The API allows `http://localhost:4200` through CORS in Development only (`api/appsettings.Development.json`).

### Tests

```bash
# API: all tests, then the same with coverage and the 50% line-coverage gate
dotnet test Stargate.sln
dotnet test Stargate.sln /p:CollectCoverage=true /p:Threshold=50 /p:ThresholdType=line

# UI: tests once, then a production build
cd ui
npm test -- --watch=false
npm run build
```

- **The coverage command fails below 50%,** so it can be a CI gate.
- **If the API isn't running:** the API tests don't need it; they run against in-memory SQLite.
- **If coverage reports 0% while the tests pass:** something else is holding the test build's `StargateAPI.dll` open, for example an editor's test explorer. Close it, or add `--output <another folder>` to the coverage command.

The UI handles calendar dates carefully (see [dates](#dates)), and its date tests can only catch a day-shift bug in a time zone other than UTC. Run them in one ahead of UTC and one behind:

```bash
TZ=Australia/Sydney npm test -- --watch=false       # bash
TZ=America/Los_Angeles npm test -- --watch=false
```

```powershell
$env:TZ = 'Australia/Sydney'; npm test -- --watch=false; Remove-Item Env:TZ   # PowerShell
```

## Try it out

Through the UI (or Swagger at `http://localhost:5204/swagger`):

1. Add **John Doe**, then give him these duties:
   - **Commander**, rank 1LT, starting 2020-01-01;
   - **Pilot**, rank 1LT, starting 2022-06-01;
   - **RETIRED**, rank 1LT, starting 2025-01-01.
2. Expected:
   - **Commander** ends 2022-05-31.
   - **Pilot** ends 2024-12-31.
   - **Career end date:** 2024-12-31.
   - **On his timeline:** a "Retired" marker at the top, no current duty, and no "Add duty" button.
3. Then the cases that should be refused:

| Try | Expected |
|---|---|
| Add "John Doe" again | 409 "A person named 'John Doe' already exists." |
| Add "john doe" | 409 (names are case-insensitive) |
| Add "Miles O'Brien" | Works (the provided code broke on the apostrophe) |
| A duty for someone who doesn't exist | 404 "No person named 'X' exists." |
| A duty starting on or before the current duty's start | 409 "A new duty for 'X' must start after their current duty, which started yyyy-MM-dd." |
| Any duty after RETIRED | 409 "'X' retired on yyyy-MM-dd and can't be given new duties." |
| RETIRED as someone's first duty | 400 "'X' has no astronaut duties yet, so RETIRED can't be their first duty." |

Afterwards, `GET /ProcessLog` (or the `ProcessLog` table in SQLite Viewer) shows one row per request: Information for successes, Warning for the refusals.

## Assumptions and interpretations

### Requirements

- **"Add/update a person by name"** means add, and rename. There's no other person field to update. A rename is `PUT /Person/{name}`, with the new name as a JSON string body, mirroring `POST /Person`.
- **Renaming a person to their exact current name** returns 200 and changes nothing. A change of case only ("john doe" to "John Doe") is a real rename and is allowed.
- **Two renames of the same person at the same moment:** the last one wins. The old name in the URL already stops a rename based on stale data (it returns 404), and optimistic concurrency is a [follow-up](#api-and-data).
- **People are identified by name**, as the README and the original API do, both in API URLs and in the UI's addresses (`/people/John%20Doe`).
- **The data arrives from an external service** (README), so the API is the system of record for the rules: every rule is enforced in the API, and the UI only repeats the simple ones for quicker feedback.
- **No authentication** is in scope for the exercise. It's the first security follow-up.
- **The UI was "encouraged".** It covers the README's three UI items: a production-quality Angular app, calls to retrieve an individual's duties, and displaying progress and results. A process-log viewer was considered and left as a [follow-up](#user-interface).

### The seven rules

The README numbers every rule "1."; they're numbered 1–7 here in the order written.

| # | Rule | How it's interpreted and enforced |
|---|---|---|
| 1 | A Person is uniquely identified by their Name. | Names are unique **ignoring case** and **surrounding spaces**: names are trimmed, and "john doe" counts as "John Doe". Enforced by a pre-processor check (409), then by a unique index with SQLite's `NOCASE` collation, which also catches two requests racing. A new name must not be blank and is at most 100 characters (400). The limit is enforced in code because SQLite ignores declared column lengths. Lookups by name are case-insensitive and ignore surrounding spaces. |
| 2 | A Person who has not had an astronaut assignment will not have Astronaut records. | A person exists without being an astronaut. Adding or renaming a person never creates astronaut records. The only path that creates `AstronautDetail` is a person's **first duty**, and it saves the detail and the duty in one transaction. No endpoint creates or edits `AstronautDetail` directly. Reads use `LEFT JOIN`, so people with no assignment are still returned, with null dates. Their rank and duty title come back as an empty string (`""`) rather than null, a known issue ([follow-up 1](#first)). `GET /AstronautDuty/{name}` returns 200 with an empty list for them (404 only means no such person). The database doesn't enforce this rule itself; it holds because there's one atomic write path. |
| 3 | A Person will only ever hold one current Astronaut Duty Title, Start Date, and Rank at a time. | `AstronautDetail` holds the current values, one row per person (unique index on `PersonId`). A partial unique index allows only one open duty per person (`AstronautDuty(PersonId) WHERE DutyEndDate IS NULL`). Two duties saved at the same moment get a 409 instead of producing two current duties. |
| 4 | A Person's Current Duty will not have a Duty End Date. | "Current duty" means the duty with no end date, which is the latest one received. It does **not** mean "the duty whose dates include today"; see the future-start-date gap below. |
| 5 | A Person's Previous Duty End Date is set to the day before the New Astronaut Duty Start Date when a new Astronaut Duty is received for a Person. | Applied when a duty is added. A new duty must start **after** the current duty's start date (409 otherwise); otherwise the previous duty would end before it began. Starting the very next day is allowed, which gives a one-day duty. A duty with the same title and start date for the same person is a duplicate (409). |
| 6 | A Person is classified as 'Retired' when a Duty Title is 'RETIRED'. | Recognized in any case ("retired", "Retired") and stored as `RETIRED`. **RETIRED is final:** any duty after it is refused (409). |
| 7 | A Person's Career End Date is one day before the Retired Duty Start Date. | Set when RETIRED is added. **RETIRED can't be someone's first duty** (400), because their career would end the day before it began. Someone who retired before records began must first be given an earlier duty. |

### Dates

- **Duty and career dates are calendar dates.** Any time of day sent to the API is dropped before saving.
- **The UI sends dates as `yyyy-MM-dd`,** built from the local year, month and day, and keeps dates from the API as text. Converting through UTC (`toISOString()`, or `new Date('2020-01-01')`) moves a date by a day in some time zones.
- **A client that sends a date with a UTC offset** (`2020-01-01T00:00:00+10:00`) can still be shifted a day by the API. `DateOnly` is a [follow-up](#api-and-data).
- **Future start dates are currently accepted, and that is a known gap.** Because "current" means "no end date" (rule 4), a duty posted today that starts next month becomes current immediately, and the person's current rank and title switch to it. The UI shows such a duty's length as "Not started yet". Rejecting future start dates is the first [API follow-up](#api-and-data); the UI's date picker deliberately allows them until then, so the UI and API agree.
- **Durations in the UI** are whole years and months and count the last day, so Jan 1, 2020 to May 31, 2022 is "2 years 5 months". Anything shorter reads "Less than a month". The current duty counts to today.

### Errors

| Status | Meaning | Example message |
|---|---|---|
| 400 | The request breaks an input rule, or RETIRED as a first duty | "A person's name is required." |
| 404 | No person with that name | "No person named 'John Doe' exists." |
| 409 | A conflict with existing data or another request | "A person named 'John Doe' already exists." |
| 500 | Anything unexpected | "An unexpected error occurred." (the details go only to the server log) |

All of them use the `BaseResponse` envelope. Requests that ASP.NET itself rejects before our code runs (malformed JSON, a missing body) also return the envelope, with ASP.NET's own wording.

### Process logging

- Every request handled by MediatR is logged with:
  - its outcome;
  - the request as the caller sent it;
  - how long it took;
  - for failures, the actual exception.
- **Levels:**
  - **Information:** success.
  - **Warning:** refused by a rule (400/404/409), or cancelled by the caller.
  - **Error:** anything unexpected.
- **`Message`** is what the caller was told (for a 500, the generic message). **`Exception`** is what was actually thrown. The API never returns `Exception`.
- **Not logged:** requests that ASP.NET rejects before they reach MediatR (see [decisions](#process-logging-1)), and reads of the log itself.

### UI

- **"Possible Astronauts"** lists everyone, astronaut or not. Each person's status is worked out from their data:
  - **Retired:** they have a career end date.
  - **Active:** they have a duty.
  - **Not an astronaut yet:** neither.

  **Known issue:** against the running API, people with no duties currently show as **Active**, because the API sends their duty title as `""` rather than null ([follow-up 1](#first)).
- **Retired people can't be offered a new duty,** so their timeline shows a note instead of the button. They can still be renamed.
- **The UI repeats the simple rules for quicker feedback:** name, rank and title lengths, required fields, and a start date after the current duty. It uses the API's exact messages, and the API stays the authority. Duplicates and the other rules are checked only by the API, and its message is shown.

### Environment

- **Local development uses HTTP only.** The `https` profile needs a trusted development certificate and breaks CORS preflights through its redirect.
- **Single-instance SQLite file** (`api/starbase.db`), as provided.
- **The stack stays on .NET 8,** as provided. It reaches end of support on 2026-11-10; upgrading is the first follow-up.
- **License-driven version limits:**
  - MediatR stays on 12.x, because 13 and later need a commercial license.
  - FluentAssertions 8 isn't used, for the same reason; the tests use plain xUnit assertions.

## What was wrong, and the fixes

Each fix is its own commit with its regression test. Commits are listed by short hash; see the [commit history](#commit-history).

<details>
<summary>Flaws found in the provided code (25)</summary>

| # | Flaw | Fix | Commit |
|---|---|---|---|
| 1 | The model and the initial migration disagreed: `CareerStartDate` was nullable in the database but required in the model, so the next migration would have changed it unannounced. | Migration `MakeCareerStartDateRequired`, before any other change. | aa47e39 |
| 2 | **SQL injection:** names were interpolated into Dapper SQL. `O'Brien` broke queries, and `x' OR '1'='1` matched everyone. | All queries parameterized. | e132c8f |
| 3 | Every error became 500 with the raw exception text (leaking internals), client errors were reported as 500, and `POST /AstronautDuty` had no handling at all. | One `IExceptionHandler`: 400/404/409 with a specific message, and 500 with a generic one. | f09cc6d |
| 4 | `GET /AstronautDuty/{name}` sent the wrong query (`GetPersonByName`) and never returned duties. | Sends `GetAstronautDutiesByName`. | b990ed7 |
| 5 | An unknown person in the duty lookup crashed with a `NullReferenceException` (500). | 404. | 3ee299a |
| 6 | An unknown person in `GET /Person/{name}` returned 200 with `person: null`. | 404. | 56e8eb4 |
| 7 | EF entities were returned straight from the API, leaking the persistence model. | DTOs. | 7750c06 |
| 8 | `CreatePersonPreProcessor` never ran: MediatR 12 only registers pre-processors when asked. Duplicate names were accepted (rule 1). | Pre-processors registered automatically, plus the behavior that runs them. | 6bacaeb |
| 9 | Nothing in the database kept names unique, so two requests could both pass the check and insert. | Unique index, with a violation returning 409 (08c3276), made case-insensitive with `NOCASE` (8794354). | 08c3276, 8794354 |
| 10 | The duplicate-duty check ignored which person the duty was for, and compared timestamps rather than dates. | Scoped to the person, comparing dates. | 5dbbe93 |
| 11 | The duty handler passed detached Dapper rows to EF's `Update()`, mixing data-access styles, and crashed for an unknown person. | Tracked EF entities; 404. | af9c4ad |
| 12 | A backdated or same-day duty set the previous duty's end date before its own start (rules 4–5). | 409 unless the new duty starts after the current one. | 475b496 |
| 13 | `"RETIRED"` was a case-sensitive magic string (rule 6). | `DutyRules.IsRetirement`, any case, stored as `RETIRED`. | a1d5d06 |
| 14 | RETIRED as a first duty set the career end to the start date instead of the day before (rule 7). | Refused with 400. | a2a20a5 |
| 15 | Duties were accepted after retirement, leaving the career end date stale. | 409 (RETIRED is final). | 802bf82 |
| 16 | Nothing in the database prevented two current duties for one person (rules 3–4). | Partial unique index, plus a transaction that closes the old duty before inserting the new one. | 870f945 |
| 17 | No input validation: blank names, oversized fields, and a missing `DutyStartDate` saved as 0001-01-01. Requests ASP.NET couldn't read got its own ProblemDetails format instead of the envelope. | Binding errors returned in the envelope (a8e14bb); names validated and trimmed (fa717f3); duty fields validated and trimmed (04f353f). | a8e14bb, fa717f3, 04f353f |
| 18 | Blocking EF calls inside async pre-processors, and every handler got `CancellationToken.None`. | Async calls, with the request's token passed all the way down. | 5c4eea6 |
| 19 | Nullable-reference warnings. | Resolved; the build has 0 warnings. | 18f0fc4 |
| 20 | A handler exposed its database context as a `public` field. | Private. | d140e96 |
| 21 | Dead `SeedData`: never called, and non-deterministic if it had been (`DateTime.Now` in `HasData`). Plus unused usings. | Removed. | d20e5d8 |
| 22 | The "update a person" requirement was missing. | `PUT /Person/{name}`. | 5ef3edc |
| 23 | No process logging. | `ProcessLog` table (da8aa35), a logging pipeline behavior (7ddc340), `GET /ProcessLog` (8a433df). | da8aa35, 7ddc340, 8a433df |
| 24 | Out-of-date packages (EF Core 8.0.4; a model snapshot from EF 7.0.15), and the business layer throwing ASP.NET's `BadHttpRequestException`. | Not changed yet: both are [follow-ups](#follow-ups). | |
| 25 | People with no astronaut record come back with `""` for `currentRank` and `currentDutyTitle`, but `null` dates. `PersonAstronaut` defaults both strings to `string.Empty`, and Dapper leaves a property at its default when the column is NULL. Found by the fresh-clone rehearsal of the run instructions. | Not changed yet: [follow-up 1](#first). | |

</details>

## Design decisions

### Patterns kept, and made consistent

- **MediatR commands and queries,** with **pre-processors** doing validation before the handler runs. The pre-processors write the cleaned values (trimmed names, dates without a time) back to the request.
- **Commands use EF Core; queries use Dapper.** Every Dapper query is parameterized. The one command that mixed the two styles now uses EF only.
- **The `BaseResponse` envelope** for every response, errors included, since existing callers and the UI rely on it.
- **Errors are `BadHttpRequestException(message, status)`,** passed through by one `GlobalExceptionHandler`. Replacing them with domain exceptions so the business layer doesn't depend on ASP.NET is a [follow-up](#api-and-data).

### Data integrity

- **The database backs up every rule that a race could break:**
  - the case-insensitive unique index on names;
  - the one-open-duty index;
  - the one-detail-per-person index.

  A violation becomes a 409 with a message, not a 500.
- **Adding a duty runs in one transaction with two saves:** first close the previous duty, then insert the new one. That way the one-open-duty index never sees two open duties, and it doesn't depend on EF's internal ordering.
- **The transaction starts after the reads,** deliberately. SQLite's `BEGIN IMMEDIATE` would make a race loser wait, re-read, and then close the winner's duty past checks it never ran.
- **Length limits are enforced in code.** SQLite ignores declared lengths, and `HasMaxLength` would rebuild the Person table for no gain.
- **Each migration makes one schema change,** and each was rehearsed on a populated copy of the database (row counts compared, rollback tested).

### Process logging

1. **Implemented as a MediatR pipeline behavior registered first,** so it also records requests that the pre-processors reject.
2. **Requests ASP.NET rejects before our code runs aren't logged:** malformed JSON, a blank route value, the wrong HTTP method. They never reach MediatR, they appear in ASP.NET's own logs, and they aren't operations of ours that succeeded or failed. They could be added from the `InvalidModelStateResponseFactory`.
3. **A request the caller cancels** is a Warning, "The request was cancelled.": not our failure, but the operation didn't complete. A cancellation the caller didn't cause is an Error.
4. **The log row is saved through its own DbContext.** A failed request's pending changes are never saved along with its log entry. A mutation check proved this: with the shared context, the entry for a request that lost a race disappeared.
5. **A failure to write the log never changes the response.** It goes to `ILogger` instead. A mutation check proved this too: rethrowing turned a 200 into a 500.
6. **The log write is awaited, not queued.** It costs a few milliseconds per request, but the entry exists before the response is sent and tests aren't racy. A background queue is a [follow-up](#api-and-data) if latency matters.
7. **No `IProcessLogWriter` interface.** One class is simpler, and the test for a failing log write breaks the real table instead of swapping in a fake.
8. **`RequestJson` is captured before the pre-processors clean it up,** so it shows what was sent, and is capped at 4,000 characters.
9. **`GET /ProcessLog`:**
   - it is not itself logged (marker interface `ISkipProcessLogging`), so reading the log doesn't fill it;
   - it clamps `take` to 1–200 rather than rejecting;
   - it never returns the `Exception` column.

### CORS

- **CORS is configured in the API rather than through an Angular dev-server proxy,** so the UI and API can be hosted separately and development behaves like production.
- **Only `GET`, `POST`, `PUT` and the `Content-Type` header are allowed,** to be widened only when the API grows.
- **No origins are allowed outside Development.** A deployment names its UI's origin, for example `Cors__AllowedOrigins__0=https://ui.example.com`.
- **CORS is a browser rule, not API security.** Authentication is a follow-up.

### UI

- **Stack:**
  - **Framework:** Angular 22 with standalone components, zoneless change detection and signals throughout. Data loads with `rxResource`, which has been stable since 22.0.
  - **Components:** Angular Material, with a Material 3 theme that follows the computer's light or dark setting. Our styles use only Material's color variables.
  - **Tests:** Vitest.
- **The API address is configuration, not part of the build.** `config.json` is read at startup, so one build can be deployed anywhere by replacing that file. This is the same idea as the API's `appsettings`. It's fetched with `no-cache`, so a replaced file takes effect, and a missing or broken one shows "Stargate ACTS couldn't start" with the reason instead of a blank page.
- **One typed API client** turns every failure into an `ApiError` whose message can be shown as is:
  - the API's own message;
  - "Can't reach the Stargate API. Check that it's running." when there's no response;
  - a general message otherwise.

  Person names are sent as a JSON string, because Angular would send a bare string as plain text, which the API rejects.
- **A progress bar shows whenever a call is in flight.** It shows immediately, with no delay. It counts down however a call ends, including errors and cancellations, and its space is reserved so the page doesn't move.
- **Pages load on demand.** With the Material table in the starting bundle, the app went over Angular's 500 kB budget; lazy loading keeps the starting bundle at about 408 kB.
- **Dialogs, not separate pages,** for adding a person, renaming, and adding a duty:
  - they use Angular's signal forms (stable since 22.0);
  - New person and Rename share one dialog;
  - the API's messages show inside the dialog, and a taken name shows under the field.
- **After a rename, the old address is replaced in the browser history,** so Back doesn't lead to a page that no longer exists.
- **The Node version is enforced:** `engine-strict` makes `npm ci` fail on the wrong Node.
- **Angular CLI usage analytics are turned off** for the workspace.
- **Fonts come from Google Fonts,** as Angular Material sets up. Bundling them is a [follow-up](#user-interface).

## Testing

### API (120 tests)

- **Setup:** xUnit and `WebApplicationFactory`. Each test class gets its own in-memory SQLite database, built by the real migrations, so tests exercise the real SQL, including Dapper's.
- **HTTP-level tests are preferred.** Handler- and behavior-level tests cover what HTTP can't reach, such as races and cancellation.
- **Test classes:**
  - **People:** endpoints, rules, validation, rename.
  - **Duties:** endpoints, rules, validation, the handler.
  - **Cross-cutting:** SQL injection, exception handling, cancellation, CORS, process logging (the table, the behavior and the endpoint).
- **Coverage:** 99.61% line, 96.42% branch, 98.37% method. Migrations are excluded. The command is in [Tests](#tests).
- **The most impactful code, tested most heavily:**
  - **`CreateAstronautDutyPreProcessor` and its handler:** every duty rule, the transaction, races and the duplicate check.
  - **The person pre-processors and handlers:** uniqueness, validation, and the race caught by the index.
  - **`GetAstronautDutiesByNameHandler`.**
  - **`GlobalExceptionHandler`:** the status codes, and no internals in a 500.
  - **`ProcessLoggingBehavior`:** levels, the separate context, and failure isolation.
- **Test-first:** each fix's test was seen failing first ("red"). Mutation checks (temporarily breaking the code) confirmed the important tests catch real mistakes.

### UI (107 tests)

- **Framework:** Vitest with Angular's `TestBed`.
- **Pages and dialogs** are tested through Angular Material's test harnesses (the controls a user touches), against a fake API, so every state can be set up: loading, empty, errors, retry.
- **The API client** is tested with Angular's HTTP testing tools: URLs, methods, bodies and error mapping.
- **Dates:** tests that depend on "today" fix the date. The suite passes in UTC, Sydney and Los Angeles, and a mutation check showed why that matters: the `toISOString()` shortcut passed in UTC but sent the wrong day in the other two.
- **Material animations are switched off in tests,** so dialogs close straight away instead of after a timer.

### Not automated

- **A fresh-clone rehearsal** of [Run it on a new machine](#run-it-on-a-new-machine), done by hand. Every command worked as written, and calling the running API exposed the empty-string issue in [follow-up 1](#first).
- **End-to-end browser tests** against a running API (a [follow-up](#quality-and-ci)). Those would have caught follow-up 1 automatically.
- **Migration rehearsals** were done by hand on database copies, with the SQL checks below.

## Before deploying to an existing database

Run these against the existing database first. The migrations added constraints that existing data might break:

```sql
-- Names that differ only by case: the unique NOCASE index can't be created while these exist.
SELECT lower(Name), COUNT(*) FROM Person GROUP BY lower(Name) HAVING COUNT(*) > 1;

-- People with more than one open duty: the one-current-duty index can't be created while these exist.
SELECT PersonId, COUNT(*) FROM AstronautDuty WHERE DutyEndDate IS NULL GROUP BY PersonId HAVING COUNT(*) > 1;

-- Names with surrounding spaces: still found by lookups (which trim), but worth cleaning up.
SELECT Id, Name FROM Person WHERE Name <> trim(Name);
```

If a constraint can't be created, the migration fails and the database stays on the previous migration. This was rehearsed for the one-current-duty index. Apply with `dotnet ef database update --project api`, or with a migration bundle in a deployment pipeline.

## Follow-ups

Not done, roughly in priority order within each group.

### First

1. **People with no duties show as "Active" in the UI.** A known bug, found while verifying the run instructions against a fresh clone.
   - **What:** for a person with no duties, the API returns `"currentRank": ""` and `"currentDutyTitle": ""` instead of `null`, while their dates are `null`.
   - **Why:** the provided `PersonAstronaut` DTO defaults both strings to `string.Empty`, and Dapper leaves a property at its default when the column is NULL.
   - **Effect in the UI:** the UI treats a non-null duty title as Active, so these people show as Active instead of "Not an astronaut yet". A search for "not an astronaut" finds no one, and their blank ranks sort first instead of last.
   - **Why the tests missed it:** the UI's tests use `null` for this case, and no API test covers a person with no duties.
   - **Fix:** make `CurrentRank` and `CurrentDutyTitle` nullable with no default, so a missing astronaut record is `null` in every field, matching the dates and rule 2. Write the API test first (a person with no duties returns `null` rank and title), and add a UI test that uses the API's real response. Optionally, the UI could also treat `""` as missing, as a safety net.
2. **Upgrade to .NET 10 LTS.** .NET 8 reaches end of support on 2026-11-10.
3. **Package updates.** EF Core and the other Microsoft packages are on 8.0.4; the latest 8.0 patch is 8.0.32. Move `dotnet-ef` in step with EF Core, and keep MediatR on 12.x (license).
4. **UI tooling advisory.** `npm audit` reports a high-severity advisory in Vite 8.3.0, which `@angular/build` 22.2.4 (the latest stable) pins. Vite is only the dev server and the build and test tooling; nothing from it ships in the built app. Update `@angular/build` and `@angular/cli` together once a fixed release is stable. `npm audit fix --force` would downgrade `@angular/build` out of step with the CLI, so don't use it.

### Security

5. **Authentication** for the external feed and the UI: an API key or OAuth client credentials. CORS stays limited to named origins, and it isn't security.
6. **An admin role for the process log** once authentication exists. Its entries include request bodies, which means people's names.

### API and data

7. **Reject duty start dates in the future.** Today a future-dated duty becomes current immediately (see [dates](#dates)).
   - **Example from development data:** a duty posted on 2026-10-23 starting 2026-11-01 closed the duty the person was actually serving and showed as Current, "Not started yet".
   - **Fix:** `CreateAstronautDutyPreProcessor` returns 400 "A duty can't start in the future.", allowing one day past the server's UTC date so a caller ahead of UTC can still enter their own today. The UI's date picker gets the same limit in the same change.
   - **If scheduled assignments are a real need,** the alternative is to work out "current" as the duty whose dates include today, at read time. That's a model change away from rule 4's wording.
8. **Stable links after a rename.** People are addressed by name, so after a rename, bookmarks and shared links to the old name show "No person named … exists." (The UI already keeps Back from landing there.) Stable links need ID-based addresses (`/people/7`) and `GET /Person/{id}` (and duties by ID), keeping names for display and search.
9. **Optimistic concurrency for updates:**
   - an ETag on `GET /Person/{name}` and `If-Match` on `PUT`;
   - backed by a version column that EF checks as a concurrency token;
   - a stale update gets 412.

   This matters once a person has fields that can change without changing the URL.
10. **`DateOnly` for duty dates,** so a date sent with a UTC offset can't shift a day.
11. **Idempotency keys** for the external feed, so a retried request can't add a duty twice.
12. **Pagination for `GET /Person`.**
13. **`GET /ProcessLog` pagination and filters:**
    - an ID cursor (`?before=<id>`), relying on the AUTOINCREMENT order the query already uses;
    - filters such as `?level=Error`;
    - a retention policy for the table.
14. **Domain exceptions.** Replace `BadHttpRequestException` in the business layer with `NotFoundException`, `ConflictException` and `ValidationException`, mapped in `GlobalExceptionHandler`, so business rules don't depend on ASP.NET.
15. **Move `BaseResponse` out of the Controllers namespace.** Today the business layer depends on Controllers.
16. **Clearer binding messages.** A few ASP.NET messages still name .NET types, for example "could not be converted to System.DateTime", or the command's class name when the body isn't a JSON object.
17. **Log the requests ASP.NET rejects before MediatR,** from the `InvalidModelStateResponseFactory`, if those should appear in `ProcessLog` too.
18. **Queue log writes in the background** if their few milliseconds per request ever matter.
19. **OpenAPI documentation.** Swagger lists only a bare "200" for every endpoint, because the actions return `IActionResult`:
    - **Declare each endpoint's responses:** the result type for 200, and the 400/404/409 envelope it can really return. Declare 500 once, globally, and add `[Produces("application/json")]`.
    - **Show the input rules in the document:** required fields, the 100-character limits, and `take`'s default and range. Use a small Swagger filter reading the validation constants, not `[MaxLength]` attributes, which would make ASP.NET reject with its own messages.
    - **Add a short description of each endpoint** and the rules it applies, for example that `POST /AstronautDuty` closes the current duty and that RETIRED ends a career.
    - **Add a test that reads the generated `swagger.json`,** including a list of all endpoints, so a new one can't go undocumented.

### User interface

20. **A process activity panel:** a page listing the newest `ProcessLog` entries, with level chips, operation, message, time and duration, plus Refresh. The endpoint and client method already exist. It should be admin-only once authentication exists.
21. **Snackbar (toast) notifications** for messages that should outlive a dialog: confirmations such as "Duty added for John Doe", and failures after a dialog has closed. Today the API's messages show inside the dialog, where they stay until fixed and screen readers announce them.
22. **Bundle the fonts.** Roboto and the Material Symbols icons come from Google Fonts today. Bundling them with the app (for example `@fontsource/roboto` and `material-symbols`):
    - avoids a request to Google from every user's browser, a privacy concern especially under GDPR;
    - keeps the UI working on networks that block Google;
    - simplifies a strict Content Security Policy;
    - pins the font versions with the app.
23. **A cosmetic date-picker quirk.** Material's native date adapter writes the date input's `min` attribute from UTC date parts, so east of UTC that attribute reads a day early. The calendar itself uses local dates and behaves correctly. A custom adapter, or a date-library adapter, would make the attribute match.

### Quality and CI

24. **CI on every push:**
    - **API:** `dotnet build` (0 warnings), `dotnet test` with the 50% coverage gate, and `dotnet ef migrations has-pending-model-changes`.
    - **UI:** `npm ci`, `npm test -- --watch=false` in a time zone ahead of UTC and one behind, `npm run build`, and `npm audit`.
25. **End-to-end browser tests** (for example Playwright) running the UI against a real API and a throwaway database.
26. **Treat nullable warnings as build errors** (`WarningsAsErrors=Nullable`), so the 0-warning state can't drift.

### Operations and tooling

27. **Run migrations as a deployment step.** Use a migration bundle after the [pre-deploy checks](#before-deploying-to-an-existing-database), rather than at application startup. Today the database is created with `dotnet ef database update`.
28. **Health checks, OpenTelemetry and containerization.**
29. **HTTPS:** terminate TLS in front of the API in a deployment. Locally, the `http` profile is used.
30. **Recommend VS Code extensions** in a root `.vscode/extensions.json`: C# Dev Kit, Angular Language Service, SQLite Viewer.

## Commit history

<details>
<summary>All commits, oldest first</summary>

| Commit | Subject |
|---|---|
| 83b98d8 | initial project with the gitignore file |
| 7edb97b | import exercise1 as provided. This is the baseline every later change is measured against. |
| 5c73c1a | Setting .NET SDK 8.0 via global.json |
| 10906da | add Stargate solution |
| 6ccc5d2 | build: add dotnet-ef as a local tool |
| aa47e39 | sync CareerStartDate migration with the model and generate the database |
| e7c8f41 | add xUnit project with in-memory SQLite test host |
| 56b4e64 | tests for CreateAstronautDuty happy paths |
| 082560a | Adding a feature to see the test coverage |
| e132c8f | Made some updates to existing code to account for the possibility of SQL injections. Created tests around this. |
| f09cc6d | Centralize exception handling |
| 3ee299a | fix api issue 404 instead of crash for unknown person in duty lookup |
| b990ed7 | fix: GET /AstronautDuty/{name} returns duties |
| 56e8eb4 | fix: 404 for unknown person in GET /Person/{name} |
| 7750c06 | refactored GetAstronautDutiesByNamResult into a DTOs instead of EF entities |
| 6bacaeb | Run CreatePersonPreProcessor (Rule 1) |
| 08c3276 | Unique index on Person.Name |
| 8794354 | case-insensitive person names |
| 5dbbe93 | scope duplicate-duty check to the person |
| af9c4ad | use tracked EF entities in duty handler; 404 for unknown person |
| 475b496 | Reject duties that don't start after the current duty |
| a1d5d06 | recognize RETIRED case-insensitively |
| a2a20a5 | Reject RETIRED as a first duty |
| 802bf82 | Reject new duties after retirement |
| 870f945 | At most one current duty per person |
| a8e14bb | Return model binding errors in the BaseResponse envelope |
| fa717f3 | Validate and normalize person names |
| 04f353f | Validate astronaut duty input |
| 5c4eea6 | Async EF calls and CancellationToken propagation |
| 18f0fc4 | Resolve nullable-reference warnings |
| d140e96 | Make GetPeopleHandler context private |
| d20e5d8 | Remove dead SeedData and unused usings |
| 5ef3edc | Rename a person via PUT /Person/{name} |
| da8aa35 | Add ProcessLog table - Database |
| 7ddc340 | Log every request outcome to ProcessLog |
| 8a433df | feat(api): GET /ProcessLog for recent entries |
| 86daa9d | CORS for the UI origin from configuration |
| 35f5889 | Initial UI setup - scaffold Angular 22 app |
| 053403a | Pin Node 24 LTS |
| bee3894 | add Angular Material |
| 4afc781 | App shell with toolbar and not-found page |
| b3d7926 | feat(ui): typed Stargate API client with runtime config |
| dd41f38 | don't do analytics |
| 35587cc | Global request progress indicator |
| 5fce50d | Astronauts roster page |
| cb65200 | Astronaut duty timeline |
| f3a1425 | feat(ui): create a person from a dialog |
| 4b018bd | feat(ui): add a duty from a person's timeline |
| 621ce83 | Rename a person from their timeline |

</details>
