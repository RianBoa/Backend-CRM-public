# Student roadmap checklist

Assignment creation now requires the roadmap template and enrollment purchase to
refer to the same product. POST /roadmaps/assignments returns 409
roadmap_product_mismatch for a different course, or enrollment_purchase_required
when a legacy enrollment has no purchased item. Validation happens before writes
and is also enforced by the domain factory. Existing historical assignments are
not rewritten by this source change. Added regression tests were not executed.

Existing roadmap assignments are reused, scoped to one enrollment. No new tables or automatic assignments are introduced. Existing template and assignment administration endpoints remain available.

## Endpoint

`GET /learning/enrollments/{enrollmentId}/roadmap`

## Params

`enrollmentId`: ULID. Server-side `page` (1..1000000, default 1), `pageSize` (1..100, default 20).
Filters: `templateId` positive integer, `status` Pending|Completed|Overdue|Skipped, `search` title substring up to 120 characters (case-sensitive).
Sorting whitelist: `sortBy=dueAt|title`, `sortDir=asc|desc`; defaults dueAt ascending. Assignment ID breaks ties.

## Response

```json
{
  "items": [{
    "assignmentId": "01K00000000000000000000001",
    "itemId": 1,
    "templateId": 1,
    "title": "First case",
    "type": "Case",
    "status": "Pending",
    "completed": false,
    "dueAt": "2026-10-30T00:00:00Z"
  }],
  "total": 1,
  "page": 1,
  "pageSize": 20
}
```

## Completion action

`PUT /learning/enrollments/{enrollmentId}/roadmap/{assignmentId}/completion`

Body: `{ "completed": true }` or `{ "completed": false }`.
Returns the updated item, not the entire checklist. Missing or null completed is invalid; false is accepted.

## Backend logic

The HTTP controller validates input and delegates to the feature service. Domain code handles completion and reopening. Unchecking returns Pending or Overdue according to the existing deadline rule: enrollment time plus assigned offset, not subscription start or cohort dates. Skipped tasks must first be restored through existing assignment management.

An actual change and its audit event are committed in one SaveChanges call. The audit records JWT actor, enrollment, previous and new status. Repeating the same completion state is a no-op. Status is an EF concurrency token; conflicting writes return 409. No schema change is required for this concurrency metadata.

## Validation And Errors

ManageRoadMaps policy, authenticated JWT required; writes additionally require an active actor in the database. Invalid IDs, paging, filters, sorting or body: 400. No token: 401. No permission/inactive actor: 403. Missing enrollment or assignment in that enrollment: 404. Skipped assignment or concurrent status update: 409. Unexpected failures use the existing global error handler, not a success fallback.

## ToolJet Integration

Load the checklist after selecting an enrollment. Bind table data to `data.items`, total count to `data.total`, and use server-side paging. Checkbox submits only completed, using the row assignmentId and selected enrollmentId. After success refresh the page; on 409 refresh before retrying. Empty existing enrollment returns an empty paged list.

## Limits

No automatic case verification, background overdue processing or template bulk assignment. Status filters use stored status; deadlines alone do not silently change Pending to Overdue. Completion time/author are recorded in audit, not exposed as invented assignment columns. Existing EnrollmentId index bounds the query; substring search and derived deadline sorting are not index-backed. PostgreSQL query translation and live HTTP behavior still require integration verification; InMemory tests do not establish those guarantees.
