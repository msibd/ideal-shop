# Phase 3 — Category and Brand

Status: **Complete** — build passes, migrations applied, verified against the running application
Date: 2026-09-04 · *updated 2026-09-04 (System Settings added; stale database claims corrected)*

> **Branch was skipped on request.** The shop is a single store, so no `Branch` entity, table, or column exists.
> Nothing in Category or Brand references a branch, so there is no stub or dead column to clean up later.

---

## 1. Scope

Implemented:

- Category — list, search, create, edit, active/inactive
- Brand — list, search, create, edit, active/inactive
- **System Settings — business name and address, create + update, Admin only** *(added later, see §11)*
- `BaseEntity` with automatic `CreatedAt` / `UpdatedAt` stamping
- `IApplicationDbContext` (first real requirement)
- First EF entity configurations + migration
- First role-gated controllers (Admin only)

Not implemented (out of scope):

- Branch (skipped by instruction)
- Product, Supplier, Purchase, Inventory, Customer, POS
- delete/soft-delete (status change covers the MVP need)
- pagination on these two lists
- centralised exception middleware (Phase 10)

---

## 2. Design decisions

| Decision | Choice | Reason |
|---|---|---|
| Primary key | `int` identity on `BaseEntity` | Small indexes, readable URLs (`/Categories/Edit/5`). Identity users keep their `string` keys — mixing is fine. |
| Timestamps | Stamped once in `ApplicationDbContext.SaveChangesAsync` (UTC) | Not repeated in every handler. Npgsql requires UTC for `timestamptz`. |
| `IApplicationDbContext` | Introduced **now** | First real requirement: Application must query without knowing the concrete EF context. Phase 2 correctly had none. |
| Slice granularity | **One file per slice** — record + handler together | 4 files per entity instead of 8. Still a genuine vertical slice; less file noise. |
| Active/Inactive | Checkbox on the **Edit form only** | No separate toggle slice. One way to change status, not two. |
| Validation errors | `DomainException` → caught once per POST → `ModelState` | Explicit, no `Result<T>` abstraction. One `try/catch` per POST action, not defensive noise. |
| `NotFoundException` | Deliberately **not** caught in POST | Only reachable if a record is deleted between GET and POST. Falls through to the error page. |
| Authorization | `[Authorize(Roles = AppRoles.Admin)]` on both controllers | A cashier has no business creating categories. |
| Pagination | **None** on these lists | A shop has ~20 categories. Search by name is enough. Products get pagination in Phase 4. |

---

## 3. Layer-by-layer changes

### 3.1 Optical.Domain

First files in this layer. Still **zero** project or package references.

```
Optical.Domain/
├── Common/
│   └── BaseEntity.cs
├── Entities/
│   ├── Category.cs
│   └── Brand.cs
└── Exceptions/
    ├── DomainException.cs
    └── NotFoundException.cs
```

**`Common/BaseEntity.cs`**

```csharp
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

`UpdatedAt` is nullable on purpose: a record that was never edited has no update time, and `null` says that honestly.

**`Entities/Category.cs`** and **`Entities/Brand.cs`** — identical shape, deliberately kept separate (they will diverge; `Product` references both):

```csharp
public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
```

**`Exceptions/DomainException.cs`** — a business rule was violated; the message is safe to show the user.
**`Exceptions/NotFoundException.cs`** — the requested record does not exist.

Both are one-line primary-constructor types. No exception hierarchy, no error codes, no base exception class.

---

### 3.2 Optical.Application

```
Optical.Application/
├── DependencyInjection.cs                       (new)
├── Abstractions/
│   └── Persistence/
│       └── IApplicationDbContext.cs             (new)
└── Features/
    ├── Categories/
    │   ├── GetCategories.cs
    │   ├── GetCategoryById.cs
    │   ├── CreateCategory.cs
    │   └── UpdateCategory.cs
    └── Brands/
        ├── GetBrands.cs
        ├── GetBrandById.cs
        ├── CreateBrand.cs
        └── UpdateBrand.cs
```

**`Abstractions/Persistence/IApplicationDbContext.cs`**

```csharp
public interface IApplicationDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

This is the *only* persistence abstraction. No repository, no `IRepository<T>`, no unit of work — `DbSet` plus `SaveChangesAsync` already is a unit of work.

**Slice anatomy.** Each file holds the request record and its handler. Handlers are plain classes registered in DI and injected into the controller — no mediator, no dispatcher.

