# Resource API conventions

## Endpoint

Existing routes remain `/clients`, `/products`, `/orders`, `/subscriptions`,
`/applications`, `/cases`, `/learning-structure`, `/roadmaps` and `/users`.
Use task endpoints for workspace forms and commands; see workspace-api-map.md.

## Method

GET reads. POST creates or performs a named action. PUT replaces a form block.
PATCH updates supplied fields. Resource writes are not generic SQL update APIs.

## Params

Resource lists use server-side `page` (1..1000000, default 1) and `pageSize`
(1..100, default 20). Invalid values return 400 instead of being silently clamped.
Search is optional, trimmed and capped at 120 characters. Omitted/blank optional
text filters mean no filter. Supplied invalid ULIDs, enums or integer IDs return
400, rather than expanding the query by silently ignoring a filter.

| Resource | Allowed filters, excluding paging | Sort whitelist |
| --- | --- | --- |
| clients | search:string, email:string(150), telegram:string(100), hasEmail:bool, hasTelegram:bool, createdFrom/createdTo:date | name, createdAtUtc |
| products | search:string, type:string, isActive:bool, isArchived:bool | name, code, createdAt |
| orders | clientId:ULID, status:OrderStatus, source:OrderSource, productId:int, createdFrom/createdTo:date | createdAt, status |
| subscriptions | clientId:ULID, productId:int, status:SubscriptionStatus, type:SubscriptionType, activeOn:UTC timestamp | startAt, endAt, status |
| applications | search:string, status:ApplicationStatus, reason:ApplicationReason, typeId/authorId/clientId:ULID | createdAt, status, reason |
| cases | clientId/createdById:ULID, status:CaseStatus, type:CaseType, isFeatured/hasConsent:bool, createdFrom/createdTo:UTC timestamp | createdAt, amount, status |
| users | search:string, role:UserRole, isActive:bool | fullName, email, role, createdAt |
| learning-structure/cohorts | search:string, productId:int, status:CohortStatus | fixed startAt descending, name, ID |
| learning-structure/groups | search:string, cohortId/mentorId:ULID, packageId:int | fixed name, ID |
| learning-structure/enrollments | orderId/cohortId/groupId:ULID, status:EnrollmentStatus | fixed enrolledAt descending, ID |
| roadmaps/templates | search:string, productId:int, isActive:bool | fixed name, ID |
| roadmaps/assignments | enrollmentId/userId:ULID, status:AssignmentStatus | fixed assignedAt descending, ID |

Sort direction is asc/desc, case insensitive; unknown sortable fields return 400.
Enums are the named domain values, case insensitive, not arbitrary integers.
Positive integer IDs and chronological date ranges are checked explicitly.
Case amount sorting additionally requires ViewFinancials. Select endpoints use
their existing `{items}` envelope, default limit 50 and permitted limit 1..100;
application types and role vocabularies retain their dedicated select contracts.

## Request Body

Feature Management write DTOs reject unknown JSON fields. Record validation
attributes are on constructor parameters, not duplicate property attributes.
Optional patch null means unchanged in existing resource contracts; it is not a
universal "clear field" command. Block PUT endpoints have their own replacement
semantics. Never send unrelated fields from a full client card to a small form.

```json
{ "name": "FFA Core", "isActive": true }
```

The example is PATCH /products/{id}; code/name/type cannot be blank. Field lengths
and package durations are bounded. UTC timestamp fields require a Z suffix.
Dates such as purchaseDate remain dates, not timestamps. Access and learning
dates must never be substituted for one another in ToolJet.

Manual subscription creation and manual term overrides are restricted to Admin.
Use POST /subscriptions/{id}/resume-access (Admin/OnBoarding, no body) to restore
unexpired suspended access without changing its dates. See subscription-lifecycle.md.
Roadmap assignment validates that the template product matches the enrollment's
purchased item; incompatible products or an unlinked legacy enrollment return 409.

## Response

Resource list envelope is unchanged:

```json
{ "items": [], "total": 0, "page": 1, "pageSize": 20 }
```

Detail/write success keeps the resource's existing DTO. Creates with an existing
detail route retain 201 and Location; other existing actions retain 200. DTOs
never serialize an EF entity graph. Order totalAmount is the item purchase total;
payments remain separate records, including their actual payment statuses.

## Backend Logic

HTTP adapters delegate to management services; task workspaces use their own
feature services. ModuleActor comes from the authenticated request, never from
an editable actor object in the body. Query projections, domain checks and audit
writes are separated from HTTP result mapping. The order detail reader explicitly
materializes relations before converting domain statuses into JSON strings.

## Validation And Errors

400 uses ValidationProblemDetails with `errors[field]` arrays. A field not present
in a write DTO is a 400, not a successful ignored update.

```json
{ "code": "product_code_conflict", "message": "Product code already exists." }
```

Other failures use the shared code/message structure where a response body is
supplied. 401 means missing/invalid authentication, 403 means missing permission,
404 means an authorized resource lookup failed, 409 means a business, unique/FK
constraint or concurrency conflict. Unexpected exceptions remain 500 with a trace
ID, not a leaked SQL statement or stack trace. Human JWT authorization responses
use the same camelCase code/message keys as MVC and middleware failures.
Consumers must still check HTTP status first.

## ToolJet Integration

Bind tables to `query.data.items` and total rows to `query.data.total`. Use server
paging/sorting events to rerun the query; reset page to 1 after a filter change.
Bind select values to stable IDs, not display names. On a master-detail selection,
load the resource-scoped detail query. Submit only the form's write DTO. After
success refresh detail and the relevant list; display field errors beside inputs.
On 409 reload before retrying; on 401 reauthenticate. Hiding an action button is
only UX: backend policies remain the security boundary.

## Performance And Safety

All table queries are paged with stable ordering and total counts. Offset pages
are not a snapshot across concurrent changes. Search with `%term%`/ILIKE can scan
large tables; inspect query plans before adding PostgreSQL trigram indexes. No
trigram extension or index was installed here. Batch lookups avoid per-row catalog
queries. Order detail uses a fixed split-query include plan, not lazy loading or
an N+1 loop; large histories still belong in independently paged workspace APIs.
No SQL execution or PostgreSQL performance claim is established by source review.
