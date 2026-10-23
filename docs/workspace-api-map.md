# Task-oriented workspace API

This is the navigation and API plan for the portfolio refactor. Screens are
abstract consumers, not a requirement to build a new frontend. "Target" routes
are a design proposal, not implemented endpoints. Existing controllers remain
available as thin adapters delegating to module management services. See
project-review-checklist.md and api-conventions.md for the current source-only baseline.

## Boundaries

| Workspace | User task | API boundary | State |
| --- | --- | --- | --- |
| Client profile | Read/save personal information | GET/PUT /clients/{id}/profile | Implemented feature |
| Client contacts | Read/save phone and social contacts | GET/PUT /clients/{id}/contacts; existing separate email actions | Contact block implemented; email migration pending |
| Client purchases | Browse purchases and inspect one purchase | GET /clients/{id}/purchases and /clients/{id}/purchases/{orderItemId} | Implemented feature |
| Client finance | View/edit financial summary | GET/PUT /clients/{id}/financials with separate read/write permissions | Implemented; see backend-security.md |
| Learning assignment | Find pending students and assign a group | GET /learning/assignment-queue; POST /learning/enrollments/{id}/assign-group | Implemented feature |
| Learning directory | Browse all or active cohorts/groups | GET /learning/cohorts and /learning/groups | Implemented feature |
| Student learning | Inspect, transfer, freeze, complete or revoke learning | GET /learning/enrollments/{id}; assignment and status commands | Implemented feature |
| Cohort administration | Create/edit a cohort or group | Existing /learning-structure routes | Feature extraction implemented in source |
| Team requests | Read, reply, resolve and update linked client profile | /applications/{id}/processing, /replies, /status, /client-profile, /activity | Conversation and processing implemented; other task actions pending |
| Student achievements | Show checklist and mark achievement | GET /learning/enrollments/{id}/roadmap; PUT completion action | Implemented; see student-roadmap-api.md |
| Student results | Review a reported trading result | Existing /cases | Preserve current scope |
| Catalog | Maintain products and package durations | Existing /products | CatalogService extraction implemented in source |
| Access confirmation | Confirm actual grant and start duration | POST /subscriptions/{id}/activate-access | Implemented; automatic Paid processing linked to order writes |
| Team administration | Manage users and roles | Existing /users | TeamAdministrationService extraction implemented in source |
| Session | Login, refresh, logout, current user | Existing /auth | Revocable sessions and live permissions implemented; see backend-security.md |
| Sales integration | Import client, purchase and confirmed payments | POST /integrations/sales/orders; Person ID / Deal ID / external payment ID | Source implementation; not executed or deployed; see sales-ingress-api.md |

Purchase detail must be keyed by order item. Product ID alone cannot identify
the correct package, enrollment or entitlement after repeat purchases.

## Learning workspace contract

All new /learning routes require Admin or OnBoarding. Unauthorized callers get
401, other roles get 403. These role checks also apply to the two legacy
legacy cohort/group/enrollment mutation actions. Existing module policies still
control legacy reads; roadmap checklist routes use ManageRoadMaps separately.

Directory parameters: page (1..1000000), pageSize (1..100), activeOnly (boolean),
search (up to 120 characters), productId (positive integer).
Active groups are groups belonging to an Active cohort; groups have no separate
active state in the current model. Counts include all linked enrollments.
Directory ordering is fixed: cohorts by start date descending then ID, groups by
name then ID. Sorting is intentionally not supplied as an arbitrary SQL field.

The assignment queue accepts page, pageSize, search (client name), productId.
It lists existing Pending enrollments without a group, oldest first with an ID
tiebreaker. It does not yet discover paid purchases without an enrollment.
Legacy enrollments without order items remain visible, but must be repaired
before assignment. No amount or payment details are exposed in this workspace.

All lists return:

```json
{ "items": [], "total": 0, "page": 1, "pageSize": 20 }
```

Assignment body:

```json
{ "groupId": "01K00000000000000000000000" }
```

Status body uses existing domain names (case insensitive):

```json
{ "status": "Frozen" }
```

Supported domain values: Pending, Active, Frozen, Graduated, AccessRevoked.
Dropped and Closed are retained for legacy records. Unknown values return 400.
Graduated means completed in the UI. AccessRevoked is the learning state only;
this change does not call Discord or a learning platform to remove permissions.

Assignment and status return the same EnrollmentView as GET enrollment. It
contains client/purchase identity, product/package, cohort/group, status and
learning dates. Learning dates always come from the cohort.

Missing enrollment/group returns 404. Incompatible product/package, archived or
completed target cohort, or an invalid state transition returns 409. No partial
assignment is applied when validation fails. Repeating an unchanged command is a
no-op and does not produce another audit event. Changes and audit are committed
together. Simultaneous competing edits currently use last-write-wins semantics;
optimistic concurrency is future work.

## Proposed transition policy

The owner confirmed the states; these transition restrictions are implementation
defaults, documented here for review:

- Pending -> Active (requires assignment), Closed or AccessRevoked.
- Active -> Frozen, Graduated, Dropped, Closed or AccessRevoked.
- Frozen -> Active, Dropped, Closed or AccessRevoked.
- Graduated, Dropped, Closed and AccessRevoked are terminal.
- Assigning the first group activates a Pending enrollment. Transfers preserve
  Active or Frozen state. Repeating the current state is allowed.

## Review and verification

Student roadmap checklist is implemented separately in StudentRoadMapController
and Features/RoadMaps. See [student-roadmap-api.md](student-roadmap-api.md) for
paged enrollment-scoped reads and the completion toggle with atomic audit.

Read Enrollment domain rules, EnrollmentService, LearningWorkspaceController and
EnrollmentTests in that order. The service materializes enums/ULIDs before JSON
conversion and never loads an unbounded collection of student entities.

Current tests cover domain rules and profile validation. PostgreSQL execution,
HTTP authentication, concurrent requests and external platform integration are
not covered by these unit tests. The existing migration linking enrollment to
order item is a prerequisite; it is not applied by this refactor.
