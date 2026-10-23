# Inbound sales integration: source-only showcase

The integration receives client, purchase and confirmed-payment data from one
trusted external sales connector. Pipedrive Person ID identifies the client;
Deal ID identifies the order. It does not fetch data from Pipedrive itself.
No services, compilation, tests or database migrations were run for this change.
The implementation, migration metadata and tests are source examples awaiting
separate execution/verification, not a deployed integration.

## Endpoint

`/integrations/sales/orders`

## Method

`POST`

## Params And Authentication

No query parameters. Header `X-Integration-Key` authenticates the connector.
Configure `SalesIntegration__ApiKey` externally, using a random secret with at
least 32 bytes and at most 512 characters; no secret is supplied in this repo.
An unset/invalid-length key leaves ingress disabled. A normal Admin JWT alone
cannot call this endpoint. Connector credentials do not authorize client lists,
user administration or other JWT-only endpoints. Use TLS if ever deployed.

The source/account scope is fixed to `sales` and client provider `pipedrive`.
This version assumes one Pipedrive organization, not multiple organizations
whose Person/Deal IDs might overlap. Adding another connector requires a trusted
source/account namespace, not a caller-controlled provider string.

## Request Body

```json
{
  "eventId": "sales-event-1001",
  "client": {
    "pipedrivePersonId": "12345",
    "email": "student@example.test",
    "name": "Demo Student"
  },
  "order": {
    "pipedriveDealId": "67890",
    "currency": "USD",
    "purchasedAt": "2026-06-01T12:00:00Z",
    "items": [
      { "productCode": "ffa-core", "packageCode": "mentorship", "price": 1000, "quantity": 1 }
    ]
  },
  "payments": [
    {
      "externalId": "provider-payment-001",
      "amount": 250,
      "currency": "USD",
      "status": "Paid",
      "paidAt": "2026-06-01T12:05:00Z",
      "provider": "external-sales",
      "invoice": "invoice-001",
      "totalInstallments": 4,
      "currentInstallment": 1
    }
  ]
}
```

Required: eventId; client Person ID/email/name; order Deal ID/currency/purchasedAt;
1..50 order items; a payments array with 0..50 entries. Empty payments creates an
unpaid order without provisioning subscriptions. Event/payment external IDs are
case-sensitive, trimmed strings up to 100 characters. Person/Deal IDs are strings
of 1..19 digits without leading zeros; no numeric JSON IDs or internal client IDs.

Product/package codes are case-sensitive, trimmed catalog keys up to 64 characters.
Pairs cannot repeat; use quantity (1..100000). Products must be active and not
archived for a new order, and the package must belong to the product. Catalog
objects are not created automatically. Later payment events can still reference
the original immutable purchase even if its product is subsequently archived.

Price is the per-unit purchase price, not the payment amount. Price/amount must
be positive, have at most two decimals and fit numeric(12,2); order total is at
most 9999999999.99. Partial payments are allowed; confirmed payments, including
existing manual payments, cannot exceed the order total. Currency is an uppercase
normalized alphabetic code of 3..10 characters (for example USD, UAH or USDT),
not a claim of ISO validation or exchange-rate support. All payments must match
the order currency. Installment count defaults to 1, range 1..120; an optional
current installment cannot exceed it. Provider/invoice are optional, max 100.
If the connector aggregates several payment providers, it must namespace their
payment IDs so ExternalId remains unique across this one integration source.

Only confirmed Paid payments are imported. Pending events can omit payments;
cancelled payments, refunds, currency conversion, negative/zero amounts and
mutating a previously accepted payment are not handled by this endpoint. A later
payment event uses a NEW eventId, the SAME Deal ID and the SAME immutable order
data, with either new payment entries or a snapshot including previously accepted
entries. Existing payment IDs are skipped only if their data and order match.

Purchase/payment timestamps must be supplied, no earlier than 1970 or in the
future. They are normalized to UTC. The complete purchase instant is retained in
the import order link; the existing Order model and purchase UI expose its UTC
calendar date. Subscription duration still starts at actual access grant.

`initialBudget`, `totalEarnings`, client roles and arbitrary metadata are not part
of the contract. Unknown JSON properties are rejected, including nested profile
financial fields. Existing client financials are not overwritten; a new client
retains the domain's zero-valued financial defaults.

## Response

201 for a newly created order; 200 for accepted events on an existing order or
an exact event replay:

