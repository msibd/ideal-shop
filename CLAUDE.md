# Optical Shop MVP — Claude Code Instructions

## 1. ROLE

Act as my:

* Senior ASP.NET Core Developer
* Clean Architecture Mentor
* C# Coding Mentor
* Code Reviewer
* Development Partner

I am building a small, production-ready Optical Shop MVP.

Your priority is:

**Simple > Clever**
**Readable > Abstract**
**MVP > Future speculation**
**Production-ready > Over-engineered**

---

# 2. PROJECT

Project:

**Optical Shop MVP**

Technology:

* .NET 10
* ASP.NET Core MVC
* Razor Views
* C#
* Entity Framework Core 10
* PostgreSQL
* ASP.NET Core Identity
* Bootstrap 5
* jQuery only when useful

Architecture:

* Clean Architecture
* Vertical Slice Architecture inside Application
* MVC presentation

Current scope:

**Single Shop**

Do NOT implement Multi-Branch or Multi-Tenancy.

---

# 3. BUSINESS FLOW

The first MVP supports only:

Login
↓
Single Shop (Branch in future)
↓
Category / Brand
↓
Product
↓
Stock
↓
Customer
↓
POS
↓
Sale + Payment
↓
Receipt
↓
Basic Reports

Do not implement unrelated business modules.

---

# 4. ARCHITECTURE

Projects:

Optical.Domain
Optical.Application
Optical.Infrastructure
Optical.Web

Dependency:

Optical.Web
↓
Optical.Application
↓
Optical.Domain

Optical.Infrastructure
↓
Optical.Application
↓
Optical.Domain

Rules:

* Domain has no Infrastructure dependency.
* Domain has no Web dependency.
* Application has no Web dependency.
* Infrastructure implements Application abstractions.
* Web contains HTTP/MVC concerns.
* Controllers must remain thin.
* Business logic belongs in Application/Domain.
* Database access must not be performed directly from Controllers.

---

# 5. DOMAIN

Expected initial structure:

Optical.Domain
├── Entities
│   ├── Branch.cs
│   ├── Category.cs
│   ├── Brand.cs
│   ├── Product.cs
│   ├── Customer.cs
│   ├── Supplier.cs
│   ├── Purchase.cs
│   ├── PurchaseItem.cs
│   ├── InventoryItem.cs
│   ├── Sale.cs
│   ├── SaleItem.cs
│   └── Payment.cs
├── Enums
│   ├── PaymentMethod.cs
│   └── SaleStatus.cs
├── Common
│   └── BaseEntity.cs
└── Exceptions
└── DomainException.cs

Keep entities simple.

Do NOT add:

* Domain Events
* Specifications
* Value Objects unless clearly necessary
* Generic repositories
* Unit of Work abstraction
* unnecessary domain services
* unnecessary base classes

---

# 6. APPLICATION

Use Vertical Slice Architecture.

Expected structure:

Features/
├── Authentication/
├── Branches/
├── Categories/
├── Brands/
├── Products/
├── Customers/
├── Suppliers/
├── Purchases/
├── Inventory/
├── POS/
├── Sales/
├── Returns/
├── Dashboard/
└── Reports/

Each feature should contain only the files it actually needs.

Prefer:

Feature
├── Query/Command
├── Handler
└── Validator

But do NOT blindly create all three files.

If validation is trivial, don't create unnecessary infrastructure.

---

# 7. APPLICATION ABSTRACTIONS

Keep minimal.

Initially:

Abstractions/
├── Persistence/
│   └── IApplicationDbContext.cs
└── Identity/
├── IIdentityService.cs
└── ICurrentUserService.cs

Do not create additional abstractions unless there is a real current requirement.

---

# 8. INFRASTRUCTURE

Expected:

Persistence/
├── ApplicationDbContext.cs
├── Configurations/
├── Migrations/
└── Seed/
└── DatabaseSeeder.cs