`GetCategories.cs` — read side, `AsNoTracking` + projection, never loads the entity:

```csharp
public sealed record CategoryListItem(int Id, string Name, string? Description, bool IsActive);

public sealed class GetCategoriesHandler(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<CategoryListItem>> HandleAsync(string? search, CancellationToken ct = default)
    {
        var query = db.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term));
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CategoryListItem(c.Id, c.Name, c.Description, c.IsActive))
            .ToListAsync(ct);
    }
}
```

Search uses `ToLower().Contains(...)` rather than `EF.Functions.ILike` on purpose — `ILike` is Npgsql-specific and would drag a PostgreSQL dependency into the Application layer.

`GetCategoryById.cs` — same read pattern, returns `CategoryDetails?`.

`CreateCategory.cs` — trims the name, rejects a case-insensitive duplicate, sets `IsActive = true`:

```csharp
var name = command.Name.Trim();
var normalized = name.ToLower();

if (await db.Categories.AnyAsync(c => c.Name.ToLower() == normalized, ct))
    throw new DomainException($"A category named \"{name}\" already exists.");
```

`normalized` is computed **before** the query so EF sends it as a parameter instead of trying to translate `ToLower()` on a local.

`UpdateCategory.cs` — loads the tracked entity, throws `NotFoundException` if missing, excludes itself from the duplicate check (`c.Id != command.Id`), then assigns `Name`, `Description`, `IsActive`.

Empty or whitespace descriptions are normalised to `null` in both commands, so the database never stores `""`.

**`DependencyInjection.cs`** — 8 handlers registered explicitly, one line each:

```csharp
services.AddScoped<GetCategoriesHandler>();
services.AddScoped<GetCategoryByIdHandler>();
...
```

Assembly scanning was rejected: explicit registration is greppable and has no magic. It grows one line per slice, which is acceptable.

**csproj change**

```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.11" />
```

Abstractions only — the Application layer still has no ASP.NET Core dependency.

---

### 3.3 Optical.Infrastructure

```
Optical.Infrastructure/
├── DependencyInjection.cs                                   (edited)
└── Persistence/
    ├── ApplicationDbContext.cs                              (edited)
    ├── Configurations/                                      (new folder)
    │   ├── CategoryConfiguration.cs
    │   └── BrandConfiguration.cs
    └── Migrations/
        ├── 20260904143110_AddCategoryAndBrand.cs            (new)
        ├── 20260904143110_AddCategoryAndBrand.Designer.cs   (new)
        └── ApplicationDbContextModelSnapshot.cs             (updated)
```

**`Persistence/Configurations/CategoryConfiguration.cs`** (Brand is identical)

```csharp
builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
builder.Property(c => c.Description).HasMaxLength(300);
builder.HasIndex(c => c.Name).IsUnique();
```

`ApplyConfigurationsFromAssembly` — already wired in Phase 2 — picks these up automatically. The
"No IEntityTypeConfiguration found" warning from Phase 2 is now gone.

**`Persistence/ApplicationDbContext.cs`** — two changes:

1. Implements `IApplicationDbContext`, exposing `Categories` and `Brands` via `Set<T>()`.
2. Overrides `SaveChangesAsync` to stamp timestamps:

```csharp
private void StampTimestamps()
{
    var now = DateTime.UtcNow;

    foreach (var entry in ChangeTracker.Entries<BaseEntity>())
    {
        if (entry.State == EntityState.Added)        entry.Entity.CreatedAt = now;
        else if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = now;
    }
}
```

One place, applies to every current and future `BaseEntity`. Handlers never touch timestamps.

**`DependencyInjection.cs`** — one line added so the Application layer gets the same scoped instance as EF:

```csharp
services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
```

Resolving the *same* registration matters: if it were a second `AddScoped<IApplicationDbContext, ApplicationDbContext>()`, a request would end up with two contexts and two change trackers.

**Migration `20260904143110_AddCategoryAndBrand`**

| Column | Type | Constraint |
|---|---|---|
| `Id` | `integer` | PK, `IdentityByDefaultColumn` |
| `Name` | `character varying(100)` | NOT NULL, unique index `IX_Categories_Name` / `IX_Brands_Name` |
| `Description` | `character varying(300)` | NULL |
| `IsActive` | `boolean` | NOT NULL |
| `CreatedAt` | `timestamp with time zone` | NOT NULL |
| `UpdatedAt` | `timestamp with time zone` | NULL |

