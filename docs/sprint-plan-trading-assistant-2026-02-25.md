# Sprint Plan: TradingAssistant

**Date:** 2026-02-25
**Scrum Master:** BMAD Creative Intelligence
**Project Level:** 2 (Multi-context enterprise application)
**Total Stories:** 19
**Total Points:** 69
**Planned Sprints:** 3 (6 weeks)
**Team:** 1 senior developer, 2-week sprints, 30 pts/sprint capacity

---

## Executive Summary

This plan covers **Phase 1 (Foundation)** and **Phase 2 (Core Engagement)** from the brainstorming session. Phase 1 replaces the InMemory databases with PostgreSQL, adds JWT authentication with multi-tenant data scoping, and implements an audit trail. Phase 2 delivers high-engagement features: watchlists, paper trading, DCA automation, and a trade journal. All 19 stories are broken down to ≤5 points and allocated across 3 sprints at ~80% capacity utilization.

**Key Metrics:**
- Total Stories: 19
- Total Points: 69
- Sprints: 3
- Team Capacity: 30 points per sprint
- Target Completion: Week 6 (~2026-04-07)

---

## Story Inventory

### Epic 1: Persistent Storage (PostgreSQL) — Must Have

#### STORY-001: Set up PostgreSQL infrastructure & migrate MarketData context

**Epic:** Persistent Storage
**Priority:** Must Have
**Points:** 5

**User Story:**
As a developer
I want the MarketData context backed by PostgreSQL
So that stock and price data persists across application restarts

**Acceptance Criteria:**
- [ ] `Npgsql.EntityFrameworkCore.PostgreSQL` package added to Infrastructure project
- [ ] Connection string configured in `appsettings.json` and `appsettings.Development.json`
- [ ] `MarketDataDbContext` registration changed from `UseInMemoryDatabase` to `UseNpgsql`
- [ ] Initial EF Core migration created for MarketData context (Stocks, PriceCandles, TechnicalIndicators)
- [ ] Unique index on `Stock.Symbol` preserved in migration
- [ ] Composite index on `PriceCandle(StockId, Timestamp, Interval)` preserved
- [ ] All decimal precision annotations (18,4 for prices, 18,6 for indicators) reflected in migration
- [ ] `dotnet ef database update` succeeds against a local PostgreSQL instance

**Technical Notes:**
- 3 DbSets: Stocks, PriceCandles, TechnicalIndicators
- Consider using `HasPrecision()` explicitly since InMemory ignores precision but PostgreSQL enforces it
- Connection string format: `Host=localhost;Database=TradingAssistant_MarketData;Username=...;Password=...`

**Dependencies:** None

---

#### STORY-002: Migrate Trading & Backtest contexts to PostgreSQL

**Epic:** Persistent Storage
**Priority:** Must Have
**Points:** 5

**User Story:**
As a developer
I want all three bounded contexts using PostgreSQL
So that the entire application has durable persistence

**Acceptance Criteria:**
- [ ] `TradingDbContext` switched to `UseNpgsql` with its own connection string (or schema)
- [ ] `BacktestDbContext` switched to `UseNpgsql` with its own connection string (or schema)
- [ ] Initial migration created for TradingDb (Accounts, Orders, Positions, Portfolios, TradeExecutions)
- [ ] Initial migration created for BacktestDb (Strategies, StrategyRules, BacktestRuns, BacktestResults)
- [ ] Indexes preserved: `Order(AccountId, Status)`, `Position(AccountId, Status)`, `Portfolio(AccountId)` unique
- [ ] 1:1 relationships preserved: `Account ↔ Portfolio`, `BacktestRun ↔ BacktestResult`
- [ ] `Position.UnrealizedPnL` remains ignored by EF (computed property)
- [ ] All 3 databases can be created/migrated independently
- [ ] Application starts successfully with all 3 PostgreSQL connections

**Technical Notes:**
- Can use 3 separate databases or 3 schemas in one database
- TradingDb has 5 DbSets with complex relationships (Account → Orders, Positions, Portfolio)
- BacktestDb has 4 DbSets (Strategy → Rules, BacktestRuns; BacktestRun ↔ Result)
- Decimal precisions: 18,2 (money), 18,4 (quantities/prices), 8,4 (rates like WinRate/SharpeRatio)