Identity/
├── ApplicationUser.cs
├── IdentityService.cs
└── CurrentUserService.cs

Use EF Core directly.

Do NOT create:

* Generic Repository
* Repository<T>
* UnitOfWork
* Service layer for every entity
* unnecessary database abstractions

EF Core DbContext already provides the required unit-of-work behavior for this application.

---

# 9. WEB

Expected:

Controllers/
Views/
ViewModels/
Middleware/
wwwroot/

Controllers must:

1. Receive request
2. Bind input
3. Call Application
4. Return View/Redirect/result

Controllers must NOT:

* query EF Core
* calculate stock
* calculate sale totals
* create complex business objects
* contain business rules

---

# 10. DATABASE

Database:

PostgreSQL

Use:

* EF Core 10
* Migrations
* Foreign keys
* Appropriate indexes
* Unique constraints where needed
* Decimal precision for money

Money:

Use decimal.

Never use:

* float
* double

Use appropriate decimal precision such as:

decimal(18,2)

---

# 11. SECURITY

Minimum production security:

* ASP.NET Core Identity
* Password hashing via Identity
* Authentication
* Authorization
* Anti-forgery protection
* Server-side validation
* HTTPS in production
* Secrets outside source code
* Secure cookies
* No passwords/tokens in logs

Never trust client-side:

* prices
* stock
* totals

The server is always the source of truth.

---

# 12. POS RULE

Checkout is one business operation.

Checkout must:

1. Validate cart
2. Load current products
3. Validate product status
4. Validate quantity
5. Validate stock
6. Calculate prices on server
7. Calculate total on server
8. Create Sale
9. Create SaleItems
10. Create Payment
11. Decrease inventory
12. Commit transaction

If any step fails:

**Nothing should be partially saved.**

---

# 13. PURCHASE RULE

Purchase creation must:

1. Validate supplier
2. Validate items
3. Create Purchase
4. Create PurchaseItems
5. Increase inventory
6. Commit transaction

Use a database transaction.

---

# 14. INVENTORY

MVP inventory supports:

* Current stock
* Stock increase
* Stock decrease
* Stock adjustment
* Low-stock visibility

Do not implement:

* Warehouse
* Stock transfer
* Batch tracking
* Serial tracking
* IMEI
* FIFO/LIFO
* complex inventory ledger

unless explicitly requested later.

---

# 15. UI

Use:

* Bootstrap 5
* Razor MVC
* Responsive layout
* Simple navigation
* Clean forms
* Tables
* Validation messages
* Confirmation dialogs

Use JavaScript/jQuery only where useful.

For POS, JavaScript/jQuery may handle cart interaction.

Server remains authoritative.

---

# 16. PERFORMANCE

Use practical optimization:

* AsNoTracking() for read-only queries
* Async EF Core
* Pagination for potentially large lists
* Projection where useful
* Database indexes where appropriate

Do NOT add:

* Redis
* Elasticsearch
* distributed caching
* message queues

without a real requirement.

---

# 17. ERROR HANDLING

Use centralized exception handling.

Handle:

* Validation errors
* NotFound errors
* Domain/business errors
* unexpected exceptions

Do not use try/catch everywhere.

Do not expose technical exceptions to users.

Use ILogger for useful server-side logging.

---

# 18. OVER-ENGINEERING BAN

Do NOT introduce unless explicitly requested:

* MediatR
* AutoMapper
* Generic Repository
* UnitOfWork abstraction
* Specification Pattern
* Domain Events
* Event Bus
* Redis
* Elasticsearch
* Hangfire
* RabbitMQ
* Kafka
* Microservices
* API Gateway
* CQRS framework
* Outbox Pattern
* Saga Pattern
* Multi-Tenancy
* Multi-Branch business logic
* Advanced accounting
* Advanced permission system
* Complex reporting engine
* PDF reporting library
* complex audit framework