Two tables, `Categories` and `Brands`. No foreign keys yet — `Product` adds those in Phase 4.

---

### 3.4 Optical.Web

```
Optical.Web/
├── Program.cs                                (edited: AddApplication)
├── Controllers/
│   ├── CategoriesController.cs               (new)
│   └── BrandsController.cs                   (new)
├── ViewModels/
│   ├── CategoryFormViewModel.cs              (new)
│   └── BrandFormViewModel.cs                 (new)
└── Views/
    ├── Categories/  Index / Create / Edit    (new)
    ├── Brands/      Index / Create / Edit    (new)
    └── Shared/
        └── _Layout.cshtml                    (edited)
```

**`ViewModels/CategoryFormViewModel.cs`** — `Id`, `Name` (required, max 100), `Description` (max 300), `IsActive`.
Lengths mirror the EF configuration, so client-side and database validation agree.

**`Controllers/CategoriesController.cs`** (Brands is identical in shape)

| Action | Method | Behaviour |
|---|---|---|
| `Index(search)` | GET | calls `GetCategoriesHandler`, passes the search term back via `ViewData["Search"]` |
| `Create()` | GET | empty form |
| `Create(model)` | POST | `ModelState` → `CreateCategoryCommand` → `TempData["Success"]` → redirect |
| `Edit(id)` | GET | `GetCategoryByIdHandler`; `NotFound()` if null |
| `Edit(model)` | POST | `ModelState` → `UpdateCategoryCommand` → `TempData["Success"]` → redirect |

Both POST actions follow the same shape:

```csharp
if (!ModelState.IsValid) return View(model);

try
{
    await createCategory.HandleAsync(new CreateCategoryCommand(model.Name, model.Description), ct);
}
catch (DomainException ex)
{
    ModelState.AddModelError(nameof(model.Name), ex.Message);
    return View(model);
}

TempData["Success"] = "Category created.";
return RedirectToAction(nameof(Index));
```

Post-Redirect-Get, so a browser refresh cannot resubmit. The controller holds no EF query, no
uniqueness rule, no total, no status logic — it binds, calls, and redirects.

Handlers are injected individually through the primary constructor (four of them). Explicit and
greppable; no service locator, no mediator.

**Views** — Bootstrap 5, no custom CSS.

- `Index.cshtml` — heading + "New category" button, GET search form with a Clear link, empty-state alert, table of Name / Description / Status badge / Edit button. Status renders as a green **Active** or grey **Inactive** badge.
- `Create.cshtml` — Name + Description. No `IsActive` field: a new record is always active.
- `Edit.cshtml` — hidden `Id`, Name, Description, and the `IsActive` checkbox.
- Both forms include `_ValidationScriptsPartial` for client-side validation; the server validates regardless.

**`Views/Shared/_Layout.cshtml`** — two additions:

1. Categories and Brands nav links, wrapped in `@if (User.IsInRole(AppRoles.Admin))` so a cashier does not see links that would only give them an Access Denied page.
2. A dismissible `TempData["Success"]` alert above `@RenderBody()`, shared by every feature from here on.

**`Program.cs`** — one line, before `AddInfrastructure`:

```csharp
builder.Services.AddApplication();
```

---

## 4. Request flow

```
GET /Categories?search=fra
  CategoriesController.Index
    -> GetCategoriesHandler.HandleAsync("fra")
         db.Categories.AsNoTracking()
           .Where(name contains)      -> SQL LIKE on lower(Name)
           .OrderBy(Name)
           .Select(CategoryListItem)  -> projection, entity never materialised
    -> View(IReadOnlyList<CategoryListItem>)

POST /Categories/Create                [anti-forgery enforced globally]
  ModelState invalid          -> redisplay form
  CreateCategoryHandler
    duplicate name            -> DomainException -> ModelState -> redisplay form
    ok                        -> Add + SaveChangesAsync (CreatedAt stamped)
                              -> TempData["Success"] -> redirect to Index

POST /Categories/Edit
  UpdateCategoryHandler
    record missing            -> NotFoundException -> error page
    duplicate name (other id) -> DomainException -> ModelState -> redisplay form
    ok                        -> assign + SaveChangesAsync (UpdatedAt stamped)
                              -> TempData["Success"] -> redirect to Index

Any of the above, signed in as Cashier -> 302 /Account/AccessDenied
```

