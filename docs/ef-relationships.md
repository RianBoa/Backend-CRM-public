# EF relationship corrections

## Cause and ownership

An empty `WithOne()` / `WithMany()` means **no inverse navigation**, not "discover
the inverse automatically". Configuring the other navigation separately created
additional relationships and shadow foreign keys in the original model.

Each affected relationship now has one configuration owner:

| Dependent | Principal / inverse collection | Foreign key | Configuration owner |
| --- | --- | --- | --- |
| Case | Client.Cases | ClientId | CaseConfiguration |
| AppliedBenefit | Order.AppliedBenefits | OrderId | AppliedBenefitConfiguration |
| AppliedBenefit | PromoBenefitRule.AppliedBenefits | PromoBenefitRuleId | AppliedBenefitConfiguration |
| DiscordAccess | Subscription.DiscordAccesses | SubscriptionId | DiscordAccessConfiguration |
| PlatformAccess | Subscription.PlatformAccesses | SubscriptionId | PlatformAccessConfiguration |
| SubscriptionAdjustment | Subscription.Adjustments | SubscriptionId | SubscriptionAdjustmentConfiguration |

The reference and inverse collection use the **same explicit CLR foreign key**.
In particular, navigation-based client case counts and access-to-subscription
queries no longer depend on a separate `*Id1` value. ULID keys and existing column
names, including `subscription_id`, are preserved. Promo rule deletion remains
restricted when applied benefits exist; access rows still cascade with their subscription.

There was also a conflicting delete policy for Enrollment.GroupId. Its single
owner is now EnrollmentConfiguration: deleting a group sets GroupId to null
instead of deleting enrollments and their learning history. This does not change
other relationships' delete policies or introduce a group-deletion API.

## Migration source

`20261023130000_FixDuplicateRelationships` follows the existing migrations rather
than rewriting their history. It removes six redundant columns, indexes and
foreign keys, and changes the enrollment-group FK to ON DELETE SET NULL.

Before removing anything it locks the affected tables for the migration transaction
and checks that every non-null shadow key equals its canonical key. A mismatch
aborts the migration with a table/column-specific error. Resolving such data needs
a separate, reviewed reconciliation decision; this migration never silently merges
clients, purchases or subscriptions. Null optional shadow keys need no backfill:
the canonical foreign keys are already required and constrained in the old schema.

The Down path recreates the old columns and constraints from the canonical keys.
It restores the old schema, **not historical null values** in the redundant columns.
It also restores the old group cascade policy. Review rollback implications before
using it. Deployment should stop writes from old application versions first: those
versions still expect the redundant columns. The table locks require a maintenance
window sized for the actual data volume.

The migration, target model and current snapshot were edited as source only.
Older migration target models intentionally retain their historical shadow keys.
Nothing here confirms that a deployed database has been inspected or upgraded.

## Regression coverage

`tests/Backend.Tests/RelationshipModelTests.cs` uses the PostgreSQL provider's model
metadata without opening a database connection. It checks all six navigation pairs,
key types, physical column names, requiredness, cardinality and delete behavior.
It rejects unexpected non-owned shadow foreign keys and promotes EF's shadow-FK
creation warning to an error in this model check. It also checks that group deletion
preserves enrollments.

`ClientDetailQueryTests` additionally checks that the navigation-based total case
count matches the workspace case summary.

These are test sources, not a report of passing tests. No build, test execution,
database startup or migration application was performed for these corrections.