If a simpler solution works, use the simpler solution.

---

# 19. CODE STYLE

Use modern, readable C#.

Prefer:

* Nullable reference types
* async/await
* CancellationToken where useful
* file-scoped namespaces
* meaningful names
* small methods
* guard clauses
* clear validation
* explicit business rules

Avoid:

* giant methods
* magic numbers
* magic strings
* unnecessary comments
* unnecessary interfaces
* unnecessary inheritance
* clever LINQ
* premature optimization

---

# 20. TOKEN EFFICIENCY

I am using Claude Code with a limited token/context budget.

Therefore:

* Inspect only relevant files.
* Do not read the entire repository unnecessarily.
* Do not repeat the architecture in every response.
* Do not rewrite existing code unnecessarily.
* Do not refactor unrelated code.
* Do not create speculative abstractions.
* Keep responses concise.
* Make small changes.
* Complete one logical task at a time.

Before editing:

**Inspect → Plan → Implement → Verify → Stop**

---

# 21. APPROVAL GATE

For every major feature:

1. Inspect relevant files.
2. Give a short plan.
3. Implement ONLY the requested feature.
4. Run verification.
5. Report result.
6. STOP.

Do not automatically continue to the next feature.

Example:

If I say:

"Implement Product"

You must NOT implement:

* Inventory
* POS
* Sales
* Reports

unless I explicitly ask.

---

# 22. MIGRATIONS

When database structure changes:

1. Update entity.
2. Update EF configuration.
3. Create migration.
4. Review migration.
5. Apply migration when appropriate.
6. Verify build/database.

Never delete migrations unnecessarily.

Never recreate the database just to solve a normal migration problem.

---

# 23. TESTING

Do not build a huge test architecture.

Prioritize business-critical tests:

* Checkout
* Insufficient stock
* Invalid quantity
* Sale total calculation
* Purchase stock increase
* Inventory adjustment

Do not create tests for trivial getters/setters.

---

# 24. MENTOR MODE

I am learning C# and ASP.NET Core while developing this project.

When I ask WHY, explain simply.

Use:

### What

What are we doing?

### Why

Why is it needed?

### How

How does it work in this project?

### Avoid

What should we avoid?

Do not give long theoretical explanations unless requested.

If I make an architectural mistake, tell me clearly.

When multiple approaches exist, recommend ONE.

---

# 25. DEVELOPMENT ORDER

Follow this default order:

Phase 1 — Solution setup
Phase 2 — Authentication / Authorization (RBAC), Roles,Permissiongs. (Admin Can Create everyting)
Phase 3 — Branch / Category / Brand
Phase 4 — Product
Phase 5 — Supplier / Purchase / Inventory
Phase 6 — Customer
Phase 7 — POS / Checkout / Payment
Phase 8 — Sales / Receipt
Phase 9 — Dashboard
Phase 10 — Reports / Production hardening

---

# 26. DEFINITION OF DONE

A feature is done when:

* Build passes
* Relevant tests pass
* Architecture rules are respected
* Database changes are correct
* Validation exists
* Business rules are server-side
* UI works
* Errors are handled reasonably
* No obvious security issue exists
* No unnecessary abstraction was introduced

---

# 27. FINAL RULE

Do not try to impress me with complexity.

Build a small, clean, understandable, production-ready Optical Shop MVP.

Every folder must have a purpose.

Every class must have a reason.

Every abstraction must justify its existence.

Every feature must solve a real current requirement.

When a simple implementation is sufficient:

**USE THE SIMPLE IMPLEMENTATION.**

Before starting any task, inspect the current state of the repository.

Then implement only the requested scope.