---

## 5. Validation rules

| Rule | Enforced where |
|---|---|
| Name required | ViewModel `[Required]` + EF `IsRequired()` (NOT NULL) |
| Name max 100 | ViewModel `[StringLength(100)]` + `varchar(100)` |
| Description max 300 | ViewModel `[StringLength(300)]` + `varchar(300)` |
| Name unique (case-insensitive) | `CreateCategoryHandler` / `UpdateCategoryHandler` |
| Name unique (exact) | PostgreSQL unique index — backstop against a race |
| Name trimmed | handler, before saving |
| Empty description stored as NULL | handler |
| Admin only | `[Authorize(Roles = AppRoles.Admin)]` |
| Anti-forgery on POST | global filter from Phase 2 |

**Known limitation.** The case-insensitive duplicate check lives in the handler; the database index is
case-*sensitive*. So "Frames" vs "frames" is rejected by the handler, but two concurrent requests could
in theory slip a case-variant past the index. A case-insensitive database constraint needs the
PostgreSQL `citext` type or an expression index on `lower(Name)` — not worth the complexity for a
single shop, and recorded here rather than silently ignored.

---

## 6. Verification

```
dotnet build Optical.slnx
  -> Build succeeded. 0 Warning(s), 0 Error(s)
  -> Razor views compile at build time, so all 6 views are verified.

dotnet ef migrations add AddCategoryAndBrand
      --project Optical.Infrastructure --startup-project Optical.Web
      --output-dir Persistence/Migrations
  -> OK. Tables Categories + Brands, unique index on Name, timestamptz columns.
  -> The Phase 2 "No IEntityTypeConfiguration found" warning is gone.

dotnet ef database update
  -> OK. Applied 20260904141647_InitialIdentity
             and 20260904143110_AddCategoryAndBrand
```

### Correction to the original text of this section

This document first reported `database update` as **FAILED — cannot connect to 127.0.0.1:5432**, and
concluded that "PostgreSQL is still not running on the development machine." **That conclusion was
wrong.** The check only looked for a Windows service and for `psql` on `PATH`. PostgreSQL was running
the whole time, in Docker:

```
PostgreSQL_Server   postgres:18   0.0.0.0:5434->5432/tcp   (healthy)
```

The real fault was the guessed connection string, which used port **5432** while the container
publishes **5434**. Once the port was corrected and the `opticalshop` database created, both
migrations applied cleanly. Working connection string (held in user-secrets, never in source):

```
Host=localhost;Port=5434;Database=opticalshop;Username=postgres;Password=postgres
```

Live schema now contains 11 tables: 7 Identity + `Categories` + `Brands` + `ShopSettings` +
`__EFMigrationsHistory`.

---

## 7. Manual test checklist

1. Sign in as `admin`, confirm **Categories** and **Brands** appear in the nav.
2. Create a category — success alert shows, row appears in the list.
3. Create the same name again — rejected with "A category named ... already exists." on the Name field.
4. Create the same name in different case (`frames` vs `Frames`) — also rejected.
5. Edit a category, uncheck **Active** — row shows the grey Inactive badge.
6. Edit a category and keep its own name — must **not** report a duplicate.
7. Search by partial name, then Clear.
8. Submit an empty name — client and server both reject it.
9. Check the database: `CreatedAt` set on insert, `UpdatedAt` still null; after an edit, `UpdatedAt` set.
10. Repeat 2-9 for Brands.
11. Browse to `/Categories` without the Admin role — expect Access Denied. **Verified** (§11.7).

---

## 8. Architecture compliance

| Rule | Result |
|---|---|
| Domain has no Infrastructure/Web dependency | Domain has zero references of any kind |
| Application has no Web dependency | DI abstractions + EF Core only; no ASP.NET types |
| Infrastructure implements Application abstractions | `ApplicationDbContext : IApplicationDbContext` |
| Controllers stay thin | bind, call handler, redirect; no EF, no rules |
| No DB access from controllers | no `DbContext` or `DbSet` anywhere in Web |
| Business logic in Application/Domain | uniqueness, trimming, status all in handlers |
| No generic repository / UnitOfWork | `IApplicationDbContext` only |
| No banned abstractions (§18) | no MediatR, AutoMapper, Repository, CQRS framework |
| `AsNoTracking` on read queries | both list and detail queries |
| Projection instead of entity loading | `Select(...)` into records |
| Decimal precision | not applicable yet — no money in this phase |

