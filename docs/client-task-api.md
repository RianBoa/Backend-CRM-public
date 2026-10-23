# Client API organized by task

The first migrated block is the client profile. Existing client routes remain
available while consumers move to the task API.

## Profile

- `GET /clients/{id}/profile`: load the profile form.
- `PUT /clients/{id}/profile`: replace the complete profile block.
- Both require a bearer token with `manage_clients`.
- `id` is a client ULID. Invalid identifiers return 400; missing clients return 404.
- Missing credentials return 401; missing permission returns 403.

Request and successful response use the same fields:

```json
{
  "name": "Demo Client",
  "notes": "Prefers evening sessions",
  "preferredLanguage": "uk",
  "source": "referral",
  "city": "Kyiv",
  "birthDate": null,
  "occupation": null
}
```

PUT replaces this block, including optional fields. An omitted optional field,
null, or a blank string clears its value. Name is required. Length limits match
the database; future birth dates are rejected. Contacts, emails, financials and
learning assignments are outside this operation.

ToolJet loads this response into the profile form and submits the entire small
block on Save. The successful response becomes the new form state.

## Code boundaries

The legacy ClientsController is also now an HTTP-only adapter. Its list/select,
detail and command logic lives in Features/Clients/Directory, Workspace and
Commands respectively. Public DTOs moved into Features/Clients/Contracts.
See client-module-architecture.md for the reading order and compatibility notes.

- `Controllers/Clients/ClientProfileController.cs`: HTTP routing, authorization and status codes.
- `Features/Clients/Profile/ClientProfile.cs`: form contract and input validation.
- `Features/Clients/Profile/ClientProfileService.cs`: load/save scenario using domain methods.
- Domain models retain business rules; EF configurations retain storage mapping.
- Profile and audit event are saved atomically with one SaveChanges call.

## Contacts

`GET /clients/{id}/contacts` loads the contact form. `PUT` at the same route
replaces only phone and social contacts. Both require `manage_clients`.
The request and response have identical fields:

```json
{
  "phoneNumber": "+380671234567",
  "telegram": "@student",
  "discord": null,
  "instagram": null
}
```

Null, blank or omitted fields clear their values. Strings are trimmed. Phone is
6..32 characters when supplied; Telegram and Discord are at most 64 characters,
Instagram at most 128. Telegram cannot contain whitespace. Invalid fields or an
invalid ULID return 400; a missing client returns 404. Removing the last contact
returns 409 with `last_contact_required` and preserves the previous state.
Email, phone, Telegram and Discord qualify as contact methods; Instagram alone
does not, matching the existing domain rule. Existing emails remain unchanged.
Profile, finances and learning data are outside this operation.

After success ToolJet uses the normalized response as the contact form state.
Repeated unchanged saves do not append duplicate audit events. A change and its
audit event are saved together; contact values are not duplicated in audit JSON.

## Following blocks

Financials, purchases/access and learning are separate tasks. Each
should have a focused read model and explicit write semantics. Commands such as
setting the primary email or assigning a group remain dedicated actions.
UI tabs can consume these blocks without making component names part of the API.

This is the first migration slice, not a completed migration of all controllers.
All client routes now require authentication. Financial isolation and the
ToolJet migration requirements are documented in backend-security.md.
