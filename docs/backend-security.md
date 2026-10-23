# Backend security and ToolJet migration

## Authorization

Program and the HTTP test host share `Auth/BackendSecurity.cs`. All routes require authentication by default. The only anonymous controller routes are login, refresh, logout and health. Logout requires possession of the opaque refresh token, not a JWT. There is no public first-user registration endpoint. Sales ingress uses a separate, narrowly scoped service-key scheme; see sales-ingress-api.md. A user JWT alone does not authorize it.

Existing module permissions still apply. Additional restrictions are cumulative:

| Operation | Allowed roles |
| --- | --- |
| Read clients/profile/contacts/purchases | Existing ManageClients roles |
| Read budgets, earnings, order prices and payments | Admin, TeamLead, ProjectManager |
| Write financial data, create orders/payments, create monetary cases | Admin |
| Create/change users, passwords, roles and activation | Admin |
| Change cohorts, groups or enrollments | Admin, OnBoarding |
| Read learning data / manage achievement checklist | Existing module policies |

Sales is not yet a domain role. Separate `view_financials` and `manage_financials` permissions allow adding it deliberately later. Admin-only financial writes are a conservative default pending the sales workflow. Curators can view case metadata and perform existing non-monetary case actions, but cannot create monetary cases. Self-demotion and self-deactivation are rejected. Authorization remains module-based, not per-client ownership or multi-tenant isolation. Purchase IDs and roadmap assignment IDs are additionally scoped to their parent resource.

## Endpoint

`/clients/{id}/financials`

## Method

`GET` reads; `PUT` replaces the financial form block.

## Params

`id`: client ULID. No query parameters.

## Request Body

PUT requires both fields. Null, omission and negative values are rejected. Maximum 9999999999.99 matches existing PostgreSQL numeric(12,2) columns. The domain rounds to two decimal places.

```json
{
  "initialBudget": 1000.00,
  "totalEarnings": 2500.00
}
```

## Response

GET and PUT return the same JSON block shown above.

## Backend Logic

GET projects only required values. PUT changes only this block and writes an audit event with the authenticated actor in the same SaveChanges operation. Identical values are a no-op. Contacts/profile are preserved. Competing financial edits currently use last-write-wins; there is no ETag/version contract.

General client detail omits `financials` for all roles. Unauthorized case amounts are null in case endpoints and embedded client case lists; sorting by amount is forbidden without financial read permission. Orders require financial read permission plus ManageOrders; mutations additionally require financial write permission. Safe purchase endpoints retain product/enrollment context without prices/payments.

Legacy client POST/PUT/PATCH reject a supplied financial block with 403 without write permission, rather than silently accepting part of the operation. Omitted financials on legacy PUT preserve existing amounts. Admin writes through legacy routes remain supported and audited. New clients without financials start at zero. Client CreatedBy, case author and roadmap assigning actor cannot be freely impersonated through the body.

## Validation And Errors

- 400: malformed ULID or invalid body/financial values.
- 401: no/invalid/expired JWT, missing/revoked/expired session, inactive or missing user.
- 403: active session without the required permission/role.
- 404: authorized reader requests a missing resource.
- 409: detected concurrent status/session update; self-demotion/deactivation.
- 429: authentication rate limit.
- 500: generic internal error with trace ID, no exception stack in the response.

## Sessions

Access JWTs include sid, referencing the persisted refresh session. Each authenticated request checks session and user in one database query and rebuilds role/permission claims from the current database role. No authorization cache is used. JWT validation allows HS256 only and validates signature, issuer, audience and expiry, with 30 seconds of clock skew. Runtime signing keys require at least 32 bytes. Default access lifetime is 15 minutes; configured lifetime is capped at 60.

Refresh rotates the session and invalidates both the previous access JWT and refresh token. RevokedAtUtc is an EF concurrency token: a competing refresh cannot successfully reuse the same session on PostgreSQL. Logout revokes that session immediately. Password resets, role changes and user deactivation through Users API revoke all that user's sessions atomically with the user change and audit. Reactivation does not restore them. Direct SQL administration bypasses application-level revocation and should not be the normal management path.

Refresh tokens remain hashed in storage. Login and refresh share a process-local per-connection-IP limit of 10 requests/minute, no queue. Behind a reverse proxy this is the proxy IP unless trusted forwarding is configured. Arbitrary forwarded headers are intentionally not trusted. Multiple backend instances need gateway/distributed limiting. This baseline is not a complete brute-force defense.

## ToolJet Integration

1. Require login after deployment: old JWTs lack sid and are rejected. Existing active refresh tokens can still be rotated.
2. Send each employee's own JWT, never a shared Admin token. Do not embed credentials/tokens in exported apps or committed configuration. Serialize refresh per session, replacing both tokens together; do not refresh independently for parallel queries because rotation invalidates the previous JWT. Clear session state if refresh fails.
3. Use `/auth/me` permissions for UI visibility, with backend policies as the authority. User mutation buttons require Admin, not just the legacy ManageUsers permission. Learning mutation buttons require Admin/OnBoarding.
4. Replace `getClientById.data.financials` bindings with the separate authorized GET financials query and PUT form submit. Missing/hidden amounts are not zero.
5. OnBoarding/Curator product panels use `/clients/{id}/purchases`, not monetary order endpoints. Monetary case creation is now Admin-only. Do not endlessly refresh/retry on 403.

## Initial Admin

For a new empty database, apply reviewed migrations first. Set BootstrapAdmin__Email, BootstrapAdmin__Password (12+ characters), BootstrapAdmin__Telegram and BootstrapAdmin__FullName through a local secret manager or temporary environment, not committed configuration. From src/backend run `dotnet run -- --bootstrap-admin`.

The command creates an Admin and an audit event, then exits without HTTP. It refuses an existing user database and cannot reset/take over an account. Clear bootstrap credentials afterward. Existing installations keep their users and do not need this command. Run it once, not concurrently.

## Verification And Limits

SecurityHttpTests send real requests through TestServer, actual controllers/JWT middleware and the shared security registration, with EF InMemory as database. They cover anonymous requests, role matrices, financial bypass attempts, validation, general detail privacy, login/refresh/logout, stale roles, deactivation, password reset and invalid JWTs. They are not PostgreSQL tests or a deployed ToolJet smoke test. Real database concurrency, migrations, SQL translation, TLS/proxy setup and browser token handling still need integration verification.

No schema migration is needed for sid, permissions or concurrency metadata; the EF snapshot reflects the token configuration. Earlier feature migrations remain separate prerequisites and are not applied here. Existing shadow-FK/nullable warnings remain. Free-text notes and screenshots are not automatically redacted: restricted financial data or secrets must not be copied into broadly readable notes. This is hardening, not a production-security certification.
