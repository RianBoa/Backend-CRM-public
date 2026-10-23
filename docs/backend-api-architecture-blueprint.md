# FFA: архитектурный blueprint backend/API

> Historical proposal, not the current implementation map. For the current
> source-only baseline see project-review-checklist.md, workspace-api-map.md,
> api-conventions.md and ef-relationships.md. Statements below describe the
> original survey unless explicitly updated.

**Статус:** предложение для согласования. **Дата обследования:** 2026-08-06. <br>
**Граница работ:** документ основан на коде и миграциях репозитория; никаких миграций, изменений схемы, данных `ffa` или `ffa_test` не выполнялось.

## 1. Решение в одном абзаце

FFA следует развивать как модульный монолит ASP.NET/EF Core с PostgreSQL: одна транзакционная модель записи, явные команды для изменения доменных сущностей и отдельные проекции (read models) для ToolJet. ToolJet — только OAuth/JWT-клиент backend API, никогда не пользователь PostgreSQL. Ресурсные endpoints остаются каноническим API изменений; `/onboarding/*`, `/clients/*`, `/cases/*`, `/learning/*` — стабильные read-контракты, не EF-сущности. Новые поля добавляются сначала в source of truth и DTO/read-model, а не в контроллер конкретного экрана.

## 2. Обследованное текущее состояние

### 2.1. Фактическая модель и связи

| Область | Уже есть | Ключевые связи / замечание |
|---|---|---|
| Клиенты | `Client`, `ClientEmail`, `ExternalAccount`; value objects contacts, financials, additional info | `Client -> Orders`, `Subscriptions`, `Cases`; `initial_budget` и `total_earnings` хранятся у клиента. |
| Продажи | `Order`, `OrderItem`, `Payment`, benefits/rules | `Order -> Client`, `OrderItem -> Product/Package`; `Order.CreatedAt` — `DateOnly`. |
| Каталог/доступ | `Product`, `Package`, `Subscription`, `DiscordAccess`, `PlatformAccess`, adjustments | Подписка привязана к `Client`, `OrderItem`, `Product`; доступы привязаны к подписке. |
| Обучение | `Cohort`, `Group`, `Enrollment` | enrollment привязан к **order**, затем опционально к cohort/group. Группа принадлежит cohort. |
| Кейсы | `Case` | Это торговый результат клиента, с суммой, валютой, proof URL, consent, featured и одним общим `Status`. |
| Команда | `User`, JWT, refresh tokens | В коде роли: Admin, OnBoarding, Curator, ProjectManager, TroubleShooter, Analyst, TeamLead, Developer. |
| История | `AuditLog` | `userId`, action, entity type/id, JSON metadata, createdAt. |
| Прочее | applications, roadmaps, curator notes, workflow tasks | Curator notes уже в схеме, но не представлены отдельным стабильным API в обследованных контроллерах. |

### 2.2. Существующие endpoints

Маршруты не имеют общего `/api/v1` префикса. Есть `auth`, `users`, `clients`, `orders`, `products`, `subscriptions`, `cases`, `learning-structure`, `applications`, `roadmaps`, `health`. CRUD/read уже есть для большинства из них: например, `GET /clients`, `GET /clients/{id}`, `GET/POST/PATCH /cases`, `GET/POST/PATCH /learning-structure/cohorts` и `GET/POST /learning-structure/enrollments`.

### 2.3. Критичное наблюдение по client-product endpoints

HEAD-коммит `fcace1a` добавлял `GET /clients/{id}/products` и `GET /clients/{id}/products/{productId}`. В текущем рабочем дереве оба обработчика удалены незакоммиченным diff из `src/backend/Controllers/Clients/ClientsController.cs`; типы DTO и helpers остались. Поэтому **в текущем дереве endpoints отсутствуют**, а API клиента уже частично возвращает другую, более широкую проекцию products. До merge/deploy владелец текущего локального изменения должен решить: восстановить handlers, либо выпустить совместимый v1 replacement. Удалять их сейчас нельзя.

### 2.4. Долг и противоречия