**Dependencies:** STORY-001 (shared Npgsql infrastructure)

---

#### STORY-003: Convert seed data to startup initializer

**Epic:** Persistent Storage
**Priority:** Must Have
**Points:** 2

**User Story:**
As a developer
I want seed data applied automatically on startup in development
So that I don't need to manually call the seed endpoint after every database reset

**Acceptance Criteria:**
- [ ] Seed logic extracted into an `IHostedService` or startup delegate
- [ ] Seeds 8 stocks, 90 days of candles, default account + portfolio (same as current `SeedMarketDataHandler`)
- [ ] Idempotent — skips seeding if data already exists
- [ ] Only runs in Development environment (`IHostEnvironment.IsDevelopment()`)
- [ ] Existing `POST /api/market-data/seed` endpoint remains for manual use
- [ ] Application starts and data is queryable without manual seeding

**Technical Notes:**
- Current seed logic is in `SeedMarketDataHandler` — reuse the same code
- Use `IServiceScopeFactory` to create a scope for the scoped DbContexts
- Run after `app.Build()` but before `app.Run()`, or use `IHostedService`

**Dependencies:** STORY-001, STORY-002

---

### Epic 2: Authentication & RBAC — Must Have

#### STORY-004: User entity & registration endpoint

**Epic:** Authentication
**Priority:** Must Have
**Points:** 5

**User Story:**
As a new user
I want to register an account with my email and password
So that I can access the trading platform securely