---

## 9. Open items carried forward

- ~~Apply both migrations once PostgreSQL is installed and running.~~ **Done** — see §6.
- ~~No Cashier user exists, so the Access Denied path is untested.~~ **Done** — the role restriction
  was exercised directly, see §11.7. A real Cashier *user* still does not exist.
- Run the manual test checklist in section 7 by hand in a browser.
- Centralised exception middleware for `NotFoundException` / unexpected errors — Phase 10.
- Case-insensitive uniqueness is enforced in handlers only; the database index is case-sensitive (§5).

---

## 10. Next phase

**Phase 4 — Product** (CLAUDE.md §25), now implemented — see `docs/Phase4.md`. It added the first
foreign keys (`CategoryId`, `BrandId`) pointing at these two tables with `DeleteBehavior.Restrict`,
the first `decimal(18,2)` money columns, a unique SKU index, and the first paginated list.

*(The database bring-up, app shell and dashboard UI are documented separately in
`docs/Platform-UI-And-Database.md` — that work is not a numbered phase.)*

---

# 11. Addendum — System Settings

Added after the original Phase 3 work, on request: **create and update options for the business name
and address, held in System Settings, changeable only by the shop owner / administrator.**

It is documented here because it is the same kind of feature as Category and Brand — small
configuration data, Admin-only, no money and no stock — and it reuses the Phase 3 slice pattern
unchanged.

## 11.1 Design decisions

| Decision | Choice | Reason |
|---|---|---|
| Row count | **Exactly one row**, `Id = 1`, enforced by a database check constraint | A single shop has one identity. The invariant lives in the database, not in a comment. |
| Create vs update | **One screen, one handler** (`SaveShopSettings`) that creates on first save and updates afterwards | A "create" screen usable exactly once is bad UX. The handler returns whether it created, so the success message still distinguishes the two. |
| Fields | `BusinessName`, `Address` only | Exactly what was asked. Phone and email are a two-line change if a receipt later needs them — not added speculatively. |
| "Shop owner" role | Mapped to the existing **`Admin`** role | `Admin` already *is* the owner in this MVP. A separate `Owner` role would be a second name for the same thing. |
| Validation | ViewModel `[Required]` + database `NOT NULL` | Consistent with Category/Brand. No `DomainException` is thrown here — there is no uniqueness or cross-record rule to break — so the controller needs no `try/catch`. |

## 11.2 Optical.Domain

**`Entities/ShopSettings.cs`** (new)

```csharp
public class ShopSettings : BaseEntity
{
    public const int SingleRowId = 1;

    public string BusinessName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}
```

`SingleRowId` is a domain constant rather than a magic `1` scattered through handlers. Domain still
has zero references.

## 11.3 Optical.Application

```
Features/Settings/
├── GetShopSettings.cs      (query + handler)
└── SaveShopSettings.cs     (command + handler)
```

**`GetShopSettings.cs`** — `AsNoTracking` projection; returns `null` when never configured:

```csharp
public sealed record ShopSettingsDetails(string BusinessName, string Address);

public Task<ShopSettingsDetails?> HandleAsync(CancellationToken ct = default) =>
    db.ShopSettings.AsNoTracking()
        .Select(s => new ShopSettingsDetails(s.BusinessName, s.Address))
        .FirstOrDefaultAsync(ct);
```

**`SaveShopSettings.cs`** — the create-or-update rule, returning `true` when it created:

```csharp
var settings = await db.ShopSettings.FirstOrDefaultAsync(ct);
var created = settings is null;

if (settings is null)
{
    settings = new ShopSettings { Id = ShopSettings.SingleRowId };
    db.ShopSettings.Add(settings);
}

settings.BusinessName = command.BusinessName.Trim();
settings.Address = command.Address.Trim();

await db.SaveChangesAsync(ct);
return created;
```

`CreatedAt` / `UpdatedAt` are stamped automatically by the `SaveChangesAsync` override from §3.3 —
nothing extra was needed.

**`IApplicationDbContext`** gains one member:

```csharp
DbSet<ShopSettings> ShopSettings { get; }
```

**`DependencyInjection.cs`** — two more explicit registrations.

## 11.4 Optical.Infrastructure

**`Persistence/Configurations/ShopSettingsConfiguration.cs`** (new)

