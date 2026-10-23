# HTTP entry points

Controllers are grouped by business module, not by HTTP verb or individual form field.

| Folder | Responsibilities |
| --- | --- |
| Common | Shared HTTP controller base |
| Auth | Login, refresh, logout and current user |
| Clients | Client workspace, profile, contacts, financials and purchases |
| Applications | Internal requests, replies, processing and activity |
| Learning | Cohorts, groups, enrollments and onboarding workspace |
| Orders | Orders and payments |
| Products | Product catalog and packages |
| Subscriptions | Subscriptions, access records and grant confirmation |
| RoadMaps | Templates, assignments and student achievement checklist |
| Cases | Student results and case review |
| Users | Team directory and account administration |
| Health | Public service health endpoint |
| Integrations | Service-authenticated sales ingress, isolated from human JWT permissions |

Resource controllers delegate to `Features/<module>/Management`. Contracts,
queries, commands and validation live there, not in controller partial files.
Client contracts remain in `Features/Clients/Contracts`; task-focused services
remain beside their workspace controllers. Domain rules and EF mappings stay
in Domain and Infrastructure respectively.

Clients has completed this extraction: its controller is no longer partial;
DTOs are under Features/Clients/Contracts and queries/commands under that module.
See docs/client-module-architecture.md for the detailed source-only walkthrough.

The existing `backend.Controllers` namespace is intentionally retained so moving
files does not rename public CLR types, break partial classes or change consumers.
ASP.NET discovers controllers by type and attributes, not their folder path.
Existing route templates and authorization attributes are retained. CLR DTO
types moved out of controllers; JSON property shapes remain stable. Feature
services are registered centrally with `AddBackendFeatures`. Invalid filters,
out-of-range paging and unknown JSON write fields now return 400 explicitly.

For a new operation, select its business module first. Keep HTTP binding,
authorization and status-code mapping in the controller; put orchestration in
the matching feature service. Do not add more controller files to the root.
Auth and Health are explicit exceptions: they remain small infrastructure-facing
controllers. See docs/project-review-checklist.md for scope and verification limits.
