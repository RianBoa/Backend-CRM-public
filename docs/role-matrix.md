# Roles and cumulative authorization

The source of truth is AuthPolicies.GetPermissions plus controller attributes.
This table distinguishes a module permission from narrower operation overrides.

| Role | Module permissions |
| --- | --- |
| Admin | All listed module and financial permissions |
| OnBoarding | Clients, Orders, Applications, LearningStructure |
| Curator | Clients, Orders, Subscriptions, Applications, Cases, LearningStructure, RoadMaps |
| ProjectManager | Clients, Orders, Products, Subscriptions, Applications, Cases, LearningStructure, RoadMaps, Users; ViewFinancials |
| TroubleShooter | Clients, Subscriptions, Applications, Cases |
| Analyst | Cases |
| TeamLead | Same module/financial-read permissions as ProjectManager |
| Developer | Products only; not an administrative bypass |

Additional restrictions are cumulative, not alternatives:

| Operation | Effective restriction |
| --- | --- |
| General client workspace | ManageClients; financial block omitted, case amounts masked without ViewFinancials |
| Financial summary / full order reads | ViewFinancials; orders also require ManageOrders |
| Financial changes / order or payment creation / monetary case creation | ManageFinancials, currently Admin only, plus the resource module policy |
| User create/patch/status/password | Admin role plus ManageUsers; self-demotion/deactivation rejected |
| Cohort/group/enrollment mutation | Admin or OnBoarding plus ManageLearningStructure |
| Roadmap checklist | ManageRoadMaps; assignment is scoped to its enrollment |
| Application conversation | ManageApplications; authenticated actor attribution |
| Product reads | Authenticated fallback policy; catalog changes require ManageProducts |
| Manual subscription creation / manual date override / direct legacy activation | Admin plus ManageSubscriptions; automatic dates remain lifecycle-managed |
| Resume suspended, unexpired subscription | Admin or OnBoarding through POST /subscriptions/{id}/resume-access; expiry is preserved |
| Machine sales import | Dedicated SalesIntegration scheme and receive_sales_orders policy, not a human Admin JWT |

Sales is intentionally not a user role yet. External ingress is a machine identity,
not permission to edit all clients or budgets. OnBoarding/Curator having ManageOrders
does not override the financial restriction on full orders: use the safe client
purchases workspace for non-financial context.

Each JWT session is checked against current user/session state and permissions are
rebuilt from the database. Password, role and activation changes revoke sessions.
The current model is module RBAC, not multi-tenant or per-client ownership isolation.
See backend-security.md for session lifecycle, bootstrap and operational limits.