1. У `Case.Status` смешаны две независимые оси: проверка (`Approved`) и публикация (`Published`).
2. В DTO client-product `AccessStartAt/AccessEndAt` фактически берутся из `Subscription.StartAt/EndAt`; это срок подписки, не дата фактической выдачи платформенного доступа.
3. Enrollment содержит `AddedToPlatformAt`, но доменная модель не содержит команды, которая выставляет `IsAddedToPlatform` и дату; текущий endpoint это поле не изменяет.
4. Из `Enrollment.AssignToGroup` статус безусловно становится `Active`, а `ChangeStatus` разрешает любой переход — инварианты workflow не защищены.
5. Несколько `GET /clients*` помечены `AllowAnonymous`, что отменяет class-level policy и раскрывает клиентские PII при доступном backend.
6. Роли/permissions отражают исторические роли, а не требуемую минимальную матрицу. `Viewer` и `Finance` отсутствуют.
7. Audit infrastructure есть, но контроллеры пишут аудит отдельным `SaveChangesAsync`: изменение может сохраниться без audit-log при сбое второго шага; metadata не нормализованы как before/after.
8. Docker stack называет внутренний UI `appsmith`, а целевой контекст — ToolJet. Это не архитектурная проблема само по себе, но конфигурация и границы доступа должны быть подтверждены отдельно.
9. Мигратор запускается в каждом deploy. Это допустимо только для заранее проверенных, backward-compatible миграций; он не заменяет тестовый rollout.

## 3. Целевая граница модулей

Модули остаются в одном deployable приложении и одной БД, но имеют собственные domain/application/read API namespaces. Нельзя создавать foreign key между модулями без владельца связи и бизнес-инварианта; read-model может join-ить данные через EF projection.

| Модуль | Source of truth | Производные read models | Допустимые статусы/переходы |
|---|---|---|---|
| Identity / Users / Roles | `User`, role assignments, credentials, refresh tokens | current-user, assignee selector | User `active/inactive`; деактивация запрещает login и новые назначения, не стирает аудит. |
| Clients | `Client`, emails, contacts, external accounts | client list/detail, client timeline | Клиент не является onboarding workflow; удаления — только по отдельной retention-политике. |
| Sales & Finance | `Order`, items, payments, benefits | purchases, payment summary, client products | Order: `New -> Active -> Graduated`; отмена/возврат должны быть явными, если поддерживаются бизнесом. |
| Product Catalog & Access | `Product`, `Package`, `Subscription`, `PlatformAccess`, `DiscordAccess` | product-access card, active-access summary | Subscription: `Active -> Suspended|Canceled|Expired`; `Suspended -> Active`; terminal `Canceled/Expired` не реактивировать, создавать новую подписку. |
| Learning Structure | `Cohort`, `Group`, `Enrollment` | cohorts, cohort detail, enrollments | Cohort `Planned -> Active -> Graduated -> Archived`; Enrollment `Pending -> Active -> Frozen|Dropped|Closed|Graduated`; `Frozen -> Active|Dropped`; terminal не менять без reopen-команды. |
| Onboarding Operations | **новые** `OnboardingCase`, `OnboardingTransition` | onboarding queue/detail, SLA counters | `New -> Assigned -> InProgress -> WaitingClient -> Completed`; `Assigned|InProgress|WaitingClient -> Blocked`; `Blocked -> Assigned|InProgress`; `Completed` terminal. |
| Trading Cases | `TradingCase`, review decision, publication state | cases list/detail, client case summary | Review: `New -> Reviewed -> Approved|Rejected`, `Rejected -> Reviewed`; Publication: `Draft -> Published -> Unpublished`. |
| Notes / Activity / Audit | `CuratorNote`, `ActivityEvent`, `AuditLog` | client activity timeline, case/enrollment history | Notes mutable with author/updated timestamp; audit append-only. |

`Application`, `RoadMap*`, `WorkflowTask` остаются существующими bounded contexts; до их объединения в новый workflow нельзя переиспользовать их как onboarding/case status без согласования семантики.

### Таблица «сущность → истина → read model → команды»