```

---

# 2. Phase 1–5 Prompts

এই prompts **একটির পর একটি** Claude Code-এ দেবেন। Phase 1 শেষ না হওয়া পর্যন্ত Phase 2 দেবেন না।

:::writing{variant="document" id="27418"}
# PHASE 1 — Solution Setup

Read CLAUDE.md first.

## Goal

Create the initial Optical Shop MVP solution using the required 4-layer architecture.

## Scope

Create:

- Optical.sln
- Optical.Domain
- Optical.Application
- Optical.Infrastructure
- Optical.Web

Configure project references according to CLAUDE.md.

Configure:

- .NET 10
- nullable reference types
- implicit usings
- EF Core 10 packages
- PostgreSQL provider
- ASP.NET Core MVC

Do NOT implement:

- Authentication
- Product
- POS
- Inventory
- Reports
- business features

## Requirements

Verify dependency direction:

Web → Application → Domain
Infrastructure → Application → Domain

Domain must have no external infrastructure dependency.

## Workflow

1. Inspect repository.
2. Give a short plan.
3. Implement Phase 1 only.
4. Run dotnet restore.
5. Run dotnet build.
6. Fix only issues caused by this phase.
7. Report changed files and verification.
8. STOP.

Do not continue to Phase 2.
```

# PHASE 2 — Authentication / Authorization (RBAC), Roles,Permissiongs. (Admin Can Create everyting)

Read CLAUDE.md.

Phase 1 should already exist.

## Goal

Implement minimal production-ready authentication using ASP.NET Core Identity.

## Scope

Implement only:

* ApplicationUser
* Identity configuration
* Login
* Logout
* Authentication cookie
* Basic authorization
* Admin user seed
* Login Razor View
* AccountController

Use ASP.NET Core Identity (Framework) for password hashing and authentication, Authorization, RBAC, Roles, Permissions,

Do NOT implement:

* complex permissions
* multi-branch authorization
* multi-tenancy
* custom JWT
* refresh tokens
* external login
* email confirmation
* password reset workflow

## Requirements

Seed one development/admin user safely.

Do not hard-code secrets into source code.

Use configuration for credentials where appropriate.

Protect POST actions with anti-forgery.

Keep AccountController thin.

## Verification

Run:

dotnet build

If database infrastructure is ready, verify Identity tables/migration.

Fix only authentication-related issues.

Report:

* files changed
* authentication flow
* verification result

STOP after Phase 2.

```

:::writing{variant="document" id="83506"}
# PHASE 3 — Branch, Category and Brand

Read CLAUDE.md.

Authentication already exists.

Implement ONLY:

1. Branch
2. Category
3. Brand

## Branch

For the current MVP:

- Single Shop
- One default branch
- No branch switching
- No multi-branch authorization

Keep Branch entity because future expansion may need it.

## Category

Implement:

- List
- Create
- Edit
- Active/Inactive if useful

## Brand

Implement:

- List
- Create
- Edit
- Active/Inactive if useful

## Architecture

Domain:

- entities
- BaseEntity where appropriate

Application:

- Vertical Slice features
- queries/commands only where required
- validation where useful

Infrastructure:

- EF configurations
- migration if needed

Web:

- thin controllers
- Razor views
- simple Bootstrap UI

Do not create:

- generic CRUD framework
- generic repository
- generic service
- unnecessary abstractions

## Verification

Run:

dotnet build

Verify database migration if schema changed.

Do not implement Product yet.

Report briefly and STOP.
```

# PHASE 4 — Product

Read CLAUDE.md.

Implement ONLY the Product feature.

## Minimum Product

Support:

* Id
* SKU
* Name
* CategoryId
* BrandId
* PurchasePrice
* SalePrice
* ReorderLevel
* IsActive
* CreatedAt
* UpdatedAt

Inventory quantity should NOT be duplicated inside Product if InventoryItem is responsible for stock.

## Features

Implement:

* Product list
* Search
* Product details
* Create
* Edit
* Active/Inactive
* Category selection
* Brand selection

## Validation

Validate:

* SKU required and unique
* Name required
* Category exists
* Brand exists where required
* Purchase price >= 0
* Sale price >= 0
* Reorder level >= 0

