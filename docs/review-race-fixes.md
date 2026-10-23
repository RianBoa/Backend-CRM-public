# Backend review corrections

Source-only changes, 2026-10-23. Existing uncommitted work retained. No build,
tests, application, Docker, migrations, database connections, brokers, bots or
external services started. No staging, checkout, commit or push performed.

## Security version

User.SecurityVersion is an optimistic concurrency token. Both password routes
(`POST /users/{id}/password`, `PATCH /users/{id}`), role changes and deactivation
advance it atomically with user changes, session revocation and audit.
RefreshToken.SecurityVersion captures the version read during login, or inherited
from a validated refresh session. Do not reread a new version after verifying an
old password. JWT authentication and refresh compare the session version to the
current user version. A concurrent old-password login can finish, but its tokens
are unusable. Existing rows migrate to version 0; future invalidation breaks the match.

## Enrollment and group consistency

Enrollment.Version is a GUID token rotated for every tracked update by both
synchronous/asynchronous SaveChanges. It also detects cross-column races such
as transfer versus freeze. Failed concurrency saves clear tracked pending
entities; losing audit inserts roll back with business writes.

Assignment/transfer, status changes and group PATCH use LearningWriteScope with
serializable isolation. Both assignment and package edits read compatibility
predicates before writing, so PostgreSQL SSI can reject stale assignments and
package edits, including the empty-group case. An enrollment token alone cannot
protect this cross-entity invariant. Existing outer transactions must also be
serializable. Raw SQL/ExecuteUpdate bypass the tracked version mechanism and are
not supported application write paths; maintenance SQL needs separate coordination.

EF concurrency and PostgreSQL serialization/deadlock failures return HTTP 409
concurrent_change. Rollback/disposal clears the losing unit of work. ToolJet must
reload enrollment/group state before retrying; user decisions are not auto-retried.

## Subscription transitions

Cancel, suspend, expire and resume now enforce transitions in the domain.
Canceled/expired access cannot pass through Suspended back to Active. Repeated
commands are no-ops; resume requires started, unexpired suspended access and never
extends dates. The legacy status route delegates to domain commands instead of
EF CurrentValues. Existing Admin/manual-override restrictions remain. Initial
activation/resume retain their dedicated routes. Pending cancellation is explicit,
not a generic status edit. No refund/payment reversal rules were introduced.

## Cancel pending access API

### Endpoint
`/subscriptions/{id}/cancel-pending`

### Method
`POST`

### Params
Subscription ULID id; authenticated Admin or OnBoarding session.

### Request body
None.

### Response
200, including an idempotent repeat of pending cancellation:
```json
{
  "id": "01ARZ3NDEKTSV4RRFFQ69G5FAV",
  "status": "Canceled",
  "durationDays": 30,
  "startAt": null,
  "endAt": null
}
```

### Backend logic
Lock the subscription before reading state. Discord enqueue uses the same lock,
preventing new grants based on a pre-cancellation read. Accept AwaitingAccess or
an idempotent Canceled record whose access never started. Lock queued outbox rows
with the dispatcher's FOR UPDATE lock. Unpublished rows get CanceledAtUtc, and
their grants become Canceled. The dispatcher, due index and retry command exclude
canceled messages. If dispatch already owns the row, wait and observe the
confirmed publication state. Published requests remain Queued pending a result.
Cancellation, suppression and authenticated audit commit atomically.

A crash can occur after broker acceptance but before DB commit. Therefore a
valid late success for a suppressed Canceled request also becomes NeedsReview,
just like a late success for a published request. Neither activates canceled
access. No physical Discord role removal is claimed. Payments/orders are unchanged.

### Validation and errors
400 malformed ID; 401 invalid session; 403 role; 404 subscription_not_found;
409 cancel_pending_conflict for started/ineligible access; 409 concurrent_change;
500 internal_error. Repeating successful cancellation adds no audit.

### ToolJet integration
Show a confirmable cancel-waiting-access action before access starts. Refresh
subscription/request state afterward. Request status Canceled and deliveryStatus
canceled stop polling/retry controls. NeedsReview requires manual reconciliation,
not a success/refund/confirmed role-removal notification.

## Bounded card API

### Endpoint
`/clients/{id}`

### Method
`GET`

### Params
Existing client ULID and manage_clients permission.

### Request body
None.

### Response
Existing profile keys and array item shapes remain. products, discordAccesses,
cases and productTimeline are previews capped at 20. Purchase/subscription/learning
inputs are each bounded in SQL, not after full materialization. Timeline is a
recent preview, not a complete global feed. Product package/learning context is
also a preview: use purchase detail for authoritative per-purchase context.

Summary counters and displayed product counters/access flags/dates use full SQL
history. Tags use up to 100 distinct catalog products, with explicit truncation.
An additive field describes full-history navigation:
```json
{
  "history": {
    "previewLimit": 20,
    "purchasesTotal": 35,
    "subscriptionsTotal": 35,
    "discordAccessesTotal": 35,
    "platformAccessesTotal": 35,
    "casesTotal": 35,
    "purchasesUrl": "/clients/01ARZ3NDEKTSV4RRFFQ69G5FAV/purchases",
    "subscriptionsUrl": "/clients/01ARZ3NDEKTSV4RRFFQ69G5FAV/subscriptions",
    "discordAccessesUrl": "/clients/01ARZ3NDEKTSV4RRFFQ69G5FAV/discord-accesses",
    "platformAccessesUrl": "/clients/01ARZ3NDEKTSV4RRFFQ69G5FAV/platform-accesses",
    "casesUrl": "/clients/01ARZ3NDEKTSV4RRFFQ69G5FAV/cases",
    "productTagsTruncated": false
  }
}
```

