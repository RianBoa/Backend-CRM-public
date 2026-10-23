# Client purchases workspace

## Endpoints

- `GET /clients/{id}/purchases`: the left-side purchase list.
- `GET /clients/{id}/purchases/{orderItemId}`: the selected purchase panel.
- Both require a JWT with `manage_clients`.
- Both identifiers are ULIDs. Invalid IDs/parameters return 400; missing client
  on the list returns 404. Missing or foreign purchase detail returns the same
  404 response, without disclosing another client's purchase.

No request body is needed. Missing authentication returns 401 and a missing
permission returns 403. Unexpected failures use the existing error middleware.

## List parameters

| Parameter | Allowed value | Default |
| --- | --- | --- |
| page | integer 1..1000000 | 1 |
| pageSize | integer 1..100 | 20 |
| search | product name/code or package name; up to 120 characters | none |
| productId | positive integer | none |
| sortBy | purchaseDate, productName | purchaseDate |
| sortDir | asc, desc | desc |

Filters run before counting and paging. Each ordering has an ID tiebreaker.
The list returns the existing `items/total/page/pageSize` envelope. An existing
client without purchases gets an empty page. Repeated purchases of the same
product remain distinct items, selected by orderItemId.

```json
{
  "items": [
    {
      "orderItemId": "01K00000000000000000000001",
      "orderId": "01K00000000000000000000002",
      "productId": 1,
      "productCode": "core",
      "productName": "Core Program",
      "productType": "Course",
      "packageId": 1,
      "packageName": "Standard",
      "quantity": 1,
      "purchaseDate": "2026-10-01",
      "orderStatus": "Active"
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 20
}
```

## Detail

Detail contains `purchase` (the same item shape), `subscriptions` and
`enrollments`. Each child section uses `items/total/page/pageSize`.
Query parameters: subscriptionPage and enrollmentPage (1..1000000),
pageSize (1..100); defaults are 1, 1 and 20. Related lists are ordered by start
date/enrollment date descending then ID. They are fetched only after ownership
has been checked and are never joined by product ID alone.

Subscription items contain id, type, status, subscriptionStartAt,
subscriptionEndAt, discordAccessRecordsCount and platformAccessRecordsCount.
These names deliberately describe stored subscription terms and access records,
not verification of active permissions on an external platform.

Enrollment items contain id, status, cohortId/cohortName, groupId/groupName,
enrolledAt, learningStartAt and learningEndAt. Learning dates come only from the
cohort and remain null until one is assigned. Legacy enrollments without an
order item are excluded rather than guessed from the order or product.

## Semantics and limitations

- purchaseDate is the order creation date; the model has no separate purchase
  timestamp. It is not the successful-payment date.
- orderStatus, subscription status and enrollment status remain separate.
- Actual access grant activation is still a planned subscription lifecycle
  change; this endpoint reports current stored terms honestly.
- Prices, payments, client financial values, freeform metadata and order comments
  are not exposed by this operational view. Order has no explicit comment field.
- Quantity is reported as stored. Per-unit entitlement modeling is not added.
- Paginated sections are read sequentially without snapshot isolation; concurrent
  edits can change counts between the count and page queries.

## ToolJet integration

Load purchases when opening the tab. Bind the list to `data.items`, and use
orderItemId as its key. On selection, call the detail endpoint using both client
ID and selected orderItemId. Bind the right panel to `data.purchase`, the access
table to `data.subscriptions.items` and learning to `data.enrollments.items`.
Reset child pagination when selecting another purchase. The existing client
detail contract is unchanged.

## Verification

ClientPurchaseTests cover repeat purchases, client isolation, related-record
isolation, child pagination, absent clients and sorting validation. Query tests
use EF InMemory and omit its unsupported jsonb property, which is not selected
by this feature. They do not establish Npgsql SQL translation or live HTTP
authorization. PostgreSQL execution remains a separate verification task.