**Acceptance Criteria:**
- [ ] `User` entity created: Id (Guid), Email (unique), PasswordHash, Role (string), CreatedAt
- [ ] User entity added to a new `IdentityDbContext` or existing context (design decision)
- [ ] `POST /api/auth/register` endpoint accepting `{ email, password }`
- [ ] Passwords hashed with BCrypt (or ASP.NET Identity's default hasher)
- [ ] Email uniqueness validated — returns 409 Conflict if duplicate
- [ ] Password strength validation (minimum 8 chars, at least 1 number)
- [ ] Returns user ID and email on successful registration
- [ ] A default trading account + portfolio created for the new user
- [ ] FluentValidation validator for the registration command

**Technical Notes:**
- Decision: lightweight custom auth (simpler) vs ASP.NET Identity (more features)
- User → Account is 1:many (user can have multiple accounts later)
- Registration handler should cascade: create user → create account → create portfolio

**Dependencies:** STORY-001, STORY-002 (needs PostgreSQL)

---

#### STORY-005: JWT login & token generation

**Epic:** Authentication
**Priority:** Must Have
**Points:** 3

**User Story:**
As a registered user
I want to log in with my credentials and receive a JWT token
So that I can authenticate subsequent API calls

**Acceptance Criteria:**
- [ ] `POST /api/auth/login` endpoint accepting `{ email, password }`
- [ ] Password verified against stored hash
- [ ] Returns 401 Unauthorized for invalid credentials
- [ ] JWT token generated with claims: `sub` (userId), `email`, `role`
- [ ] Token expiration configurable via `appsettings.json` (default: 60 minutes)
- [ ] JWT signing key configured in `appsettings.json`
- [ ] `AddAuthentication().AddJwtBearer()` registered in DI
- [ ] Token returned as `{ token, expiresAt }`

**Technical Notes:**
- Use `Microsoft.AspNetCore.Authentication.JwtBearer` package
- JWT config in appsettings: `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiresInMinutes`
- Wolverine handlers can access `ClaimsPrincipal` via `HttpContext` or custom middleware

**Dependencies:** STORY-004 (needs User entity)

---

#### STORY-006: Auth middleware & endpoint protection

**Epic:** Authentication
**Priority:** Must Have
**Points:** 4

**User Story:**
As the system
I want to protect API endpoints with authentication
So that only authenticated users can access trading operations

**Acceptance Criteria:**
- [ ] `app.UseAuthentication()` and `app.UseAuthorization()` added to pipeline
- [ ] All existing write endpoints require authentication (POST/PUT/DELETE)
- [ ] All existing read endpoints require authentication (GET)
- [ ] `POST /api/auth/register` and `POST /api/auth/login` remain anonymous
- [ ] `POST /api/market-data/seed` remains anonymous (dev convenience)
- [ ] Unauthenticated requests return 401 with ProblemDetails
- [ ] Invalid/expired tokens return 401 with ProblemDetails
- [ ] User ID extractable from JWT claims in handlers

**Technical Notes:**
- Wolverine HTTP supports `[Authorize]` attribute on endpoint classes
- May need a custom Wolverine middleware to extract userId from `ClaimsPrincipal`
- Consider adding a `ICurrentUser` service that reads from `HttpContext.User`

**Dependencies:** STORY-005 (needs JWT setup)

---

#### STORY-007: Account-user association & multi-tenant data scoping

**Epic:** Authentication
**Priority:** Must Have
**Points:** 5

**User Story:**
As an authenticated user
I want to see only my own data (accounts, orders, positions)
So that my trading data is private and secure

**Acceptance Criteria:**
- [ ] `Account` entity gets a `UserId` (Guid) foreign key
- [ ] Migration adds `UserId` column to Accounts table
- [ ] All trading query handlers filter by authenticated user's accounts
- [ ] `GetPortfolioHandler` scoped to current user's accounts
- [ ] `GetOrderHistoryHandler` scoped to current user's accounts
- [ ] `GetPositionsHandler` scoped to current user's accounts
- [ ] `PlaceOrderCommand` validates the account belongs to the current user
- [ ] Attempting to access another user's data returns 403 Forbidden
- [ ] Seed data handler associates default account with a system/dev user

**Technical Notes:**
- Use the `ICurrentUser` service from STORY-006 to get userId in handlers
- Consider a query filter on DbContext: `HasQueryFilter(a => a.UserId == currentUserId)`
- All handlers that take `accountId` need user ownership validation

**Dependencies:** STORY-004, STORY-006 (needs User entity and auth middleware)

---

### Epic 3: Audit Trail — Should Have

#### STORY-008: Audit log entity & SaveChanges interceptor

**Epic:** Audit Trail
**Priority:** Should Have
**Points:** 5

**User Story:**
As a compliance officer
I want all data changes automatically logged
So that there is a full audit trail of trading activity

**Acceptance Criteria:**
- [ ] `AuditLog` entity: Id, EntityType (string), EntityId (string), Action (Created/Updated/Deleted), OldValues (JSON), NewValues (JSON), UserId (Guid?), Timestamp
- [ ] EF Core `SaveChangesInterceptor` captures all Added/Modified/Deleted entities
- [ ] Interceptor registered on TradingDbContext (most critical for trading ops)
- [ ] Old and new property values serialized as JSON
- [ ] UserId populated from `ICurrentUser` service (null for system operations)
- [ ] Audit logs stored in a dedicated table (not a separate database)
- [ ] Order creation, fill, cancel all generate audit entries
- [ ] Position open/close generates audit entries

**Technical Notes:**
- Use `DbContext.ChangeTracker.Entries()` in `SaveChangesInterceptor`
- For "old values" of modified entities, use `OriginalValues`
- Consider whether to audit all contexts or just TradingDb (start with TradingDb)

**Dependencies:** STORY-002 (needs PostgreSQL for persistence)

---

#### STORY-009: Audit log query endpoint

**Epic:** Audit Trail
**Priority:** Should Have
**Points:** 2

**User Story:**
As an administrator
I want to query audit logs by entity, action, or date range
So that I can investigate trading activity

**Acceptance Criteria:**
- [ ] `GET /api/audit-logs` endpoint with query parameters: entityType, entityId, action, startDate, endDate
- [ ] Paged response using existing `PagedResponse<T>`
- [ ] Results ordered by Timestamp descending
- [ ] Endpoint restricted to authenticated users (future: admin role only)

**Technical Notes:**
- Simple query handler, straightforward filtering
- Use `AuditLogDto` record for the response

**Dependencies:** STORY-008 (needs audit log entity)

---

### Epic 4: Watchlists — Should Have

#### STORY-010: Watchlist entity & CRUD endpoints

**Epic:** Watchlists
**Priority:** Should Have
**Points:** 3

**User Story:**
As a trader
I want to create and manage watchlists of stocks I'm interested in
So that I can quickly monitor the symbols I care about

**Acceptance Criteria:**
- [ ] `Watchlist` entity: Id, UserId, Name (max 100), CreatedAt
- [ ] `WatchlistItem` entity: Id, WatchlistId, Symbol (max 10), AddedAt
- [ ] Entities added to MarketDataDbContext
- [ ] `POST /api/watchlists` — create watchlist with name
- [ ] `GET /api/watchlists` — list user's watchlists
- [ ] `POST /api/watchlists/{id}/items` — add symbol to watchlist
- [ ] `DELETE /api/watchlists/{id}/items/{symbol}` — remove symbol
- [ ] `DELETE /api/watchlists/{id}` — delete watchlist (cascade items)
- [ ] Validates symbol exists in Stocks table before adding
- [ ] Scoped to authenticated user

**Technical Notes:**
- Simple CRUD — MarketData context
- Watchlist scoped by UserId
- Max items per watchlist: consider a reasonable limit (e.g., 50)

**Dependencies:** STORY-006 (needs auth), STORY-001 (needs PostgreSQL)

---

#### STORY-011: Watchlist price summary endpoint

**Epic:** Watchlists
**Priority:** Should Have
**Points:** 3

**User Story:**
As a trader
I want to see current prices for all stocks in my watchlist at a glance
So that I can quickly assess market movements for my watched symbols

**Acceptance Criteria:**
- [ ] `GET /api/watchlists/{id}/prices` — returns all items with current price data
- [ ] Uses `StockPriceCache.Get(HashSet<string>)` for batch price lookup (single cache call)
- [ ] Response includes: symbol, name, currentPrice, change, changePercent for each item
- [ ] Returns empty list if watchlist has no items
- [ ] Validates watchlist belongs to authenticated user

**Technical Notes:**
- Leverage the batch `Get(HashSet<TKey>)` on `StockPriceCache` — this is exactly what the CacheRepository's fetch coalescing is designed for
- Single cache call for all symbols in the watchlist = efficient

**Dependencies:** STORY-010 (needs watchlist entity), StockPriceCache (already implemented)

---

### Epic 5: Paper Trading — Must Have

#### STORY-012: Paper account type & creation flow

**Epic:** Paper Trading
**Priority:** Must Have
**Points:** 3

**User Story:**
As a trader
I want to create a paper trading account
So that I can practice strategies without risking real money

**Acceptance Criteria:**
- [ ] `AccountType` enum added: `Live`, `Paper`
- [ ] `Account` entity gets `AccountType` property (defaults to `Live`)
- [ ] `POST /api/trading/accounts/paper` — create paper account with configurable starting balance
- [ ] Paper account starts with specified balance (default: $100,000) and empty portfolio
- [ ] Paper accounts clearly distinguishable from live accounts in all responses
- [ ] `AccountDto` updated to include `AccountType` field
- [ ] Migration adds `AccountType` column to Accounts table
- [ ] Existing accounts default to `Live`

**Technical Notes:**
- Minimal domain change — just an enum and a column
- Paper and live accounts share the same entities and DbContext
- Differentiation happens in the order execution flow (STORY-013)

**Dependencies:** STORY-002 (needs PostgreSQL), STORY-007 (needs user-account association)

---

#### STORY-013: Paper order execution flow

**Epic:** Paper Trading
**Priority:** Must Have
**Points:** 5

**User Story:**
As a paper trader
I want my orders to be simulated instantly at current market price
So that I can test strategies with realistic but risk-free execution

**Acceptance Criteria:**
- [ ] Orders placed on paper accounts are filled immediately at current market price
- [ ] Paper fills use `StockPriceCache` for current price (no real exchange interaction)
- [ ] Position created/updated on fill (same as live flow)
- [ ] Portfolio balances updated on fill (same as live flow)
- [ ] Trade execution record created with `Fee = 0` for paper trades
- [ ] Existing `PlaceOrderCommand` flow works for both live and paper — handler branches based on `AccountType`
- [ ] Event cascading preserved: `OrderPlaced → FillOrderHandler → OrderFilled`
- [ ] Paper orders visible in order history with `AccountType = Paper` context

**Technical Notes:**
- Key decision: branch in existing `FillOrderHandler` vs separate `PaperFillOrderHandler`
- Recommend: check `AccountType` in existing fill handler — simpler, less code duplication
- Paper fills should be instantaneous (no simulated delay)

**Dependencies:** STORY-012 (needs paper account type)

---

#### STORY-014: Paper portfolio tracking

**Epic:** Paper Trading
**Priority:** Must Have
**Points:** 3

**User Story:**
As a paper trader
I want to see my paper portfolio's performance separately from live accounts
So that I can evaluate my strategy's effectiveness

**Acceptance Criteria:**
- [ ] `GET /api/trading/portfolio/{accountId}` works for paper accounts (already does via existing handler)
- [ ] `GET /api/trading/positions/{accountId}` shows paper positions
- [ ] `GET /api/trading/orders/{accountId}` shows paper order history
- [ ] Portfolio summary clearly labeled as paper in response
- [ ] Paper portfolio P&L calculated the same way as live
- [ ] Close position endpoint works for paper positions
- [ ] PortfolioCache caches paper portfolios separately (different accountId = different cache key)

**Technical Notes:**
- Most of this works already — same handlers, same cache keys (by accountId)
- Main work: ensure labels, add `AccountType` to response DTOs
- Consider: endpoint to reset paper account to starting balance

**Dependencies:** STORY-012, STORY-013

---

### Epic 6: DCA Automation — Should Have

#### STORY-015: DCA plan entity & creation endpoint

**Epic:** DCA Automation
**Priority:** Should Have
**Points:** 3

**User Story:**
As a trader
I want to create a Dollar-Cost Averaging plan for a stock
So that I can invest a fixed amount at regular intervals automatically

**Acceptance Criteria:**
- [ ] `DcaPlan` entity: Id, AccountId, Symbol, Amount (decimal), Frequency (enum: Daily/Weekly/Biweekly/Monthly), NextExecutionDate, IsActive (bool), CreatedAt
- [ ] Entity added to TradingDbContext
- [ ] `POST /api/trading/dca-plans` — create DCA plan
- [ ] `GET /api/trading/dca-plans/{accountId}` — list plans for account
- [ ] Validates symbol exists, account belongs to user, amount > 0
- [ ] Plan created with `IsActive = true` and `NextExecutionDate` calculated from frequency
- [ ] FluentValidation validator for the command

**Technical Notes:**
- `DcaFrequency` enum: Daily, Weekly, Biweekly, Monthly
- `NextExecutionDate` calculated: e.g., Weekly → next Monday, Monthly → same day next month
- DCA plan is a "schedule definition" — execution happens in STORY-016

**Dependencies:** STORY-002, STORY-007 (needs PostgreSQL + user scoping)

---

#### STORY-016: Wolverine scheduled recurring order execution

**Epic:** DCA Automation
**Priority:** Should Have
**Points:** 5

**User Story:**
As the system
I want to automatically execute DCA plans at their scheduled times
So that users' recurring investments happen without manual intervention

**Acceptance Criteria:**
- [ ] Wolverine scheduled message triggers `ExecuteDcaPlanCommand` at `NextExecutionDate`
- [ ] Handler places a market order via existing `PlaceOrderCommand` flow
- [ ] On successful execution: `NextExecutionDate` updated to next interval, `DcaExecution` record created
- [ ] On insufficient funds: DCA plan paused with reason, user notified (log for now)
- [ ] On stock not found: DCA plan deactivated with error reason
- [ ] Scheduling initiated when DCA plan is created (STORY-015)
- [ ] Scheduling cancelled when DCA plan is deactivated
- [ ] Works for both live and paper accounts

**Technical Notes:**
- Use Wolverine's `ScheduleAsync<T>(message, scheduledTime)` for durable scheduling
- After each execution, schedule the next one (self-rescheduling pattern)
- `DcaExecution` entity: Id, DcaPlanId, OrderId, ExecutedAt, Amount, Status
- Wolverine's durable outbox ensures scheduled messages survive restarts (requires PostgreSQL transport or Marten — evaluate)

**Dependencies:** STORY-015 (needs DCA plan entity), STORY-001/002 (needs durable storage for scheduled messages)

---

#### STORY-017: DCA plan management endpoints

**Epic:** DCA Automation
**Priority:** Should Have
**Points:** 3

**User Story:**
As a trader
I want to pause, resume, or cancel my DCA plans
So that I have full control over my automated investments

**Acceptance Criteria:**
- [ ] `POST /api/trading/dca-plans/{id}/pause` — pause active plan (cancels next scheduled execution)
- [ ] `POST /api/trading/dca-plans/{id}/resume` — resume paused plan (reschedules next execution)
- [ ] `DELETE /api/trading/dca-plans/{id}` — cancel plan permanently
- [ ] `GET /api/trading/dca-plans/{id}/executions` — list execution history for a plan
- [ ] All endpoints validate plan belongs to authenticated user's account
- [ ] Paused plans show status = "Paused" in list response
- [ ] Resume recalculates `NextExecutionDate` from current date

**Technical Notes:**
- Pause = set `IsActive = false` + cancel Wolverine scheduled message
- Resume = set `IsActive = true` + schedule new Wolverine message
- Cancel = soft delete or hard delete + cancel scheduled message

**Dependencies:** STORY-015, STORY-016

---

### Epic 7: Trade Journal — Could Have

#### STORY-018: Trade note entity & CRUD endpoints

**Epic:** Trade Journal
**Priority:** Could Have
**Points:** 3

**User Story:**
As a trader
I want to attach notes to my trades
So that I can document my reasoning and learn from past decisions

**Acceptance Criteria:**
- [ ] `TradeNote` entity: Id, UserId, OrderId (Guid?), PositionId (Guid?), Content (max 2000), CreatedAt, UpdatedAt
- [ ] Entity added to TradingDbContext
- [ ] `POST /api/trading/notes` — create note linked to order or position
- [ ] `GET /api/trading/notes?orderId=X` or `?positionId=X` — get notes for a trade
- [ ] `PUT /api/trading/notes/{id}` — update note content
- [ ] `DELETE /api/trading/notes/{id}` — delete note
- [ ] Notes scoped to authenticated user
- [ ] Validates referenced order/position exists and belongs to user

**Technical Notes:**
- Either `OrderId` or `PositionId` should be set (not both, not neither) — or allow general notes too
- Simple CRUD, no complex logic

**Dependencies:** STORY-007 (needs user scoping)

---

#### STORY-019: Trade note tagging & filtering

**Epic:** Trade Journal
**Priority:** Could Have
**Points:** 2

**User Story:**
As a trader
I want to tag my notes and filter by tags
So that I can categorize and find my trading insights easily

**Acceptance Criteria:**
- [ ] `Tags` property added to `TradeNote` (stored as comma-separated string or JSON array)
- [ ] Tags provided on note creation and update
- [ ] `GET /api/trading/notes?tag=strategy` — filter notes by tag
- [ ] `GET /api/trading/notes?startDate=X&endDate=Y` — filter by date range
- [ ] `GET /api/trading/notes` — list all notes for user (paged)
- [ ] Tags returned in response DTOs

**Technical Notes:**
- Simple approach: store tags as JSON array in a string column
- Filter with EF Core `Contains` or raw SQL `LIKE` for PostgreSQL
- Consider: separate `Tag` entity for normalization (overkill for v1)

**Dependencies:** STORY-018

---

## Sprint Allocation

### Sprint 1 (Weeks 1–2): "Database Foundation & Authentication" — 24/30 pts

**Goal:** Replace InMemory databases with PostgreSQL and establish JWT authentication, so the platform has durable storage and secure access.

| Story | Title | Points | Priority | Epic |
|-------|-------|--------|----------|------|
| STORY-001 | PostgreSQL infra & MarketData migration | 5 | Must Have | Persistent Storage |
| STORY-002 | Trading & Backtest context migrations | 5 | Must Have | Persistent Storage |
| STORY-003 | Seed data startup initializer | 2 | Must Have | Persistent Storage |
| STORY-004 | User entity & registration endpoint | 5 | Must Have | Authentication |
| STORY-005 | JWT login & token generation | 3 | Must Have | Authentication |
| STORY-006 | Auth middleware & endpoint protection | 4 | Must Have | Authentication |

**Sprint Total:** 24 points (80% utilization — 20% buffer for migration issues)

**Risks:**
- PostgreSQL precision/index differences from InMemory may surface unexpected issues
- JWT + Wolverine HTTP integration may need custom middleware

**Definition of Done:**
- All 3 databases running on PostgreSQL with data persisted across restarts
- Users can register, log in, and receive JWT tokens
- All endpoints (except auth + seed) reject unauthenticated requests

---

### Sprint 2 (Weeks 3–4): "Multi-Tenancy, Audit & Quick Wins" — 25/30 pts

**Goal:** Lock down data to per-user access, add an audit trail for compliance, and deliver quick-win engagement features (watchlists + trade journal).

| Story | Title | Points | Priority | Epic |
|-------|-------|--------|----------|------|
| STORY-007 | Account-user association & data scoping | 5 | Must Have | Authentication |
| STORY-008 | Audit log entity & SaveChanges interceptor | 5 | Should Have | Audit Trail |
| STORY-009 | Audit log query endpoint | 2 | Should Have | Audit Trail |
| STORY-010 | Watchlist entity & CRUD endpoints | 3 | Should Have | Watchlists |
| STORY-011 | Watchlist price summary endpoint | 3 | Should Have | Watchlists |
| STORY-018 | Trade note entity & CRUD endpoints | 3 | Could Have | Trade Journal |
| STORY-019 | Trade note tagging & filtering | 2 | Could Have | Trade Journal |

**Sprint Total:** 23 points (77% utilization — buffer for Sprint 1 carryover)

**Note:** Trade Journal stories (018, 019) pulled forward since they're simple CRUD with low risk. If Sprint 1 has carryover, these can be deprioritized.

**Risks:**
- Multi-tenant query filters may affect existing handler tests
- Audit interceptor needs careful handling of circular references in JSON serialization

**Definition of Done:**
- Users can only see their own accounts, orders, and positions
- All TradingDb changes are audit-logged with old/new values
- Users can create watchlists and see batch-cached prices
- Users can annotate trades with tagged notes

---

### Sprint 3 (Weeks 5–6): "Paper Trading & DCA Automation" — 22/30 pts

**Goal:** Deliver the two highest-engagement features: risk-free paper trading and automated recurring investments.

| Story | Title | Points | Priority | Epic |
|-------|-------|--------|----------|------|
| STORY-012 | Paper account type & creation flow | 3 | Must Have | Paper Trading |
| STORY-013 | Paper order execution flow | 5 | Must Have | Paper Trading |
| STORY-014 | Paper portfolio tracking | 3 | Must Have | Paper Trading |
| STORY-015 | DCA plan entity & creation endpoint | 3 | Should Have | DCA Automation |
| STORY-016 | Wolverine scheduled execution | 5 | Should Have | DCA Automation |
| STORY-017 | DCA plan management | 3 | Should Have | DCA Automation |

**Sprint Total:** 22 points (73% utilization — buffer for Sprint 1–2 fixes and polish)

**Risks:**
- Wolverine durable scheduling may require additional transport config (PostgreSQL transport or Marten)
- DCA self-rescheduling pattern needs careful error handling to avoid infinite retries

**Definition of Done:**
- Users can create paper accounts and trade with simulated fills
- Paper portfolio tracks P&L identically to live
- DCA plans execute automatically at scheduled intervals
- Users can pause, resume, and cancel DCA plans

---

## Epic Traceability

| Epic | Stories | Total Points | Sprint(s) |
|------|---------|-------------|-----------|
| Persistent Storage | STORY-001, 002, 003 | 12 | Sprint 1 |
| Authentication & RBAC | STORY-004, 005, 006, 007 | 17 | Sprint 1–2 |
| Audit Trail | STORY-008, 009 | 7 | Sprint 2 |
| Watchlists | STORY-010, 011 | 6 | Sprint 2 |
| Paper Trading | STORY-012, 013, 014 | 11 | Sprint 3 |
| DCA Automation | STORY-015, 016, 017 | 11 | Sprint 3 |
| Trade Journal | STORY-018, 019 | 5 | Sprint 2 |

---

## Dependency Graph

```
STORY-001 (PostgreSQL MarketData)
    ├── STORY-002 (Trading & Backtest migrations)
    │       └── STORY-003 (Seed data initializer)
    ├── STORY-004 (User registration)
    │       └── STORY-005 (JWT login)
    │               └── STORY-006 (Auth middleware)
    │                       └── STORY-007 (Multi-tenant scoping)
    │                               ├── STORY-012 (Paper account type)
    │                               │       └── STORY-013 (Paper execution)
    │                               │               └── STORY-014 (Paper portfolio)
    │                               ├── STORY-015 (DCA plan entity)
    │                               │       └── STORY-016 (Scheduled execution)
    │                               │               └── STORY-017 (DCA management)
    │                               └── STORY-018 (Trade notes)
    │                                       └── STORY-019 (Note tagging)
    └── STORY-010 (Watchlist CRUD)
            └── STORY-011 (Watchlist prices)

STORY-002 ──► STORY-008 (Audit interceptor)
                    └── STORY-009 (Audit endpoint)
```

---

## Risks and Mitigation

**High:**
- **PostgreSQL migration precision mismatches** — InMemory ignores precision annotations; PostgreSQL enforces them. Mitigation: Validate all decimal columns in migration SQL, run integration tests early in Sprint 1.
- **Wolverine durable scheduling requires transport** — `ScheduleAsync` may need Marten or a message transport for durability. Mitigation: Research Wolverine PostgreSQL transport in Sprint 1; fall back to `Hangfire` or cron-based if needed.

**Medium:**
- **Multi-tenant query filters breaking existing handlers** — Global query filters can silently affect all queries. Mitigation: Add `IgnoreQueryFilters()` where system-level access is needed (e.g., seed handler).
- **Audit log JSON serialization of navigation properties** — EF entities with circular nav properties will fail JSON serialization. Mitigation: Only serialize scalar properties in audit entries.

**Low:**
- **JWT token refresh** — No refresh token planned. Mitigation: Use generous expiration (60 min) for v1; add refresh tokens in a future sprint.

---

## Definition of Done (Global)

For a story to be considered complete:
- [ ] Code implemented and committed
- [ ] Unit tests written and passing
- [ ] Handler logic tested with integration test (where applicable)
- [ ] FluentValidation validators added for new commands
- [ ] EF migrations created and tested
- [ ] Endpoints documented in README or API spec
- [ ] Acceptance criteria validated manually

---

## Statistics

- **Total Ideas (from brainstorm):** 27
- **Selected for planning:** 7 epics (Phase 1 + Phase 2)
- **Total stories:** 19
- **Total points:** 69
- **Sprints:** 3 (6 weeks)
- **Avg points/sprint:** 23 (77% utilization)
- **Must Have:** 42 points (61%)
- **Should Have:** 22 points (32%)
- **Could Have:** 5 points (7%)

---

## Next Steps

**Immediate:** Begin Sprint 1

1. Run `/bmad:dev-story STORY-001` to start implementing PostgreSQL migration
2. Set up local PostgreSQL instance (Docker recommended: `docker run -p 5432:5432 -e POSTGRES_PASSWORD=dev postgres:16`)
3. Stories should be implemented in dependency order: 001 → 002 → 003 → 004 → 005 → 006

**Sprint cadence:**
- Sprint 1: Weeks 1–2 (target: ~2026-03-10)
- Sprint 2: Weeks 3–4 (target: ~2026-03-24)
- Sprint 3: Weeks 5–6 (target: ~2026-04-07)

---

*This plan was created using BMAD Method v6 — Phase 4 (Implementation Planning)*