| Сущность | Источник истины | Read model | Команды |
|---|---|---|---|
| Client | `clients`, emails/contacts | client list, client detail | create/patch client; manage email |
| Order/OrderItem/Payment | sales tables | purchase summary, client products | create/patch order; record payment |
| Subscription/Access | subscription/access tables | product detail, access summary | create/cancel/suspend subscription; grant/revoke access |
| Cohort/Group | learning tables | cohorts, cohort detail | create/patch/status cohort; create/patch group |
| Enrollment | `enrollments` | enrollments, client learning timeline | enroll; assign group; change status; mark platform/chat access |
| OnboardingCase | **new `onboarding_cases`** | onboarding queue/detail | create/assign/change status/record contact/block/complete |
| TradingCase | existing `cases`, evolved | cases list/detail | create/edit case; review; publish/unpublish |
| CuratorNote | curator notes | client detail/timeline | add/edit/archive note |
| ActivityEvent/AuditLog | new activity + current audit | timeline/audit | emitted atomically by every write command |

## 4. Единый контракт дат

Все API-имена — camelCase; timestamps — ISO-8601 UTC (`2026-08-06T10:15:00Z`); `DateOnly` — ISO date (`2026-08-06`). Никакое поле не меняет смысл по экрану.

| Поле API | Точный источник | Правило |
|---|---|---|
| `purchaseDate` | `Order.CreatedAt` | Производное поле; не создавать отдельную колонку. Для строки product выбирается дата конкретного order/order-item, не «последняя у клиента» без явного правила. |
| `accessDate` | `Enrollment.AddedToPlatformAt`, иначе `PlatformAccess.GrantedAt` | Дата фактического предоставления platform access. Если оба есть, отдавать оба в `accessSources`; основное `accessDate` выбирается по продукту/платформе, а не «максимумом» без правила. |
| `subscriptionStartAt` | `Subscription.StartAt` | Начало договорного срока подписки. |
| `subscriptionEndAt` | `Subscription.EndAt` | Конец договорного срока подписки. |
| `learningStartAt` | `Cohort.StartAt` | Начало потока. Не enrollment desired date. |
| `learningEndAt` | `Cohort.EndsAt` | Плановое/известное окончание обучения; nullable. |
| `enrollmentDate` | `Enrollment.EnrolledAt` | Когда запись о зачислении была создана. |

`DesiredStartDate` сохраняется как отдельное `desiredLearningStartAt`; он не заменяет `learningStartAt`. Для старого `AccessStartAt` нужен deprecation: не использовать в новых DTO, а в v1 задокументировать как subscription start.

## 5. Onboarding — самостоятельный workflow

Создать таблицу `onboarding_cases` (одна активная запись на клиента; partial unique index по `client_id` для не-terminal status) с полями: `id`, `client_id`, `status`, `assignee_user_id`, `assigned_at`, `sla_due_at`, `first_contact_at`, `completed_at`, `blocked_reason`, `created_at`, `updated_at`, `version`. История — `onboarding_transitions`: `id`, `onboarding_case_id`, `from_status`, `to_status`, `changed_by_user_id`, `changed_at`, `reason`, `metadata_json`.

Это должна быть отдельная таблица, потому что assignment, SLA, first contact, block reason и transition history являются фактами операционного процесса, которых нельзя надёжно вывести из Client/Order. Очередь строится из этой таблицы и joins клиента/последней покупки; новый клиент или оплаченный order создаёт case командой или outbox-обработчиком по согласованному правилу eligibility.

## 6. Торговые кейсы

Текущий `Case` нужно логически переименовать в API как `TradingCase` (физическое переименование таблицы — только поздняя отдельная миграция). Хранить: client id, `tradingDirection` (нынешний `Type` нужно подтвердить как направление/стратегию), result amount, currency, `initialBudgetAtCase`, proof URL(s), comment, `hasPublicationConsent`, reviewer/reviewedAt/reviewComment, publication fields и creator timestamps.

Разделение обязательно:

* `reviewStatus`: `New`, `InReview`, `Approved`, `Rejected` — качество/достоверность результата;
* `publicationStatus`: `Draft`, `Published`, `Unpublished` — видимость в публичном/внутреннем каталоге.

Публикация разрешена только при `reviewStatus=Approved` и `hasPublicationConsent=true`; `Published` не является review status. `isFeatured` — отдельный флаг, разрешён только Published. `initialBudgetAtCase` — snapshot в момент создания, не ссылка на текущий Client.Financials.InitialBudget.

