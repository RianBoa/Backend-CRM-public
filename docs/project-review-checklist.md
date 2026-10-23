# Source review baseline

See [race and bounded-workspace corrections](review-race-fixes.md) for the latest
security-version, enrollment consistency, subscription cancellation and history
pagination changes, migration and unexecuted PostgreSQL regression scenarios.

## Goal and limits

FFA is a portfolio backend for education operations, not a claim of production
readiness. The review goal is a coherent story, inspectable boundaries, concrete
business rules and honest verification status. No frontend was implemented by
this refactor; ToolJet is the intended HTTP consumer.

## 1. Architecture

The eight resource adapters for Products, Orders, Cases, Users, Applications,
LearningStructure, RoadMaps and Subscriptions delegate their 62 HTTP actions to
Features/<module>/Management. Their original route templates and authorization
attributes are retained. JSON field shapes are preserved; public C# DTOs moved
from nested controller types into feature namespaces.

Management services have separate query, command and support/validation files.
Learning writes are grouped by cohorts, groups and enrollments; roadmap writes
by templates and assignments. This is a concrete module service, not a generic
repository or mediator. Task-specific workspace services remain the preferred
entry points for profile forms, learning assignment and achievement completion.

Controllers bind requests, enforce route permissions and map outcomes to HTTP.
Features receive an explicit ModuleActor snapshot, not HttpContext or ControllerBase.
The common ModuleOutcome carries data or a named application failure without
HTTP status codes. Domain factories/methods enforce business invariants; some
legacy patch mapping still uses EF CurrentValues after validation. Domain-layer
immutability and a completely separate persistence project are not claimed.

Auth handles tokens/sessions and Health handles infrastructure directly. They
are explicit exceptions to the resource-adapter pattern, not unfinished CRUD modules.

## 2. API and security

Read api-conventions.md for limits and examples; role-matrix.md for permissions.
Invalid resource-list filters are rejected before database queries. Requests have
bounded fields, UTC timestamp validation and explicit unknown-field rejection.
Existing workspace contracts have their own validation; they are not all replaced
with one shared result type in this pass.

Resource mutations save audit and business state together. For database-generated
integer IDs, ModuleAudit uses two saves inside one relational transaction, resolving
the ID before constructing audit metadata. ULID/existing-ID changes stage both in
one save. InMemory examples do not prove rollback or concurrent-write behavior.
The old optional audit service-locator hook was removed, so missing DI registration
can no longer silently skip resource mutation audit.

Confirmed payment terms have domain-level precision, currency, installment and
date validation. Order list totalAmount means purchase item total, not the sum of
pending/cancelled payments. Financial fields remain behind financial permissions.
The sales ingress is the idempotent machine import boundary; manual payment creation
does not acquire an external event-id contract through this refactor.

## 3. Test examples

| Source | Review purpose |
| --- | --- |
| ModuleArchitectureTests | Thin resource adapters; feature outcomes and explicit actors |
| ApiInputRulesTests | Paging, sort whitelist, enum/ID rejection, unknown JSON, UTC and payment terms |
| ManagementScenarioTests | Generated audit identity, duplicate catalog code, early validation and package/course conflict |
| RelationshipModelTests | Explicit inverse navigations and enrollment-preserving group deletion |
| SecurityHttpTests | JWT sessions, cumulative permissions and record-parameter validation through HTTP |
| SubscriptionLifecycleTests | First successful payment, repeated provisioning and actual access grant |
| EnrollmentTests / StudentRoadMapTests | Assignment compatibility, transfers and achievement transitions |
| SalesContractTests / SalesIngressTests | Fingerprints, replay, identity conflict and rollback scenarios |

All newly added/changed examples in this source-only pass are **unexecuted**.
The integration project starts infrastructure if invoked; it was not invoked.

The subsequent five-item review correction adds Admin-only manual subscription
overrides, an expiry-preserving resume command, roadmap/product compatibility,
camelCase auth/internal errors and a safe inclusive maximum client date filter.
See ReviewRegressionTests, SubscriptionLifecycleTests, StudentRoadMapTests and
SecurityHttpTests for added source examples. These were not executed either.

## 4. Reading story

Read domain-map.md, role-matrix.md, showcase/client-lifecycle.md and
sales-ingress-api.md. Follow HTTP adapter -> feature service -> domain method ->
EF configuration -> matching test source. Examples use illustrative IDs and
reserved domains, not live client credentials.

## Verification still required before any real release

- Compile the changed source and execute the fast suite.
- Check SQL translation and real constraints/transactions on PostgreSQL.
- Generate/compare EF metadata with the actual model and review migration SQL.
- Apply migrations only after a backup, data reconciliation and deployment review.
- Exercise deployed authentication, ToolJet binding and external ingress end-to-end.
- Decide production retries, manual-command idempotency, backup/restore, monitoring,
  secrets rotation and real Discord/platform adapters.

The source has received static comparisons and whitespace checks only. No build,
test run, database operation, deployment, commit or push was performed for this pass.
