# Client module: reading guide

This is a source-level refactor for portfolio review. No services, migrations,
compilation or tests were run for this change. Request routes and serialized
response fields are retained; verification still requires a separate run.

## Boundaries

```text
Controllers/Clients/ClientsController.cs
    -> Features/Clients/Directory/ClientDirectoryQueries
    -> Features/Clients/Workspace/ClientDetailQueries
    -> Features/Clients/Commands/ClientWriteService

ClientDetailQueries
    -> ClientProductQueries       purchase / subscription / enrollment data
    -> ClientActivityQueries      Discord / platform records / cases
    -> ClientWorkspaceProjection  response assembly, tags and timeline

ClientProductQueries
    -> ClientProductProjection    in-memory product aggregation and status rules

ClientWriteService
    -> ClientWriteValidation      legacy request validation
    -> ClientWriteMapping         normalization and block replacement
    -> Client domain methods      contact and email invariants
    -> SaveChanges                client changes plus authenticated audit
```

All public client DTOs are in Features/Clients/Contracts. Services no longer
depend on nested controller DTOs, ControllerBase, HttpContext or IActionResult.
Internal query snapshots are separate from public API contracts. The controller
maps validation, not-found, forbidden and conflict outcomes to HTTP responses.
Unexpected persistence errors remain internal errors, not fake domain conflicts.

Profile, contacts, financials and purchase-specific browsing retain their
existing focused feature services. The legacy full-card writes remain supported
for compatibility; ToolJet should prefer task blocks for new forms.

## Routes And Contracts

- GET /clients: existing paged list, filtering, sorting and product labels.
- GET /clients/select: existing lightweight select items.
- GET /clients/{id}: existing workspace shape, without the financial block.
- POST /clients: creates a client, returns the detail contract and 201.
- PUT /clients/{id}: replaces legacy editable blocks; omitted financials remain unchanged.
- PATCH /clients/{id}: updates supplied legacy blocks only.
- POST /clients/{id}/emails: adds an email; duplicate normalized addresses conflict.
- POST /clients/{id}/primary-email: selects an existing address as primary.

The old product-ID-only endpoints were already absent before this refactor and
are not silently restored. Products are available in detail; purchase-specific
master-detail uses /clients/{id}/purchases and its order-item detail route.
DTO CLR types moved out of ClientsController into a feature contract namespace;
external C# consumers referencing nested types would need a source update. JSON
property names and route attributes are retained.

## Security And Consistency

The controller derives ClientActor and the read visibility flag from the
authenticated principal. Commands check financial write permission before any
mutation. Queries receive financial visibility explicitly, without reaching
back into HttpContext. Unprivileged case amounts remain null; financials stay
behind their dedicated endpoint. Actor/CreatedBy never comes from the body.

Every legacy client command now stages an audit event in the same SaveChanges
call, including both email actions. Domain argument errors become validation
results; contact/business conflicts become 409. Database errors are not caught
as InvalidOperationException business failures. Command contexts are scoped to
one HTTP request; a failed in-memory mutation must not be followed by another
save using that context.

Email replacement establishes replacement contacts before removing old ones.
This also handles email-only creation and transitions between email-only and
phone-only contacts without an artificial intermediate contactless state.
These cases have source examples in ClientWriteServiceTests, not a claim of
execution against PostgreSQL.

## Query Tradeoffs

List queries remain paged with a batched product lookup for that page. Detail
retains its existing full workspace contract; nested collections are not paged
by this refactor. Separate purchase endpoints are preferable for large histories.
EF queries on one context are awaited sequentially, never run concurrently.
Enums are materialized before in-memory display conversion in the moved
product/access/case snapshots. Moving code does not establish SQL correctness.

Start reviewing the controller, follow a query or command, then inspect the
domain invariant and corresponding test. For the cross-module narrative, read
[the client lifecycle showcase](showcase/client-lifecycle.md).