## 7. API-стратегия

### Конвенции

* Вводить `/api/v1` через reverse proxy или новые controllers; старые маршруты остаются до миграции ToolJet. Не смешивать две версии на одном URL.
* Команды — resource-oriented `POST`, `PATCH`, специальные action endpoints только для перехода состояния (`POST .../transitions`, `.../access-grants`). Read endpoints отдают DTO/projection, ни navigation property/EF entity.
* Коллекции: `page` (>=1), `pageSize` (1..100), `sort` (`field` или `-field`), строгий allow-list filter/sort. Невалидный filter = `400`, не молчаливое игнорирование.
* Универсальная пагинация: `{ "items": [], "page": 1, "pageSize": 20, "total": 0 }`. Все ID — string ULID; catalog ID может оставаться integer, но в имени всегда `productId`.
* Конкурентность writes: `version`/ETag и `If-Match`; mismatch = `409 concurrency_conflict`.
* Ошибки — RFC 9457 `application/problem+json`: `{ "type", "title", "status", "code", "detail", "traceId", "errors" }`. `400` validation, `401`, `403`, `404`, `409`, `422` invalid transition.
* Все endpoints требуют JWT, кроме `POST /auth/login`, `POST /auth/refresh`, `/health`. Заменить текущие `AllowAnonymous` у client reads на read policies.

### Минимальные read contracts

| Endpoint | Фильтры / сортировка | Назначение |
|---|---|---|
| `GET /api/v1/onboarding/queue` | status, assigneeId, overdue, productId, purchasedFrom/To; `priority`, `slaDueAt`, `assignedAt` | Операционная очередь, не общий clients list. |
| `GET /api/v1/clients` | search, email, telegram, productId, onboardingStatus, createdFrom/To; `name`, `createdAt` | Карточотека, лёгкая строка. |
| `GET /api/v1/clients/{id}` | — | Сводная карточка: contacts, financials, counts, links на sections. |
| `GET /api/v1/clients/{id}/products` | status, activeOnly | Одна строка на purchase/subscription-product relationship, с точными date fields. |
| `GET /api/v1/clients/{id}/products/{productId}` | `orderId` обязателен, если у клиента >1 relationship с продуктом | Детальная проекция конкретного приобретения/доступа. |
| `GET /api/v1/cases` | clientId, reviewStatus, publicationStatus, reviewerId, direction, createdFrom/To; `createdAt`, `resultAmount` | Реестр торговых результатов. |
| `GET /api/v1/cases/{id}` | — | Кейc c review/publication и proof. |
| `GET /api/v1/learning/cohorts` | productId, status, startFrom/To; `startAt` | Потоки. |
| `GET /api/v1/learning/cohorts/{id}` | — | Поток, группы и summary enrollments. |
| `GET /api/v1/learning/enrollments` | clientId, orderId, cohortId, groupId, status, platformAccessState; `enrollmentDate` | Реестр зачислений. |

### Примеры JSON

```json
GET /api/v1/onboarding/queue?status=Assigned&overdue=true
{
  "items": [{
    "id": "01J...", "clientId": "01J...", "clientName": "Анна Иванова",
    "status": "Assigned", "assignee": { "id": "01J...", "name": "Олег" },
    "assignedAt": "2026-08-06T08:30:00Z", "slaDueAt": "2026-08-06T12:30:00Z",
    "firstContactAt": null, "purchaseDate": "2026-08-05", "isOverdue": true,
    "version": 3
  }], "page": 1, "pageSize": 20, "total": 1
}
```

```json
GET /api/v1/clients/01J.../products/42?orderId=01J...
{
  "clientId": "01J...", "productId": 42, "orderId": "01J...",
  "product": { "code": "COURSE-01", "name": "Trading Pro", "type": "Course" },
  "purchaseDate": "2026-08-01",
  "subscriptionStartAt": "2026-08-01T00:00:00Z",
  "subscriptionEndAt": "2026-11-01T00:00:00Z",
  "accessDate": "2026-08-02T09:10:00Z",
  "learningStartAt": "2026-08-15T09:00:00Z", "learningEndAt": "2026-11-15T18:00:00Z",
  "enrollmentDate": "2026-08-01T12:15:00Z", "accesses": [{ "kind": "platform", "grantedAt": "2026-08-02T09:10:00Z" }]
}
```

