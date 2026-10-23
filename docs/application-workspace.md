# Internal request workspace

Applications are typed internal team requests. A conversation has its own reply
history; audit events describe actions and changes, not another copy of the reply
text. Existing list/detail/type routes remain available.

## Endpoints

| Method | Route | Task |
| --- | --- | --- |
| GET | /applications/{id}/processing | Current state, linked client, processor and reply count |
| GET | /applications/{id}/replies | Paginated conversation |
| POST | /applications/{id}/replies | Reply and optionally change state atomically |
| POST | /applications/{id}/status | Existing state action, now backed by the work service |
| PUT | /applications/{id}/client-profile | Save the linked client's profile from this request |
| GET | /applications/{id}/activity | Paginated audit summary for this request |

All require manage_applications. The client-profile action additionally requires
manage_clients. Mutation authors are taken from JWT and verified as active users,
not accepted from reply bodies. Existing create still accepts authorId for
compatibility, but it must match JWT. Inactive types and mismatched client/order
references are rejected; an order-only request derives clientId from that order.

## Reply

```json
{
  "body": "Student details checked and updated.",
  "status": "Closed"
}
```

body is required, trimmed and limited to 4000 characters. status is optional;
omitting it leaves the state unchanged. Successful POST returns 200:

```json
{
  "reply": {
    "id": "01K00000000000000000000001",
    "authorId": "01K00000000000000000000002",
    "authorName": "Onboarding Manager",
    "body": "Student details checked and updated.",
    "createdAt": "2026-10-23T10:00:00Z"
  },
  "application": {
    "id": "01K00000000000000000000003",
    "status": "Closed",
    "clientId": "01K00000000000000000000004",
    "processedById": "01K00000000000000000000002",
    "processedAt": "2026-10-23T10:00:00Z",
    "repliesCount": 1
  }
}
```

New, InProcess and RequiresClarification are open. Closed and Rejected are
terminal. Open requests may move between InProcess/RequiresClarification or
close/reject; returning to New is rejected. This is an implementation default,
not a configured workflow engine. Repeating the current status is a no-op.
Replies/profile processing of terminal requests return 409. Reply POST is not
idempotent: a successful retry can create another reply. UI should disable the
submit button while pending and reload before retrying an uncertain response.

## Pagination and failures

Replies/activity accept page (1..1000000, default 1) and pageSize (1..100, default
20), with the shared items/total/page/pageSize response. Replies are oldest first;
activity newest first. Both use ID tiebreakers. No arbitrary sort field is
accepted. Empty conversations return an empty page; missing applications 404.
Invalid IDs/body/status: 400. Missing auth: 401; missing permission or inactive
mutation actor: 403. Closed state or optimistic concurrency conflict: 409.

Activity items expose id, actorId, action and createdAtUtc only. Freeform audit
metadata is deliberately not exposed through this endpoint.

## Client changes and audit

PUT client-profile accepts the same small profile block as GET/PUT
/clients/{id}/profile; null/omitted optional fields clear them. It changes only
the client actually linked to the request. A terminal request cannot change the
profile, and a missing client/link returns 404.

The client save, application revision and two audit entries commit together.
The client audit carries applicationId, while the application audit carries
clientId. Editing the profile directly through the ordinary client endpoint does
not invent a request association. Group transfers, contact edits and external
access changes are not yet linked through this processing action.

## UI and storage

The abstract screen is a request header, reply thread, reply form, linked-client
profile form and an activity panel. After reply/status success reload processing
and the thread; after profile save reload the client card and activity. No UI
export is included.

Migration AddApplicationConversation creates application_replies and a revision
token; existing processed_by_id is reused through an explicit model property.
Replies are append-only through the API. Revision checking prevents competing
processing operations from partially committing a reply or audit event.
Migration execution, PostgreSQL constraints/concurrency and HTTP authorization
still need integration verification. Current tests use domain objects and EF
InMemory; they do not establish those guarantees on a live server.
