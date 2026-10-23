# Subscription lifecycle

The [review corrections](review-race-fixes.md) add domain-enforced terminal
transitions and POST /subscriptions/{id}/cancel-pending with outbox coordination.
Pending cancellation is not a refund or a confirmed external access revocation.

## Creation after payment

POST /orders and POST /orders/{id}/payments provision eligible subscriptions when
the order has a Paid payment. Pending payments do not provision anything. The
order/payment, subscriptions and their audit entries commit together.

Explicit catalog conventions for this first implementation:

- Product.Type Course (case insensitive) with positive Package.DurationDays
  provisions CoursePlatform.
- Product.DiscordAccess or Package.DiscordAccess with positive DiscordDays
  provisions DiscordCourseBonus for courses, DiscordStandalone otherwise.
- Missing durations do not invent a subscription; PackageAddon is not automated.

These are implementation defaults to review against the catalog. New records
have AwaitingAccess status and null startAt/endAt. Duration is copied from the
package; subsequent package edits do not change the purchased duration.

Purchase-item/type identity prevents repeat provisioning. A nullable unique
provisioning key protects automatic records against concurrent duplication.
Conflict returns 409 and rolls back that order/payment write: reload and retry.
Existing legacy subscriptions of the same purchase/type are also skipped.
This does not deduplicate payments themselves or implement webhook idempotency.
Deploying the change does not backfill all historical payments.

## Confirm actual access

POST /subscriptions/{id}/activate-access requires Admin or OnBoarding.
It records the manager's confirmation of actual issuance. It does not issue
permissions on Discord or a platform itself.

```json
{ "grantedAt": "2026-10-23T07:00:00Z" }
```

Successful response:

```json
{
  "id": "01K00000000000000000000001",
  "status": "Active",
  "durationDays": 30,
  "startAt": "2026-10-23T07:00:00Z",
  "endAt": "2026-11-22T07:00:00Z"
}
```

grantedAt is required, cannot be in the future and is normalized to UTC.
Repeating activation of an active subscription returns existing dates without
resetting duration or duplicating audit. Canceled/suspended/expired subscriptions
cannot be activated here. Concurrent state changes return 409, missing records
404, invalid requests 400, missing identity/role 401/403.

Legacy date PATCH/status actions cannot start waiting subscriptions or overwrite
automatic subscription dates. Legacy nonautomatic records retain existing edit
behavior only within the manual override restrictions below. Discord/platform
access records remain separate from this confirmation.
Confirm each subscription independently: Discord issuance does not start platform
duration. This command does not change learning dates or enrollment status.

## Manual override boundary

POST /subscriptions is an Admin-only legacy/manual override, not the normal
payment or access-grant workflow. It retains its existing request/response DTO.
Both HTTP authorization and the service's trusted ModuleActor snapshot enforce
this restriction. Curator/TroubleShooter module permissions are insufficient.
Manual date PATCH and direct legacy activation also require Admin. Automatic
dates cannot be overridden even by Admin through PATCH.

Normal creation still follows first Paid payment -> eligible AwaitingAccess
subscription -> actual grant confirmation. No new override flag is accepted in
HTTP JSON. Resume of suspended access uses the command below, not status PATCH.

## Resume suspended access

Endpoint: `/subscriptions/{id}/resume-access`.
Method: POST. Params: id is a subscription ULID. Request body: none.
Authorization: Admin or OnBoarding, as for initial grant confirmation.

Response: the same SubscriptionAccessView shape as activate-access:

```json
{
  "id": "01K00000000000000000000001",
  "status": "Active",
  "durationDays": 30,
  "startAt": "2026-10-01T00:00:00Z",
  "endAt": "2026-10-31T00:00:00Z"
}
```

Backend logic: use the server clock, accept Suspended with a started, unexpired
term, and set Active without changing startAt, endAt or durationDays. Suspension
does not pause billing or extend the purchased term. An already Active, unexpired
subscription is an idempotent no-op. Save a resume_access audit only on change,
atomically with the status; concurrent status edits return 409.

Validation/errors: invalid ULID 400, missing identity 401, wrong role 403, missing
subscription 404. Waiting/canceled/expired access, missing dates, a future start
or an elapsed term returns resume_conflict (409), with no state or audit change.
This command does not call Discord/platform providers or restore learning status.

ToolJet: show Restore for Suspended records with an unexpired term; POST with no
body and refresh purchase detail. On 409 show the returned message and reload.
The backend remains authoritative even if the UI shows a stale action button.

These fixes and added regression test sources were not compiled or executed.
No migration is needed for this change: status and dates already exist.

## ToolJet

Purchase detail includes subscription IDs and AwaitingAccess status. Show a
confirmation action, submit the actual grant timestamp, then reload the panel.
Display null dates as awaiting access. Existing JSON keys are retained, but
dates now support null. Stored legacy subscription dates are not reinterpreted
as verified provider grant events.

## Schema and tests

Migration AddSubscriptionAccessLifecycle makes dates nullable and adds duration
and provisioning identity. Existing dates are preserved. Status is an optimistic
concurrency token. Downgrade refuses to fabricate dates for pending subscriptions.
The migration is generated, not applied to any database.

Tests cover eligibility, pending payments, first paid payment, repeated processing,
grant-based duration and activation/audit idempotency. Query tests use EF InMemory;
PostgreSQL migration execution, constraint races and HTTP authorization are not
verified by them. External providers, refunds and renewals remain future work.