```json
POST /api/v1/cases/01J.../review
{ "decision": "Approved", "comment": "Proof verified", "version": 2 }

201/200
{ "id": "01J...", "reviewStatus": "Approved", "reviewedBy": { "id": "01J...", "name": "Куратор" }, "reviewedAt": "2026-08-06T10:20:00Z", "publicationStatus": "Draft", "version": 3 }
```

### Минимальные команды

| Команда | Endpoint | Инвариант |
|---|---|---|
| Назначить onboarding | `POST /api/v1/onboarding/{id}/assignments` | assignee active; assignment date сервера; audit+transition в одной транзакции. |
| Изменить onboarding | `POST /api/v1/onboarding/{id}/transitions` | Только из таблицы переходов; block требует reason; complete требует completedAt. |
| Создать/редактировать case | `POST /api/v1/cases`, `PATCH /api/v1/cases/{id}` | Создатель берётся из token, не request body; review fields не редактируются обычным PATCH. |
| Модерировать case | `POST /api/v1/cases/{id}/review` | reviewer role; valid review transition; reviewer/timestamp immutable snapshot. |
| Опубликовать case | `POST /api/v1/cases/{id}/publication` | Только approved+consent. |
| Создать cohort/group | `POST /api/v1/learning/cohorts`, `POST /api/v1/learning/groups` | Group cohort exists; cohort product exists. |
| Enroll client | `POST /api/v1/learning/enrollments` | order exists and содержит подходящий product; idempotency key; запрещать дубликат по order/product rules. |
| Выдать/отменить доступ | `POST /api/v1/subscriptions/{id}/platform-access-grants`, `DELETE .../{grantId}` | grant содержит фактический `grantedAt`; revoke — soft/revoked audit, не физическое удаление. |

## 8. Activity и audit

Оставить event sourcing вне scope. Нужен append-only audit trail в той же транзакции, что и write. Расширить `audit_logs` либо добавить `activity_events` с: `id`, `occurredAt`, `actorUserId`, `action`, `entityType`, `entityId`, `subjectClientId`, `correlationId`, `beforeJson`, `afterJson`, `metadataJson`, `requestTraceId`. Не логировать password hash/tokens/лишний PII.

Минимально писать: onboarding assignment/transition, access grant/revoke, case create/edit/review/publication, enrollment create/group/status/access. Для UI client activity — денормализованная безопасная projection из этих events и curator notes; audit endpoint доступен Admin только.

## 9. Роли и безопасность

| Роль | Разрешено | Ограничения |
|---|---|---|
| Admin | все команды, users/roles, audit | единственная роль для role management и полного audit. |
| Onboarding | читать clients/products/orders в нужном scope; queue; assign/transition/contact | не меняет payments, catalog, case review/publication. |
| Curator | client context/notes, cases create/edit/review/publish, learning read и enrollment operations | без financial write и user administration. |
| Finance | clients read с финансовыми данными, orders/payments read/write, subscriptions read | не видит notes/proof по умолчанию и не меняет onboarding/cases. |
| Viewer | read-only approved projections | PII и finance выдавать только при отдельном `clients.read_sensitive` scope. |

В backend использовать claims/permissions (`clients.read`, `clients.write`, `finance.read`, etc.), не только coarse `Manage*`. ToolJet получает короткоживущий JWT сервисного/пользовательского контекста, CORS ограничен origin UI, секреты — в environment/secret store. Database credentials есть только у backend/migrator; UI network policy не должна позволять соединение с PostgreSQL.

## 10. План миграции без breaking changes