```csharp
builder.Property(s => s.Id).ValueGeneratedNever();
builder.Property(s => s.BusinessName).IsRequired().HasMaxLength(150);
builder.Property(s => s.Address).IsRequired().HasMaxLength(300);
builder.ToTable(t => t.HasCheckConstraint("CK_ShopSettings_SingleRow", "\"Id\" = 1"));
```

`ValueGeneratedNever()` matters: the key is assigned by the application so the single-row rule can be
expressed at all. Without it PostgreSQL would hand out `2`, `3`, … and the check constraint would
simply reject every insert after the first — a confusing failure instead of a guarantee.

**`ApplicationDbContext`** exposes `ShopSettings`; nothing else changed.

**Migration `20260904155650_AddShopSettings`** — live schema:

```
    Column    |           Type           | Nullable
--------------+--------------------------+----------
 Id           | integer                  | not null
 BusinessName | character varying(150)   | not null
 Address      | character varying(300)   | not null
 CreatedAt    | timestamp with time zone | not null
 UpdatedAt    | timestamp with time zone |
Indexes:
    "PK_ShopSettings" PRIMARY KEY, btree ("Id")
Check constraints:
    "CK_ShopSettings_SingleRow" CHECK ("Id" = 1)
```

## 11.5 Optical.Web

| File | Change |
|---|---|
| `ViewModels/ShopSettingsViewModel.cs` | new — `BusinessName` (required, 150), `Address` (required, 300) |
| `Controllers/SettingsController.cs` | new — `[Authorize(Roles = AppRoles.Admin)]`, GET + POST `Index` |
| `Views/Settings/Index.cshtml` | new — form card + an explanatory card |
| `Views/Shared/_Layout.cshtml` | edited — **Settings** nav tab (gear icon) inside the existing `@if (isAdmin)` block; header now renders the configured business name (§11.10) |
| `Views/Account/Login.cshtml` | edited — sign-in heading and page title use the business name (§11.10) |

The controller stays thin: load, bind, call handler, redirect. `ViewData["IsConfigured"]` drives only
cosmetics — the status chip (`CONFIGURED` / `NOT SET UP`) and the button caption
(`Save business details` / `Save changes`).

```csharp
TempData["Success"] = created ? "Business details saved." : "Business details updated.";
return RedirectToAction(nameof(Index));
```

Post-Redirect-Get, so a refresh cannot resubmit. Anti-forgery comes from the global filter.

## 11.6 Request flow

```
GET /Settings   (Admin only)
  -> GetShopSettingsHandler
       null  -> empty form, chip "NOT SET UP",  button "Save business details"
       row   -> populated form, chip "CONFIGURED", button "Save changes"

POST /Settings                      [anti-forgery enforced globally]
  ModelState invalid -> redisplay with field errors
  SaveShopSettingsHandler
       no row -> INSERT Id=1, CreatedAt stamped -> "Business details saved."
       row    -> UPDATE,      UpdatedAt stamped -> "Business details updated."
  -> redirect to GET /Settings

Signed in without the Admin role -> 302 /Account/AccessDenied
```

## 11.7 Verification — run against the live application

```
dotnet build Optical.slnx                -> 0 Warning(s), 0 Error(s)
dotnet ef database update                -> Applied 20260904155650_AddShopSettings

1. GET  /Settings  (never configured)    -> 200, chip "NOT SET UP"
2. POST /Settings  create                -> 302
   DB: Id=1 | Vision Care Optical | 142 Green Road, Dhanmondi, Dhaka 1205 | UpdatedAt NULL
3. GET  /Settings  (after save)          -> chip "CONFIGURED", button "Save changes",
                                            field pre-filled
4. POST /Settings  update, name padded
   with spaces                           -> 302
   DB: Id=1 | Vision Care Optical Ltd. | 99 New Elephant Road, Dhaka 1205
       UpdatedAt set - row_count = 1 - value trimmed
5. POST /Settings  blank BusinessName    -> 200, "The Business name field is required."
6. Direct INSERT of a second row via SQL
   -> ERROR: new row for relation "ShopSettings"
             violates check constraint "CK_ShopSettings_SingleRow"
```

**7. Role restriction — the previously untested path.** The admin's role assignment was temporarily
removed, the session re-established, and the app exercised without the `Admin` role:

```
/Settings    => 302  /Account/AccessDenied?ReturnUrl=%2FSettings
/Categories  => 302  /Account/AccessDenied?ReturnUrl=%2FCategories
/Brands      => 302  /Account/AccessDenied?ReturnUrl=%2FBrands
/            => 200                       (dashboard stays open to any signed-in user)
Settings tab occurrences in nav HTML: 0   (hidden, not merely blocked)
```

The role assignment was then restored and re-confirmed (`admin | Admin`), and full access verified:
`/`, `/Categories`, `/Brands`, `/Settings` all `200`, with the Settings tab rendering its active state.

So the restriction holds in **both** directions: the link is hidden from non-admins, and the URL is
refused if typed directly.

## 11.8 Architecture compliance

| Rule | Result |
|---|---|
| Domain has no outward dependency | `ShopSettings` is a plain entity on `BaseEntity` |
| Application has no Web dependency | both handlers use `IApplicationDbContext` only |
| Infrastructure implements Application abstractions | `ApplicationDbContext.ShopSettings` |
| Controllers stay thin | load, bind, call, redirect — no EF, no rules |
| Business rules server-side | single-row rule in the database; trimming in the handler |
| No new abstractions | no settings service, no options wrapper, no cache |
| No new packages | zero |

## 11.9 Known limitations

- ~~The business name is not displayed anywhere yet.~~ **Done** — see §11.10.
- **No audit trail.** `UpdatedAt` records *when* the settings changed, not *who* changed them.
  `ICurrentUserService` exists and is still unused; wiring `UpdatedBy` is a Phase 10 concern.
- **No Cashier user exists.** §11.7 proved the restriction by removing the admin's role, which is
  equivalent for authorization purposes, but a real Cashier account has still never signed in.

---

## 11.10 Displaying the business name

The point of storing a business name is that the application uses it. The header previously showed a
hard-coded "Optical Shop", so a saved name had no visible effect. Now:

| Place | Shows |
|---|---|
| Header title | `BusinessName` |
| Header subtitle | `Address` |
| Browser tab | `<page title> - <BusinessName>` |
| Sign-in card heading | `BusinessName` |
| Sign-in page title | `Sign in - <BusinessName>` |

**Implementation** — `@inject` in `_Layout.cshtml` and `Login.cshtml`:

```csharp
@inject Optical.Application.Features.Settings.GetShopSettingsHandler ShopSettingsQuery
@{
    var shop = await ShopSettingsQuery.HandleAsync(Context.RequestAborted);
    var businessName = shop?.BusinessName ?? "Optical Shop";
    var businessSubtitle = shop?.Address ?? "Optical Shop Management v1.0";
}
```

**Why `@inject` rather than a ViewComponent.** A ViewComponent is the textbook answer, but it costs a
class plus a `Views/Shared/Components/.../Default.cshtml` file to render six lines of chrome, and it
still could not set `<title>` — the layout would need the value anyway. One injected handler covers
the header and both titles in one place. The dependency direction is unchanged: the view calls an
Application handler, never EF.

**Fallbacks.** Until settings are saved, the header reads `Optical Shop` /
`Optical Shop Management v1.0`. The sign-in page is anonymous and renders before any authentication,
so it must work with no settings row at all — verified below.

**Cost.** One small `AsNoTracking` projection per rendered page. No cache: for a single shop this is a
primary-key-sized read, and caching would introduce a staleness question that is not worth answering
yet. Revisit only if a page ever renders slowly.

**Long addresses** are truncated with an ellipsis in the header (`max-width: 32ch`) and carry the full
text in a `title` tooltip, so the header cannot be stretched by a long address.

### Verification

```
Settings row: Vision Care Optical Ltd. | 99 New Elephant Road, Dhaka 1205

Login (anonymous)   <title>Sign in - Vision Care Optical Ltd.</title>
                    heading "Vision Care Optical Ltd."
Dashboard           <title>Dashboard - Vision Care Optical Ltd.</title>
                    brand title    "Vision Care Optical Ltd."
                    brand subtitle "99 New Elephant Road, Dhaka 1205"

1. Renamed to "Dhaka Eye Care" through the UI
   -> header immediately shows "Dhaka Eye Care" / "12 Mirpur Road, Dhaka 1207"
2. Deleted the settings row
   -> header falls back to "Optical Shop" / "Optical Shop Management v1.0"
   -> login page still returns 200 and shows "Optical Shop"
3. Restored through the UI -> Id=1 | Vision Care Optical Ltd. | 99 New Elephant Road, Dhaka 1205
```