```json
{
  "clientId": "<client ULID>",
  "orderId": "<order ULID>",
  "clientCreated": true,
  "orderCreated": true,
  "paymentsCreated": 1,
  "subscriptionsCreated": 2,
  "replayed": false
}
```

Subscription count depends on the real catalog: two is illustrative for a course
with both platform and Discord durations. Counts describe that event, not totals.
On replay the original receipt fields/counts are returned with replayed=true;
they do NOT represent additional newly created rows. No amounts, secrets, names
or emails are echoed in the response.

## Backend Logic

1. Start a Serializable transaction and look up `(source,eventId)`.
2. If a receipt exists, compare normalized content fingerprints. Same content
   returns its prior response; changed content conflicts before any mutation.
3. Resolve client by Person ID and normalized ASCII email, searching all email
   aliases. An ambiguous email or disagreement between the two identifiers is
   a conflict. No automatic merges or reassignment of external IDs.
4. An email-only match can be linked to the Person ID unless already linked to
   another person. A Person-ID match can add an unclaimed email as an alias;
   existing name, primary address, notes, contacts and finances are preserved.
5. Resolve/create the Deal-ID order. The same deal cannot change client,
   purchase timestamp, currency, price, quantity, product or package through this
   importer. For changed purchases use a separately designed business operation,
   not a fresh event ID to bypass the rule.
6. Deduplicate payments globally within the source, not merely within a deal.
   The same payment ID with another order or different payment data conflicts.
7. Stage the existing automatic entitlement rules. Successful payments create
   eligible AwaitingAccess subscriptions, not active access or enrollments.
8. Persist client changes, links, order, payments, subscriptions, authenticated
   source audit and the receipt in one SaveChanges and transaction commit.

Rejected events leave no partial email/link/client/order/payment mutations.
Serializable conflicts and unique-key collisions return a retryable conflict;
retry in a fresh HTTP request with the same event ID and unchanged content.
Event, order and payment fingerprints normalize property ordering, email case,
decimal scale, UTC timestamps and item/payment array ordering. They are hashes
of content, not authentication signatures. Only the shared service credential
authenticates the sender; this is not a native signed Pipedrive webhook contract.

## Validation And Errors

- 400: invalid/missing fields, unsupported status, unknown properties, malformed
  identifiers, precision/date/currency or installment errors.
- 401: missing/wrong/disabled integration credentials, including JWT-only callers.
- 409: identity ambiguity/conflict; unresolved/inactive catalog item; changed
  accepted event/deal/payment; mixed existing order currency; overpayment.
- 409 import_concurrent_change: detected unique/serialization/deadlock conflict;
  retry unchanged event with bounded backoff, not an endless loop.
- 413: HTTP request body exceeds 128 KiB.
- 429: process-local limit of 60 ingress calls/minute per connection IP.
- 500: unexpected infrastructure error, existing generic handler with trace ID.

Conflicts use `{ "code": "client_identity_conflict", "message": "..." }`.
Credential values and raw request bodies are not logged by the feature. The
audit actor is an explicit source in metadata, not a fabricated human User ID.
Behind a proxy the IP limiter has the same forwarding caveats as backend-security.md.

## ToolJet Integration

ToolJet does not send this ingestion request. It continues to read /clients,
/clients/{id} and /clients/{id}/purchases. Source errors should be surfaced to the
connector/operator for manual resolution. No automatic background refresh, webhook
delivery to ToolJet, UI changes or external access provisioning are implemented.
Staff confirm actual access and assign the cohort/group through existing actions.

## Code And Schema Review

- Controllers/Integrations/SalesOrdersController.cs: narrow HTTP boundary.
- Auth/SalesIntegrationAuthentication.cs: dedicated scheme and constant-time key comparison.
- Features/Integrations/Sales: input, fingerprints, matching, catalog and transaction orchestration.
- Domain/Integrations and Infrastructure/Configuration/Integrations: persisted identities and receipt.
- Migration AddSalesOrderIngress: three new tables and provider/external-ID uniqueness.
- SalesContractTests / SecurityHttpTests / SalesIngressTests: source examples of intended behavior.

The migration normalizes legacy provider names and refuses ambiguous identities
instead of deleting/merging them. It and its target-model/snapshot metadata were
authored as source, not generated or checked by EF tooling. Nothing was applied.
Downgrade drops integration bookkeeping and cannot restore original provider
casing; it must not be treated as a harmless data-preserving operation.

This is an intentionally bounded import contract, not a refund system, general
CRM synchronization engine or verified production integration. Before any real
use it requires compilation, migration review and PostgreSQL/concurrency tests.
