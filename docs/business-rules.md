# FFA business rules

Confirmed with the project owner on 2026-10-23. This document records requirements,
not a claim that the existing implementation satisfies them.

## Purchases and subscriptions

- Only eligible products create subscriptions.
- Subscription creation is automatic; duration comes from database configuration.
- The first successful payment triggers automatic subscription creation for
  eligible products; full payment is not required for this trigger.
- The subscription duration starts at actual access grant, not at payment time.
  Creation after payment and activation after access grant are separate events.
  Before access is granted, the subscription must not consume its duration or be
  reported as active access. EndAt is calculated from the actual grant time and
  the configured duration. Reprocessing the same grant must not reset the clock.
- Packages currently expose DurationDays, DiscordAccess and DiscordDays. The exact
  eligibility rule and duration source for each subscription type still need
  confirmation. For subscriptions covering multiple access channels, the event
  that constitutes actual access grant must be specified explicitly.
- Repeat purchases, extensions, cancellation, refunds and the remaining partial
  payment rules have not yet been specified.
- Subscription automation must be idempotent so a repeated payment notification
  cannot duplicate entitlement records.

## Learning operations

- Enrollment belongs to a specific order item.
- OnBoarding managers manually assign students to cohorts and groups.
- Groups belong to cohorts, and cohorts belong to products.
- Managers need queues of people awaiting assignment, all cohorts/groups and
  active cohorts/groups. Lists must support server-side pagination and filters.
- A student can move between cohorts and groups.
- Required learning states: pending, active, completed, frozen, access revoked.
- Learning dates come from the cohort; subscription dates describe entitlement.
- OnBoarding managers manage learning, including assignment, transfer, freezing,
  completion and access revocation. Admin retains full access. Additional learning
  management grants to other roles have not been confirmed by these decisions.
- Revoked access must have an explicit meaning; existing Dropped/Closed states
  must not silently be interpreted as confirmed external access revocation.

## Internal requests (applications)

- Applications are typed internal team requests, optionally linked to a client.
- Examples: curator requests cohort transfer, Discord access or conflict handling
  by a troubleshooter.
- Team members can answer a request and change its status.
- Client changes made while handling a request must be recorded in history.
- Replies have their own model with author and timestamp. Audit and client
  activity must remain traceable to the related request when applicable; the
  implementation is described in application-workspace.md.

## Roadmaps and cases

- Roadmaps track student achievements as checklists maintained by curators.
- Cases record student financial results. Preserve the existing scope for now.

## Financial visibility

- Allowed audiences: sales team, TeamLead, Admin and product manager.
- Product manager corresponds to the existing ProjectManager role.
- Sales is a separate planned role; OnBoarding is not Sales and does not gain
  financial visibility through its learning responsibilities.
- The human Sales role is still deferred. Incoming sales-data import has a
  separate service-authenticated contract in sales-ingress-api.md: Person ID
  identifies the client, Deal ID the order; initialBudget/totalEarnings are excluded.
  This is source implementation, not a deployed or verified external integration.
- Financial visibility must be enforced in every backend response that exposes
  amounts, not only through UI visibility rules.
- Permission to view financials does not automatically imply permission to record
  payments, issue refunds or change financial values.

## Implementation direction

Use task-focused contracts and application services, with thin HTTP controllers.
Keep business invariants in domain models. Record state changes and audit events
atomically. Preserve existing consumers during incremental migration.
