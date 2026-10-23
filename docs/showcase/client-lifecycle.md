# Showcase: from purchase to student achievement

This is a reading scenario with illustrative requests and expected contract
fragments, not a recorded execution or a deployment guide. No real client,
payment, access or enrollment is created by this document.

## Business Story

A manager creates a client. An administrator records a course purchase and its
first successful partial payment. The backend creates eligible subscriptions
without starting their duration. Once access is actually issued, onboarding
confirms the grant and manually assigns the student to a group. A curator
records an achievement in the student's assigned roadmap.

The existing catalog is assumed to contain a Course product with a Mentorship
package: DurationDays=30, DiscordAccess=true, DiscordDays=30. A compatible
cohort/group and a roadmap item are assumed to exist. Placeholder IDs below refer
to related records, not arbitrary interchangeable products or purchases.

## 1. Create The Client

Endpoint: `/clients`. Method: POST. Params: none.

```json
{
  "name": "Demo Student",
  "emails": [{ "email": "student@example.test", "isPrimary": true }]
}
```

Expected response: 201 with the normal client detail object. Its id feeds later
requests. The financial block is not returned. Domain creation preserves at
least one contact method; CreatedBy is the authenticated actor, not a body value.

ToolJet: submit the small create form, retain the returned id and open the workspace.
Code: ClientsController -> ClientWriteService.CreateAsync -> Client.Create.

## 2. Record Purchase And First Payment

Endpoint: `/orders`. Method: POST. Requires Admin under current write policy.

```json
{
  "clientId": "<client ULID>",
  "source": "Manual",
  "items": [{ "productId": 1, "packageId": 10, "price": 100, "quantity": 1 }],
  "payments": [{ "amount": 25, "currency": "USD", "status": "Paid", "totalInstallments": 4, "currentInstallment": 1 }]
}
```

Expected response: 201 with order detail, including items and payments. The item
id is the purchase identity for learning and subscriptions. Eligible CoursePlatform
and DiscordCourseBonus subscriptions are created once for that purchase.
They have status AwaitingAccess and null start/end dates. Another Paid payment
does not create another entitlement of the same type for this purchase.

ToolJet: the financial screen is Admin-only for writes; onboarding uses the safe
purchase list instead of this monetary order response.
Code: OrdersController -> OrderSubscriptionProvisioner -> SubscriptionEligibility.

The HTTP adapter now delegates to OrderManagementService before provisioning.
Other resource adapters follow the same feature boundary; see
../project-review-checklist.md and ../api-conventions.md. This walkthrough remains
an illustrative contract, not evidence that the requests were executed.

## 3. Inspect Without Confusing Dates

Endpoint: `/clients/{id}/purchases/{orderItemId}`. Method: GET.
Params: client ULID, purchase ULID; independently paged subscriptions/enrollments.

Expected contract: purchase metadata plus paged subscriptions and enrollments.
At this point subscriptions await access; no learning dates are invented from
the payment or subscription. The same product bought twice remains two purchases.

ToolJet: select a purchase on the left; load this detail on the right.
Code: ClientPurchaseQueries and purchase-scoping test examples.

## 4. Confirm Actual Access Grant

Endpoint: `/subscriptions/{id}/activate-access`. Method: POST.
Params: the CoursePlatform subscription ULID. Allowed actors: Admin/OnBoarding.

```json
{ "grantedAt": "2026-06-10T09:00:00Z" }
```

Expected response fragment for a configured 30-day term:

```json
{
  "status": "Active",
  "durationDays": 30,
  "startAt": "2026-06-10T09:00:00Z",
  "endAt": "2026-07-10T09:00:00Z"
}
```

The grant timestamp must not be in the future. Repeating activation does not
restart the term. This endpoint records a confirmation; it does not call an
external learning platform or Discord API. Discord and platform access records
are separate; activating one subscription does not automatically activate another.

ToolJet: an explicit Confirm access button, not a side effect of opening a card.
Code: AccessActivationService -> Subscription.ActivateAccess.

## 5. Create And Assign Enrollment Manually

Endpoint: `/learning-structure/enrollments`. Method: POST. Admin/OnBoarding.

```json
{ "orderId": "<order ULID>", "orderItemId": "<purchase ULID>" }
```

Then POST `/learning/enrollments/{id}/assign-group`:

```json
{ "groupId": "<compatible group ULID>" }
```

Expected response: enrollment view with group/cohort and Active learning status.
LearningStartAt/LearningEndAt come from the cohort, not access timestamps.
Product/package mismatch or a closed cohort is a conflict. An incompatible
assignment is rejected rather than silently corrected.

ToolJet: assignment queue -> compatible groups -> Assign button -> refresh queue.
Code: EnrollmentService.AssignAsync -> Enrollment.AssignToGroup.

## 6. Record An Achievement

Roadmap assignments are not generated automatically in this flow. First POST
`/roadmaps/assignments` as the curator, using that curator's own id:

```json
{
  "roadMapItemId": 1,
  "enrollmentId": "<enrollment ULID>",
  "dueAtDaysFromEnrollment": 7,
  "userId": "<authenticated curator ULID>"
}
```

GET `/learning/enrollments/{id}/roadmap` supplies the checklist. PUT
`/learning/enrollments/{id}/roadmap/{assignmentId}/completion` submits:

```json
{ "completed": true }
```

Expected response: updated item with completed=true and status=Completed. The
assignment must belong to this enrollment. Repeating the same value does not
append another audit event. A skipped task requires explicit restoration.

ToolJet: bind the checkbox to completed, send only the small completion payload.
Code: StudentRoadMapService -> RoadMapAssignment.SetCompletion.

## Review Checklist

- Purchase, entitlement, actual grant and learning are distinct concepts.
- Repeat purchases are identified by order item, not product alone.
- Roles are checked by the backend, not only hidden UI controls.
- Command changes and their audit events commit together where implemented.
- Read models do not directly perform mutations or external provider calls.
- Repeated entitlement provisioning and activation have explicit duplicate rules.
- Full client detail is a workspace snapshot, not an unbounded-history solution.

The repository is not claimed fully migrated or production-ready. External
provider adapters, remaining legacy modules and PostgreSQL verification are
separate concerns. Test source demonstrates intended behavior, not execution of
this narrative. No infrastructure startup is necessary to review these files.