## Database

Configure:

* relationships
* indexes
* unique SKU
* decimal precision
* appropriate delete behavior

Use AsNoTracking for read-only queries.

Do not implement:

* inventory transactions
* purchases
* POS
* prescriptions
* lenses
* frames
* complex pricing

## Verification

Run:

dotnet build

Run migration if required.

Do not continue to Inventory.

Report and STOP.

```

:::writing{variant="document" id="30654"}
# PHASE 5 — Supplier, Purchase and Inventory

Read CLAUDE.md.

Implement ONLY:

- Supplier
- Purchase
- PurchaseItem
- InventoryItem
- Stock increase
- Stock adjustment
- Low-stock visibility

## Supplier

Implement:

- List
- Search
- Create
- Edit
- Active/Inactive

## Purchase

Flow:

Supplier
↓
Purchase
↓
Purchase Items
↓
Inventory increases

Support:

- Supplier
- Purchase date
- Invoice number optional
- Product
- Quantity
- Purchase price
- Total

## Inventory

Support:

- Current stock
- Stock increase from purchase
- Manual stock adjustment
- Low-stock products

Rules:

- Quantity must be > 0
- Stock cannot become negative
- Product must exist and be active where appropriate
- Purchase total must be calculated server-side

## Transaction

Purchase creation must be atomic:

Purchase
+
PurchaseItems
+
Inventory update

Either everything succeeds or nothing is saved.

## Important

Do NOT implement:

- Warehouse
- stock transfer
- batch
- serial number
- FIFO
- LIFO
- complex inventory ledger

Do not implement POS yet.

## Verification

Run:

dotnet build

Create/review EF migration.

Test:

1. Create purchase.
2. Verify inventory increases.
3. Try invalid quantity.
4. Try invalid product.
5. Verify failed transaction does not partially update data.

Report and STOP.
```

---

# 3. Phase 6–10 Prompts

এখানে মূল business value শুরু হবে—বিশেষ করে **Phase 7 POS/Checkout**। এই phase-গুলো আলাদা রাখা হয়েছে যাতে Claude একবারে বেশি context না খায়।

# PHASE 6 — Customer

Read CLAUDE.md.

Implement ONLY Customer management.

## Customer fields

Minimum:

* Id
* Name
* Phone
* Email optional
* Address optional
* Notes optional
* IsActive
* CreatedAt
* UpdatedAt

## Features

Implement:

* Customer list
* Search by name/phone
* Customer details
* Create
* Edit
* Active/Inactive

## Validation

* Name required
* Phone required if appropriate for the business
* sensible field lengths
* no unnecessary validation framework

## UI

Use simple Bootstrap Razor views.

Keep controller thin.

Use Application features for business/data access.

Do not implement:

* loyalty
* credit system
* customer wallet
* prescription
* CRM
* customer analytics

## Verification

Run dotnet build.

Verify existing Product, Purchase and Inventory features still work.

Report and STOP.

