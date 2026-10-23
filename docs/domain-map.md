# Domain relationship map

This is a source-model overview, not a dump of a running database. See
ef-relationships.md for the guarded schema correction and migration limitations.

```mermaid
erDiagram
    Client ||--o{ ClientEmail : emails
    Client ||--o{ ExternalAccount : external_identity
    Client ||--o{ Order : purchases
    Order ||--o{ OrderItem : items
    Order ||--o{ Payment : payments
    Product ||--o{ Package : packages
    Product ||--o{ OrderItem : purchased_product
    Package ||--o{ OrderItem : purchased_package
    Client ||--o{ Subscription : entitlements
    OrderItem ||--o{ Subscription : provisions
    Subscription ||--o{ DiscordAccess : recorded_grants
    Subscription ||--o{ PlatformAccess : recorded_grants
    Subscription ||--o{ SubscriptionAdjustment : changes
    Product o|--o{ Cohort : learning_product
    Cohort ||--o{ Group : groups
    Order ||--o{ Enrollment : learner_purchase
    OrderItem o|--o{ Enrollment : purchase_item
    Cohort o|--o{ Enrollment : assigned_cohort
    Group o|--o{ Enrollment : assigned_group
    RoadMapTemplate ||--o{ RoadMapItem : checklist
    Enrollment ||--o{ RoadMapAssignment : achievements
    RoadMapItem ||--o{ RoadMapAssignment : assigned_item
    User ||--o{ RoadMapAssignment : assigned_by
    Client ||--o{ Case : reported_results
    Client o|--o{ Application : related_client
    Application ||--o{ ApplicationReply : conversation
    User ||--o{ ApplicationReply : replies_by
    User ||--o{ RefreshToken : sessions
    Order ||--o| SalesOrderLink : external_deal
    Payment ||--o| SalesPaymentLink : external_payment
```

ULID identifies clients, orders/items/payments, users, learning structures,
subscriptions and team requests. Product/package/template/item IDs are integers.
Never join a user to Group.MentorId through a short display ID: MentorId is ULID?.

OrderItem is the purchase identity. ProductId cannot distinguish repeat purchases,
packages or multiple learning enrollments. Optional OrderItemId on Enrollment
exists for legacy rows; new manual enrollment requires the purchased item.

A first successful payment creates eligible subscriptions. Access activation starts
the configured duration. Cohort dates describe learning, independently of payment
and subscription dates. Managers assign groups manually; curators record roadmap
progress. Applications coordinate team work and audit captures resulting mutations.

For imports, Person ID belongs to Client.ExternalAccounts, Deal ID to SalesOrderLink
and external payment ID to SalesPaymentLink. SalesImportReceipt stores event replay
state. Profiles' initialBudget and totalEarnings are not external payment fields.