### Backend logic
Separate bounded previews from full aggregates. No per-row database round trips.
Case amount permissions are enforced in SQL projection. Profile financials remain
on the existing separate permission-controlled endpoint.

### Validation and errors
Existing 400/401/403/404/500 behavior. Empty clients get empty arrays, zero counts
and null latest-case date.

### ToolJet integration
Preview bindings remain structurally compatible, but full-history consumers must
migrate: array.length is not total. Bind complete tables to the pages below.
Deploy the ToolJet binding update together with this intentional semantic change;
no deployed UI update or compatibility verification was executed.

## Complete history API

### Endpoint
- Existing /clients/{id}/purchases and /clients/{id}/purchases/{orderItemId}
- New /clients/{id}/subscriptions
- New /clients/{id}/discord-accesses
- New /clients/{id}/platform-accesses
- New /clients/{id}/cases

### Method
`GET`

### Params
New routes: page integer 1..1000000 default 1; pageSize integer 1..100 default 20;
sortBy whitelist date|id default date; sortDir asc|desc default desc; productId
optional positive integer for subscriptions/accesses only; status optional string;
search optional string up to 120 characters. Search matches product names for
subscriptions/accesses and comments for cases. Cases reject productId.

Subscription/access statuses: Active, Canceled, Suspended, Expired, AwaitingAccess.
Case statuses: New, Reviewed, Approved, Rejected, Published.
All require manage_clients; case amounts also require view_financials.
Purchases retain their existing purchaseDate|productName sorting/filter contract.

### Request body
None.

### Response
```json
{ "items": [], "total": 0, "page": 1, "pageSize": 20 }
```
Subscription items retain purchase-detail subscription fields and add orderItemId,
productId/productName and packageId/packageName for the client-wide table.
Discord/case items use existing card item shapes. Platform items contain id,
subscriptionId, productId, productName, subscriptionStatus, platformName,
platformUserId and grantedAt. Unauthorized case amount reads return null.

### Backend logic
Scope by client before filtering/counting. Count full filtered results, then
page in SQL. Date means StartAt, GrantedAt or CreatedAt according to section.
Date sorting includes the unique ID in the same direction; null dates follow
PostgreSQL defaults. ID-only sorting is available. Offset pages are stable for
unchanged data, not a snapshot guarantee during concurrent inserts/deletes.

### Validation and errors
400 IDs/paging/sort/status/unsupported filters; 401/403; 404 missing client;
500 generic failure. Existing clients without history return an empty page.

### ToolJet integration
Bind items and total separately, send page/pageSize, reset page on filter changes.
Load only the selected tab, debounce search and cancel stale client requests.

### Performance limits
Materialized rows are bounded; exact counts still scan matching rows. Deep
offsets and substring ILIKE can be expensive. Existing FK indexes support
ownership lookups. Evaluate real query plans before adding composite/trigram
indexes or changing pagination. Card summaries/previews are separate reads, not
one atomic snapshot. No SQL plan verification was executed.

## Migration and regression sources

20261023150000_CloseReviewRaces adds security versions, enrollment Version and
outbox CanceledAtUtc; recreates the due index; updates snapshot and target model.
Existing versions are initialized to 0/empty GUID; application updates rotate them.
Down refuses to revive canceled deliveries and revokes version-mismatched sessions.
Migration source was authored manually, not applied or generated by EF tooling.

- SubscriptionTransitionRegressionTests: terminal transitions, repeated commands, unchanged resume dates
- ReviewConflictHttpTests: wrapped provider serialization/deadlock failures still map to 409
- SecurityRaceRegressionTests: both HTTP password-change routes versus old login, JWT/refresh rejection
- LearningRaceRegressionTests: independent-context tokens, HTTP revoke/freeze/transfer, both package-assignment commit orders and audit rollback
- SubscriptionWorkflowRegressionTests: HTTP cancellation, dispatch suppression, published/ambiguous late results and legacy status chain
- OutboxCancellationRaceTests: independent PostgreSQL transactions sharing the row lock, with a fake network publisher
- ClientHistoryRegressionTests: 35-record fixtures, caps/full counts/pages, deterministic ties, query-count budget, ownership and financial isolation
- Existing migration test additionally checks HasPendingModelChanges

## Verification report

Performed: source inspection of routes/DI, session creation/invalidation, status
write paths, lock parameters, transaction ownership and changed model metadata;
static whitespace checks. These do not establish compilation or concurrency correctness.

Not run: restore/build, any tests, runtime EF model comparison, PostgreSQL
SQL/concurrency/rollback scenarios, migrations, ToolJet requests, Docker,
RabbitMQ, bots or external integrations. All regression scenarios remain
unexecuted until a separately authorized verification pass.