1. **Контрактный фундамент (P0).** Зафиксировать OpenAPI для legacy и `/api/v1`, common error/pagination DTO, read policies; восстановить или формально сохранить два client-product legacy handlers. Миграций нет.
2. **Read layer и безопасность (P0).** Реализовать v1 projections параллельно legacy, точные date mapping, remove `AllowAnonymous` после проверки ToolJet JWT; contract/integration tests на `ffa_test`. Миграций нет или только индексы после EXPLAIN.
3. **Workflow и audit (P1).** Добавить `onboarding_cases`, transitions, version/optimistic concurrency, transactional activity/audit. Backfill создаёт case для согласованного набора eligible клиентов; dry-run с отчётом counts перед записью.
4. **Learning/access/cases (P1).** Команды access grant/revoke, строгая state machine enrollment, case review/publication split. Backfill: review status из current `Case.Status`; `Published` становится `reviewStatus=Approved`, `publicationStatus=Published` только после business validation, остальные неоднозначности — в review queue.
5. **Переключение и cleanup (P2).** Перевести ToolJet на v1 по экрану с telemetry; пометить legacy `Deprecation`/`Sunset` headers, удалить только после agreed usage window и отсутствия вызовов. Физические rename/schema cleanup — отдельная миграция.

Для каждой schema migration: тест на clean `ffa_test`, backup/restore rehearsal, idempotent backfill, row counts/checksum, API smoke tests, rollback strategy (forward-fix для уже применённой миграции). Prod не является тестовой средой.

## 11. Backlog

| Приоритет / задача | Результат | Зависимости | Риск | Готово когда |
|---|---|---|---|---|
| P0: endpoint inventory + OpenAPI baseline | Версионированный каталог legacy/v1 | владельцы ToolJet | скрытые потребители | CI diff контракта и список владельцев всех UI queries. |
| P0: вернуть/защитить client product reads | Нет регрессии двух URL | решение по локальному diff | ToolJet 404 | оба endpoint contract-tested на `ffa_test`. |
| P0: auth/read permissions | Нет anonymous PII | ToolJet token flow | сломать UI login | 401/403 tests и успешный E2E с ролью. |
| P0: common DTO/errors/date glossary | Предсказуемые ответы | API agreement | двойная семантика дат | OpenAPI examples, validation tests. |
| P1: onboarding module | SLA очередь и история | статусы/SLA owner decision | плохой backfill | migration, backfill report, transition/audit tests. |
| P1: cases split | Независимые review/publication | product rules | неверная классификация existing published | manual exception queue закрыта. |
| P1: access/enrollment commands | Факты выдачи доступа | platform rules | неполные historical dates | idempotency/concurrency/invariant tests. |
| P1: transactional audit/activity | Полная проверяемая история | middleware/application layer | partial audit | write+audit rollback test. |
| P2: telemetry/deprecation | Безопасное удаление legacy | ToolJet rollout | неучтённый consumer | 30+ дней zero calls и owner sign-off. |
| P2: read-model optimisation | Индексы/materialized views при необходимости | real query metrics | преждевременная денормализация | p95 target подтверждён EXPLAIN/monitoring. |

## 12. Решения, требующие подтверждения владельца продукта

1. Что именно делает клиента eligible для onboarding: paid order, любой order, product/package allow-list или ручное создание?
2. SLA: календарные/рабочие часы, timezone, разные SLA по продукту и кто может закрыть blocked case?
3. Означает ли нынешний `Case.Type` направление торговли или тип стратегии; нужен ли отдельный enum `tradingDirection`?
4. Нужно ли сохранять историческое `initial_budget` snapshot для каждого кейса и как backfill-ить его при отсутствии данных?
5. Может ли один order создавать несколько enrollments (по order item/product), либо один enrollment на order — намеренное правило?
6. Как выбирать `accessDate`, когда есть несколько `PlatformAccess` и enrollment platform date; нужно ли хранить access на уровне enrollment, subscription или оба?
7. Какие пользователи/роли действительно соответствуют целевым Admin/Onboarding/Curator/Finance/Viewer и какие PII/finance data доступны Viewer?
8. Подтвердить целевой UI: ToolJet или текущий Appsmith, и разрешённый migration window для legacy endpoints.
9. Правила отмены/возврата order и доступа после refund: должны ли они закрывать subscription/enrollment/onboarding автоматически?

## 13. Итог для начала реализации

Сначала утвердить разделы 4, 5, 6 и 12, восстановить совместимость client-product read endpoints и зафиксировать OpenAPI. Только затем создавать миграции `onboarding_cases` и статусов кейсов; данные production и существующие endpoints до этого не менять.
