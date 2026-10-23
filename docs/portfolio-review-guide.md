# Reviewing FFA

FFA models client operations for an education business: purchases, access,
student assignment, internal requests and achievements. It is an ASP.NET Core /
EF Core / PostgreSQL modular monolith with a ToolJet consumer.

## Suggested reading order

Start with project-review-checklist.md for the current source-only baseline.
Resource controllers now delegate to Features/<module>/Management; api-conventions.md,
domain-map.md and role-matrix.md describe their boundaries. ModuleArchitectureTests,
ApiInputRulesTests and ManagementScenarioTests are additional unexecuted examples.

For the client-focused refactor, first read client-module-architecture.md and
showcase/client-lifecycle.md, then follow ClientsController to its directory,
workspace and command services. ClientWriteServiceTests supplies source examples
for contact replacement, audit attribution and financial permission failures.
These latest changes were not compiled or tested as part of source-only review.

For inbound integration design, read sales-ingress-api.md and follow
SalesOrdersController -> SalesOrderImportService -> identity/catalog resolvers.
SalesContractTests and SalesIngressTests describe replay, conflict and rollback
behavior in source; they were not executed for this addition.

For persistence mapping, read [EF relationship corrections](ef-relationships.md)
and RelationshipModelTests. These show explicit inverse navigations, deletion
policies and a guarded schema cleanup migration. This addition was source-reviewed,
not compiled, executed or applied to a database.

1. `business-rules.md`: confirmed requirements and remaining decisions.
2. `workspace-api-map.md`: task boundaries, implemented slices and deferred work.
3. `../src/backend/Domain/LearningStructure/Enrollment.cs`: purchase-compatible
   assignment, transfer and lifecycle invariants.
4. `../src/backend/Features/Learning/EnrollmentService.cs`: scenario orchestration,
   bounded read queries and atomic audit writes.
5. `../src/backend/Controllers/Learning/LearningWorkspaceController.cs`: HTTP adaptation
   and role enforcement.
6. `../tests/Backend.Tests/EnrollmentTests.cs`: behavioral examples and rejection
   cases, including transferring a frozen student and preventing reactivation.
7. `../src/backend/Features/Clients/Contacts/ClientContactsService.cs` and
   `../tests/Backend.Tests/ClientContactsTests.cs`: contact form replacement,
   preservation of neighboring data and failure without partial domain mutation.
8. `../src/backend/Features/Clients/Purchases/ClientPurchaseQueries.cs` and
   `../tests/Backend.Tests/ClientPurchaseTests.cs`: purchase identity, client
   scoping and independently paginated subscriptions/enrollments.

9. `../src/backend/Features/Subscriptions/OrderSubscriptionProvisioner.cs` and
   `../tests/Backend.Tests/SubscriptionLifecycleTests.cs`: payment-triggered
   provisioning, duration snapshots and idempotent grant-time activation.

10. `../src/backend/Features/Applications/ApplicationWorkService.cs` and
    `../tests/Backend.Tests/ApplicationWorkTests.cs`: reply/status atomicity,
    actor attribution and client changes linked to a team request.

## Design choices

For asynchronous integration, follow [bot messaging](bot-messaging.md): HTTP
command -> transactional outbox -> confirmed RabbitMQ publication -> result
inbox -> actual-access activation. Read BotMessagingTests for unexecuted replay,
failure and reconciliation scenarios. This is an FFA-side source implementation;
no broker, external worker or deployment was verified for this addition.

Security entry points: Auth/BackendSecurity.cs, Controllers/Clients/ClientFinancialsController.cs,
Features/Clients/Financials and SecurityHttpTests. See backend-security.md for
permissions, session revocation, HTTP coverage and deployment limitations.

- Organize application code by business task, not by individual editable fields.
- Use symmetric small blocks for forms; use commands for workflow transitions.
- Keep EF in concrete application services for now. No generic repository,
  mediator or separate database per module is needed for this project size.
- Retain domain rules independently of HTTP and persistence.
- Use the same command service from new and legacy routes while migrating.
- Save the command result and its audit event in the same database transaction.
- Keep purchase, subscription and learning dates semantically separate.

## Verification

```sh
dotnet build src/backend/backend.csproj
dotnet test tests/Backend.Tests/Backend.Tests.csproj
```

The fast test suite is designed to run without an external database. Purchase query tests use EF InMemory,
which does not prove PostgreSQL SQL translation. Backend requests require PostgreSQL and configured
JWT secrets. The public snapshot includes a manual-only GitHub workflow for the
fast suite; no execution is triggered by publishing the snapshot.

Route-compatible resource adapters remain alongside task-oriented workspace APIs.
Financial read/write policies, private client reads and revocable JWT sessions
are implemented; see backend-security.md and SecurityHttpTests.
External provider calls, production operation and PostgreSQL verification remain separate work.
Automatic subscription rules are described in subscription-lifecycle.md.
The repository must not be described as production-ready or fully
migrated until those gaps and integration coverage are addressed.