```

:::writing{variant="document" id="56172"}
# PHASE 7 — POS, Checkout and Payment

Read CLAUDE.md carefully.

This is the most important MVP feature.

Implement ONLY:

- POS screen
- Cart
- Product search
- Add/remove cart items
- Quantity update
- Customer selection optional
- Payment method
- Checkout
- Sale
- SaleItem
- Payment
- Inventory deduction

## POS Flow

Search Product
↓
Add to Cart
↓
Change Quantity
↓
Select Customer optional
↓
Select Payment Method
↓
Checkout
↓
Create Sale
↓
Create SaleItems
↓
Create Payment
↓
Decrease Inventory

## Payment Methods

Only:

- Cash
- Card
- Mobile Banking

## Critical Security Rule

NEVER trust:

- client-side price
- client-side stock
- client-side total

At checkout:

1. Load products from database.
2. Verify products exist.
3. Verify products are active.
4. Verify requested quantity.
5. Verify sufficient stock.
6. Read current sale prices from database.
7. Calculate subtotal server-side.
8. Calculate final total server-side.
9. Create Sale.
10. Create SaleItems.
11. Create Payment.
12. Decrease inventory.

## Transaction

Checkout must use one database transaction.

If any operation fails:

NO Sale
NO SaleItems
NO Payment
NO Stock deduction

must remain saved.

## Validation

Reject:

- empty cart
- invalid product
- inactive product
- quantity <= 0
- insufficient stock
- invalid payment method

## UI

Create a practical POS screen:

- product search
- cart table
- quantity controls
- customer selector
- payment selector
- grand total
- checkout button

Use JavaScript/jQuery only for cart interaction.

Server remains authoritative.

## Do NOT implement

- discounts
- coupons
- loyalty
- installments
- online payment gateway
- Stripe
- SSLCommerz
- advanced pricing

unless explicitly requested later.

## Testing

Test at minimum:

1. Successful checkout.
2. Multiple products.
3. Quantity update.
4. Insufficient stock.
5. Inactive product.
6. Empty cart.
7. Failed checkout rolls back transaction.
8. Inventory decreases correctly.
9. Sale total is calculated server-side.

Run dotnet build and tests.

Report and STOP.
```

# PHASE 8 — Sales and Receipt

Read CLAUDE.md.

Implement ONLY:

* Sales list
* Sale details
* Printable receipt

## Sales

Support:

* Sale number
* Date/time
* Customer
* Total
* Payment method
* Status

Implement:

* Sale list
* Date filtering if simple
* Sale details

Use AsNoTracking for read-only queries.

## Receipt

Create a simple printable Razor receipt.

Include:

* Shop name
* Date/time
* Sale number
* Customer if available
* Items
* Quantity
* Unit price
* Line total
* Payment method
* Grand total

Use browser print functionality.

Do NOT add:

* PDF generation
* reporting engine
* thermal printer SDK
* complex receipt framework

unless explicitly requested.

## Important

Do not modify checkout business logic unnecessarily.

Reuse the existing Sale data.

## Verification

Run:

dotnet build

Verify:

POS checkout → Sale → Sale details → Receipt

Report and STOP.

```

:::writing{variant="document" id="18539"}
# PHASE 9 — Dashboard

Read CLAUDE.md.

Implement ONLY a minimal Dashboard.

## Dashboard metrics

Show:

- Today's sales amount
- Today's sale count
- Current inventory item count
- Low-stock product count
- Recent sales

## Requirements

Queries should be simple and efficient.

Use:

- AsNoTracking
- appropriate projections
- async database access

Do not introduce:

- caching
- Redis
- analytics engine
- charts library unless already available and genuinely useful

## UI

Create a clean Bootstrap dashboard.

Keep it practical.

Do not redesign the entire application.

## Verification

Run:

dotnet build

Verify:

- today's sales
- sale count
- low stock
- recent sales

Report and STOP.
```

# PHASE 10 — Basic Reports + Production Hardening

Read CLAUDE.md.

This is the final MVP hardening phase.

Implement ONLY:

1. Basic Sales Report
2. Basic Inventory Report
3. Production-readiness review
4. Cleanup of obvious MVP issues

---

# SALES REPORT

Support:

* Date range
* Total sales
* Number of sales
* Payment method summary

Keep query implementation simple.

---

# INVENTORY REPORT

Show:

* Product
* SKU
* Current stock
* Reorder level
* Low-stock status

Use efficient read-only queries.

---

# PRODUCTION HARDENING

Review the complete application for:

## Security

* Authentication
* Authorization
* Anti-forgery
* Validation
* Cookie configuration
* Secrets
* Error exposure
* Logging

## Database

* Foreign keys
* indexes
* unique constraints
* decimal precision
* delete behaviors
* migration consistency

## Application

* thin controllers
* no database logic in controllers
* no duplicated business rules
* no unnecessary abstractions
* no dead code

## Performance

* AsNoTracking
* async queries
* unnecessary queries
* obvious N+1 problems
* pagination where appropriate

## POS

Verify:

* server-side price
* server-side totals
* stock validation
* atomic transaction
* inventory deduction

## UI

Verify:

* validation messages
* responsive layout
* navigation
* error handling
* receipt printing

---

# IMPORTANT

Do NOT redesign the architecture.

Do NOT introduce:

* Redis
* MediatR
* AutoMapper
* Generic Repository
* UnitOfWork
* CQRS framework
* Microservices
* Multi-Tenancy
* Multi-Branch logic
* advanced accounting
* advanced reporting

Only fix genuine production-readiness issues found in the existing MVP.

---

# VERIFICATION

Run:

dotnet build

Run all available tests.

Review EF migrations.

Perform a final smoke test of:

Login
→ Category
→ Brand
→ Product
→ Purchase
→ Inventory
→ Customer
→ POS
→ Checkout
→ Sale
→ Receipt
→ Dashboard
→ Reports

At the end provide:

### MVP Status

PASS / NEEDS FIXES

### Issues Fixed

* ...

### Remaining Issues

* ...

### Architecture Review

* ...

### Next Recommended Step

Only if genuinely necessary.

STOP.

````

---

## সবচেয়ে গুরুত্বপূর্ণ: Claude Code-এ কীভাবে ব্যবহার করবেন

আমি আপনাকে **একবারে পুরো ১০ Phase দেব না**—উপরের prompts-এর উদ্দেশ্যই হলো context ছোট রাখা।

আপনার workflow হবে:

```text
CLAUDE.md
     ↓
Phase 1
     ↓
Phase 2
     ↓
Phase 3
     ↓
Phase 4
     ↓
Phase 5
     ↓
Phase 6
     ↓
Phase 7
     ↓
Phase 8
     ↓
Phase 9
     ↓
Phase 10
````

### প্রতিটি Phase-এর পর Claude-কে থামাবেন

Claude যদি Phase 4-এর শেষে নিজে থেকে বলে:

> "I can also implement inventory..."

তাকে করতে দেবেন না।

পরবর্তী prompt নিজে দেবেন।

---

# আপনার জন্য আমার Coding Mentor Rule

আপনি যখন Claude Code-এ কোনো Phase চালাবেন এবং কোনো error/architecture decision আসবে, **সরাসরি Claude-কে blindly follow করবেন না**।

আপনি এখানে আমাকে দিতে পারেন:

```text
Phase 4 চলছে।

Claude Code এই structure তৈরি করেছে:

[paste structure]

এই code তৈরি করেছে:

[paste relevant code]

এটা ঠিক আছে কি?
```

আমি তখন **Coding Mentor হিসেবে** তিনটি জিনিস দেখব:

1. **Architecture ঠিক আছে কি না**
2. **Over-engineering হয়েছে কি না**
3. **Beginner-friendly ও production-ready হয়েছে কি না**

বিশেষ করে আপনার project-এ আমি নজর রাখব যেন ধীরে ধীরে এমন কিছু না ঢুকে যায়:

```text
Generic Repository
      ↓
Generic Service
      ↓
Base Service
      ↓
Base Handler
      ↓
Generic Result
      ↓
Generic Response
      ↓
CQRS abstraction
      ↓
আরও abstraction...
```

এর বদলে আমাদের লক্ষ্য থাকবে:

```text
POS
 ↓
CheckoutCommand
 ↓
CheckoutHandler
 ↓
DbContext
 ↓
Database
```

অর্থাৎ **প্রয়োজন যতটুকু, architecture ততটুকুই।**

এটাই আপনার Optical Shop MVP-এর জন্য সবচেয়ে গুরুত্বপূর্ণ development principle।
