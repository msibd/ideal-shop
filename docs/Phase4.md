# Phase 4 — Product

Status: **Complete** — build passes, migration applied, every rule verified against the running application
Date: 2026-09-04 · *updated 2026-09-04 — user account panel: Change password §10, Profile name §11, Profile Management UI §12*

> This file previously held the database bring-up and dashboard UI write-up. That work is not a
> numbered phase and now lives in `docs/Platform-UI-And-Database.md`.

---

## 1. Scope

Implemented:

- Product entity with SKU, name, category, brand, purchase price, sale price, reorder level, status
- Product list with search and pagination
- Product details
- Create / Edit
- Active / Inactive
- Category and Brand selection
- Full validation, relationships, indexes, decimal precision, delete behaviour

Not implemented, per the phase brief:

- inventory transactions, stock quantity, purchases, POS
- prescriptions, lenses, frames as separate concepts
- complex pricing (discounts, tiers, tax)
- product images, variants, barcodes
- delete (status change covers the MVP need)

**Stock quantity is deliberately absent from `Product`** — `InventoryItem` owns it in Phase 5.

---

## 2. Design decisions

| Decision | Choice | Reason |
|---|---|---|
| `BrandId` | **Required** (not nullable) | The field list gives `BrandId`, not `BrandId?`. One-line change to `int?` if unbranded products are ever needed. |
| Stock quantity | **Not on Product** | Explicit instruction. Inventory owns stock; duplicating it would create two sources of truth. |
| Delete behaviour | `DeleteBehavior.Restrict` on both FKs | A category or brand that still has products cannot be deleted out from under them. `Cascade` would silently destroy the catalogue; `SetNull` is impossible on non-nullable keys. |
| Price floors | ViewModel `[Range]` **and** database `CHECK` | The server is the source of truth (§11). A `[Range]` attribute is not a guarantee — a direct SQL write must fail too. |
| Pagination | Feature-specific `ProductListResult` record | Products are the first genuinely large list (§16). A generic `PagedResult<T>` would be the start of a framework; this record is 4 lines and belongs to one feature. |
| Details vs Edit | **One** `GetProductById` slice returning both ids and names | Details renders the names, Edit binds the ids. Two near-identical query slices would be duplication. |
| Dropdown contents | Active categories/brands, **plus** the ids already on the product being edited | Otherwise editing a product whose category was later switched off would silently lose the selection. |
| Shared rule | `ProductRules.EnsureCategoryAndBrandExistAsync` | The one rule Create and Update genuinely share. Not a validation framework — one internal static method. |
| Authorization | `[Authorize(Roles = AppRoles.Admin)]` | Consistent with Categories/Brands. POS gets its own read path in Phase 7. |

---

## 3. Layer-by-layer changes

### 3.1 Optical.Domain

**`Entities/Product.cs`** (new) — the only change to this layer.

```csharp
public class Product : BaseEntity
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;

    // Stock quantity deliberately lives on InventoryItem, not here.
}
```

`Id`, `CreatedAt` and `UpdatedAt` come from `BaseEntity`.

Navigation properties are one-directional: `Product` points at `Category` and `Brand`, but neither
carries an `ICollection<Product>`. Nothing needs the reverse direction, and a back-collection invites
accidental full-table loads.

The comment about stock is left in the source on purpose — it is the kind of decision a future reader
would otherwise "fix" by adding a `Quantity` column.

### 3.2 Optical.Application

```
Optical.Application/
├── DependencyInjection.cs                (edited: +5 registrations)
├── Abstractions/Persistence/
│   └── IApplicationDbContext.cs          (edited: + DbSet<Product>)
└── Features/Products/                    (new)
    ├── GetProducts.cs
    ├── GetProductById.cs
    ├── GetProductFormOptions.cs
    ├── CreateProduct.cs
    ├── UpdateProduct.cs
    └── ProductRules.cs
```

**`GetProducts.cs`** — list, search and pagination in one slice.

```csharp
public sealed record ProductListResult(
    IReadOnlyList<ProductListItem> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
```

Search matches **name or SKU**, case-insensitively. The projection reads `p.Category.Name` and
`p.Brand.Name`, so EF emits a single query with two joins — no N+1, and the `Product` entity is never
materialised:

```csharp
.Select(p => new ProductListItem(
    p.Id, p.Sku, p.Name, p.Category.Name, p.Brand.Name,
    p.PurchasePrice, p.SalePrice, p.ReorderLevel, p.IsActive))
```

`page < 1` is clamped to 1 rather than throwing — a hand-edited query string is not an exception.

**`GetProductById.cs`** — returns `ProductDetails` carrying **both** the foreign keys and the display
names, so Details and Edit share one query.

**`GetProductFormOptions.cs`** — dropdown data:

```csharp
.Where(c => c.IsActive || c.Id == includeCategoryId)
.Select(c => new SelectOption(c.Id, c.IsActive ? c.Name : c.Name + " (inactive)"))
```

New products can only be given active categories and brands; an inactive one already assigned stays
visible and is labelled, so editing an old product never silently reassigns it.

**`CreateProduct.cs` / `UpdateProduct.cs`** — trim, check SKU uniqueness case-insensitively, verify
the category and brand exist, then write. Update excludes the product itself from the duplicate
check (`p.Id != command.Id`) and throws `NotFoundException` when the row is gone.

**`ProductRules.cs`** — `internal static`, one method, shared by both commands:

```csharp
if (!await db.Categories.AnyAsync(c => c.Id == categoryId, ct))
    throw new DomainException("The selected category no longer exists.");
```

### 3.3 Optical.Infrastructure

**`Persistence/Configurations/ProductConfiguration.cs`** (new)

```csharp
builder.Property(p => p.Sku).IsRequired().HasMaxLength(50);
builder.Property(p => p.Name).IsRequired().HasMaxLength(150);

builder.Property(p => p.PurchasePrice).HasPrecision(18, 2);
builder.Property(p => p.SalePrice).HasPrecision(18, 2);

builder.HasIndex(p => p.Sku).IsUnique();

builder.HasOne(p => p.Category).WithMany()
    .HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);

builder.HasOne(p => p.Brand).WithMany()
    .HasForeignKey(p => p.BrandId).OnDelete(DeleteBehavior.Restrict);

builder.ToTable(t =>
{
    t.HasCheckConstraint("CK_Products_PurchasePrice", "\"PurchasePrice\" >= 0");
    t.HasCheckConstraint("CK_Products_SalePrice",     "\"SalePrice\" >= 0");
    t.HasCheckConstraint("CK_Products_ReorderLevel",  "\"ReorderLevel\" >= 0");
});
```

**`ApplicationDbContext`** exposes `Products`. Timestamps are stamped by the existing
`SaveChangesAsync` override — no per-entity work.

**Migration `20260904_AddProduct`** — live schema:

```
    Column     |            Type             | Nullable
---------------+-----------------------------+----------
 Id            | integer (identity)          | not null
 Sku           | character varying(50)       | not null
 Name          | character varying(150)      | not null
 CategoryId    | integer                     | not null
 BrandId       | integer                     | not null
 PurchasePrice | numeric(18,2)               | not null
 SalePrice     | numeric(18,2)               | not null
 ReorderLevel  | integer                     | not null
 IsActive      | boolean                     | not null
 CreatedAt     | timestamp with time zone    | not null
 UpdatedAt     | timestamp with time zone     |

Indexes:
    "PK_Products" PRIMARY KEY, btree ("Id")
    "IX_Products_Sku" UNIQUE, btree ("Sku")
    "IX_Products_BrandId" btree ("BrandId")
    "IX_Products_CategoryId" btree ("CategoryId")
Check constraints:
    "CK_Products_PurchasePrice" CHECK ("PurchasePrice" >= 0::numeric)
    "CK_Products_SalePrice"     CHECK ("SalePrice" >= 0::numeric)
    "CK_Products_ReorderLevel"  CHECK ("ReorderLevel" >= 0)
Foreign-key constraints:
    "FK_Products_Brands_BrandId"        REFERENCES "Brands"("Id")     ON DELETE RESTRICT
    "FK_Products_Categories_CategoryId" REFERENCES "Categories"("Id") ON DELETE RESTRICT
```

The two FK indexes are created by EF automatically and are what keep the list query's joins cheap.

### 3.4 Optical.Web

| File | Change |
|---|---|
| `ViewModels/ProductFormViewModel.cs` | new — fields + `SelectListItem` collections for the dropdowns |
| `Controllers/ProductsController.cs` | new — `Index`, `Details`, `Create` (GET/POST), `Edit` (GET/POST) |
| `Views/Products/Index.cshtml` | new — search, table, pagination footer |
| `Views/Products/Details.cshtml` | new — details, pricing, record card |
| `Views/Products/Create.cshtml` | new |
| `Views/Products/Edit.cshtml` | new — Create plus hidden `Id` and the Active checkbox |
| `Views/Shared/_Layout.cshtml` | edited — **Products** nav tab |
| `wwwroot/css/site.css` | edited — one `.money { font-variant-numeric: tabular-nums; }` rule |

`CategoryId` and `BrandId` are `int?` on the view model on purpose. A non-nullable `int` defaults to
`0`, which silently passes `[Required]`; nullable plus `[Required]` makes "nothing selected" an
actual validation error with the message *"Please select a category."*

The controller keeps one private helper, `FillOptionsAsync`, called on every path that re-renders the
form — otherwise a validation failure would redisplay the page with empty dropdowns.

Money and counts are right-aligned with `tabular-nums`, so decimal points line up down a column.
Category and Brand render as the existing `chip-category` / `chip-brand` chips, reusing the validated
colour system with no new colours.

---

## 4. Validation rules

| Rule | Enforced where |
|---|---|
| SKU required, max 50 | ViewModel + `varchar(50) NOT NULL` |
| **SKU unique** (case-insensitive) | `CreateProductHandler` / `UpdateProductHandler` |
| SKU unique (exact) | PostgreSQL unique index — backstop against a race |
| Name required, max 150 | ViewModel + `varchar(150) NOT NULL` |
| **Category selected** | `[Required]` on `int?` |
| **Category exists** | `ProductRules` → `DomainException` |
| **Brand selected / exists** | same |
| **Purchase price >= 0** | `[Range]` + `CK_Products_PurchasePrice` |
| **Sale price >= 0** | `[Range]` + `CK_Products_SalePrice` |
| **Reorder level >= 0** | `[Range]` + `CK_Products_ReorderLevel` |
| SKU / name trimmed | handler, before saving |
| Admin only | `[Authorize(Roles = AppRoles.Admin)]` |
| Anti-forgery on POST | global filter from Phase 2 |

Same known limitation as Category/Brand: the case-insensitive SKU check lives in the handler while
the database index is case-sensitive, so two *simultaneous* requests could in theory land `ABC-1` and
`abc-1`. A case-insensitive database constraint needs `citext` or an expression index — not worth it
for a single shop, and recorded rather than ignored.

---

## 5. Request flow

```
GET /Products?search=frame&page=2
  -> GetProductsHandler
       AsNoTracking + Where(name or sku contains) + Count + Skip/Take + projection
  -> ProductListResult (items, page, pageSize, totalCount)

GET /Products/Details/5      -> GetProductByIdHandler -> 404 when null
GET /Products/Create         -> empty form + GetProductFormOptionsHandler
GET /Products/Edit/5         -> GetProductByIdHandler + options (including current ids)

POST /Products/Create|Edit             [anti-forgery enforced globally]
  ModelState invalid   -> refill dropdowns, redisplay
  duplicate SKU        -> DomainException -> ModelState -> refill, redisplay
  missing category or brand -> DomainException -> ModelState -> refill, redisplay
  row deleted meanwhile (Edit) -> NotFoundException -> error page
  ok  -> SaveChangesAsync (CreatedAt / UpdatedAt stamped) -> TempData -> redirect to Index

Signed in without the Admin role -> 302 /Account/AccessDenied
```

---

## 6. Verification

Every check below was run against the **live application and database**.

```
dotnet build Optical.slnx        -> Build succeeded. 0 Warning(s), 0 Error(s)
dotnet ef migrations add AddProduct -> OK
dotnet ef database update        -> Applied 20260904_AddProduct
```

| # | Test | Result |
|---|---|---|
| 1 | `GET /Products/Create` | 200; dropdowns list **Frames, Sunglasses / Oakley, Ray-Ban** |
| 2 | Inactive category "Contact Lens" offered? | **0 occurrences** — correctly excluded |
| 3 | Create `FRM-0012`, 1200.50 / 1999.99, reorder 5 | 302; row written with exact decimals |
| 4 | Create duplicate SKU in different case (`frm-0012`) | 200 + *"SKU "frm-0012" is already used by another product."* |
| 5 | Negative purchase price | 200 + *"Purchase price cannot be negative."* |
| 6 | No category selected | 200, redisplayed with error |
| 7 | Non-existent `CategoryId=99999` | 200 + *"The selected category no longer exists."* |
| 8 | `GET /Products/Details/1` | 200; SKU, FRAMES / RAY-BAN chips, 1,200.50 / 1,999.99 |
| 9 | `GET /Products/Details/99999` | **404** |
| 10 | Edit: rename, reprice to 2100.00, reorder 8, set inactive | 302; DB shows `IsActive=f`, `UpdatedAt` set |
| 11 | Edit keeping its **own** SKU | 302 — correctly *not* reported as a duplicate |
| 12 | Direct SQL `UPDATE ... SalePrice = -1` | `ERROR: violates check constraint "CK_Products_SalePrice"` |
| 13 | Direct SQL `DELETE FROM "Categories" WHERE "Id"=1` | `ERROR: violates RESTRICT setting of foreign key constraint` |
| 14 | Pagination with 25 products | page 1 = 20 rows, page 2 = 5 rows, *"Page 2 of 2 — 25 product(s)"*, Previous shown / Next correctly absent |
| 15 | Search `classic` (lowercase, name) | 1 row — *Classic Metal Frame v2* |
| 16 | Search `TMP-7` (SKU) | 1 row |
| 17 | Search `zzzznothing` | *"No products found."* |

Tests 12 and 13 matter most: they prove the rules survive a write that bypasses the application
entirely, which is what "the server is the source of truth" has to mean.

The 24 temporary rows used for test 14 were deleted afterwards; one real product remains.

---

## 7. Architecture compliance

| Rule | Result |
|---|---|
| Domain has no outward dependency | `Product` is a plain entity on `BaseEntity` |
| Application has no Web dependency | handlers use `IApplicationDbContext` only |
| Infrastructure implements Application abstractions | `ApplicationDbContext.Products` |
| Controllers stay thin | bind, call handler, redirect; one private dropdown helper |
| No DB access from controllers | no EF type anywhere in Web |
| Business rules server-side | uniqueness, existence, trimming in handlers; floors in the database |
| `AsNoTracking` on read queries | list, details and form options |
| Projection instead of entity loading | all three read slices |
| Pagination on a large list | 20 per page |
| Decimal precision for money | `numeric(18,2)`, never float/double |
| No banned abstractions (§18) | no MediatR, AutoMapper, Repository, UnitOfWork, generic result type |
| No new packages | zero |

---

## 8. Known limitations

- Case-insensitive SKU uniqueness is application-level only (§4).
- No delete. Products are switched inactive instead — correct while sales history will reference them.
- Products are Admin-only. A cashier cannot browse the catalogue yet; POS adds its own read path in
  Phase 7.
- The list has no category or brand filter, only free-text search. Straightforward to add if the
  catalogue grows.
- `PurchasePrice` is stored on the product as a reference figure. Phase 5 records the actual price
  paid on each `PurchaseItem` — the two are intentionally separate.

---

## 9. Next phase

Phase 5 — Supplier, Purchase, PurchaseItem, InventoryItem, stock increase, stock adjustment and
low-stock visibility. That is where `Product` gains its stock counterpart, and where the first
multi-table atomic transaction appears.

**Not started.**

---

# 10. Addendum — Change password

Added after the Product work, on request: **users change their own password from the user panel** —
the avatar dropdown in the top-right of the header.

Recorded here rather than in Phase 2 because Phase 2 is a closed record of what authentication looked
like then; this is new work. It does, however, close a gap Phase 2 named in its own scope list
("user-management UI" was explicitly out of scope there).

## 10.1 Design decisions

| Decision | Choice | Reason |
|---|---|---|
| Whose password | **Only the signed-in user's own** | The user id comes from `ICurrentUserService`, i.e. the authentication cookie — never from the request body. There is no id parameter to tamper with. |
| Who may use it | **Any authenticated role** | A cashier must be able to change their own password. No `[Authorize(Roles = ...)]`; the global fallback policy supplies the authentication requirement. |
| Slice vs pass-through | **A real handler** | Unlike Login (Phase 2), this handler has work of its own: resolve the current user, reject a no-op change, then delegate. It is not a forwarding shim. |
| Result type | `ChangePasswordOutcome(bool Succeeded, IReadOnlyList<string> Errors)` | Identity can return *several* messages at once (too short, no digit, no uppercase). An enum would have to collapse them; this record carries them through unchanged. |
| Error text | Identity's own `IdentityError.Description`, shown as-is | Those strings are already written for end users ("Incorrect password.", "Passwords must have at least one digit."). Re-writing them would mean re-implementing the policy in a second place. |
| Session after change | `RefreshSignInAsync` | Changing a password rotates the security stamp; without the refresh the user is silently signed out by their own successful action. |
| Admin resetting others | **Not implemented** | Not requested. It is a different feature with different risks (no current-password check), and belongs with a proper user-management screen. |

## 10.2 Optical.Domain

**No changes.** Passwords are an identity concern, not a domain concept — the same reasoning that
kept `AppRoles` out of Domain in Phase 2.

## 10.3 Optical.Application

```
Optical.Application/
├── DependencyInjection.cs                        (edited: +1 registration)
├── Abstractions/Identity/
│   ├── IIdentityService.cs                       (edited: +1 method)
│   └── ChangePasswordOutcome.cs                  (new)
└── Features/Account/                             (new)
    └── ChangePassword.cs
```

**`Abstractions/Identity/ChangePasswordOutcome.cs`** (new)

```csharp
public sealed record ChangePasswordOutcome(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static ChangePasswordOutcome Success { get; } = new(true, []);
    public static ChangePasswordOutcome Failure(params string[] errors) => new(false, errors);
}
```

Sits beside `LoginResult` in the same folder — both are the shape of an identity operation's answer.

**`Abstractions/Identity/IIdentityService.cs`** gains one method:

```csharp
Task<ChangePasswordOutcome> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
```

**`Features/Account/ChangePassword.cs`** (new) — the vertical slice: command record + handler.

```csharp
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

public sealed class ChangePasswordHandler(
    ICurrentUserService currentUser,
    IIdentityService identityService)
{
    public async Task<ChangePasswordOutcome> HandleAsync(
        ChangePasswordCommand command, CancellationToken cancellationToken = default)
    {
        // A user may only ever change their own password: the id comes from the
        // authentication cookie, never from the request body.
        var userId = currentUser.UserId
            ?? throw new DomainException("You must be signed in to change your password.");

        if (string.Equals(command.CurrentPassword, command.NewPassword, StringComparison.Ordinal))
        {
            return ChangePasswordOutcome.Failure(
                "The new password must be different from the current password.");
        }

        return await identityService.ChangePasswordAsync(userId, command.CurrentPassword, command.NewPassword);
    }
}
```

**This is the first consumer of `ICurrentUserService`.** The abstraction was created in Phase 2 to
match CLAUDE.md §7 and had been unused ever since; it now earns its place, and it is what makes the
"own password only" guarantee structural rather than a check someone could forget.

The command deliberately carries **no user id**. There is no parameter an attacker could swap.

## 10.4 Optical.Infrastructure

**`Identity/IdentityService.cs`** — one method added, no new file:

```csharp
public async Task<ChangePasswordOutcome> ChangePasswordAsync(
    string userId, string currentPassword, string newPassword)
{
    var user = await userManager.FindByIdAsync(userId);
    if (user is null)
    {
        return ChangePasswordOutcome.Failure("Your account could not be found.");
    }

    var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
    if (!result.Succeeded)
    {
        return new ChangePasswordOutcome(false, result.Errors.Select(e => e.Description).ToList());
    }

    // Changing the password rotates the security stamp, which would otherwise
    // invalidate the current cookie and sign the user out mid-request.
    await signInManager.RefreshSignInAsync(user);

    return ChangePasswordOutcome.Success;
}
```

`UserManager.ChangePasswordAsync` verifies the current password and applies the configured policy
(min 8, upper, lower, digit — set in Phase 2) in one call. Nothing re-implements hashing or policy.

**No migration.** `AspNetUsers` already stores `PasswordHash` and `SecurityStamp`; both are simply
rewritten.

## 10.5 Optical.Web

| File | Change |
|---|---|
| `ViewModels/ChangePasswordViewModel.cs` | new — `CurrentPassword`, `NewPassword` (min 8), `ConfirmPassword` with `[Compare]` |
| `Controllers/AccountController.cs` | edited — `ChangePassword` GET + POST; takes `ChangePasswordHandler` alongside `IIdentityService` |
| `Views/Account/ChangePassword.cshtml` | new — form card + requirements card |
| `Views/Shared/_Layout.cshtml` | edited — dropdown item for the password screen (later folded into a single **Profile Management** entry, §12) |
| `Views/Shared/_PasswordToggle.cshtml` | new — show/hide button for a password field (§10.10) |
| `wwwroot/js/site.js` | edited — the delegated show/hide handler (§10.10) |
| `wwwroot/css/site.css` | edited — `.password-toggle` styles (§10.10) |

The new actions carry **no `[AllowAnonymous]`**, unlike every other action on `AccountController`.
That matters: Phase 2's fix moved `[AllowAnonymous]` from the class onto individual actions, so
`ChangePassword` inherits the global fallback policy and requires authentication. Had the attribute
still been at class level, this screen would have been publicly reachable.

The controller maps each outcome error onto `ModelState`, then redirects to the dashboard with
`TempData["Success"]` on success — Post-Redirect-Get, so a refresh cannot resubmit.

The view reuses the existing design system: `app-card`, `app-card-header`, `stat-hint`, `_InfoIcon`.
The only CSS added later was for the show/hide button (§10.10). The right-hand card lists the password rules so a user is not guessing.

## 10.6 Request flow

```
Avatar dropdown -> Change password

GET /Account/ChangePassword
  not signed in -> 302 /Account/Login?ReturnUrl=%2FAccount%2FChangePassword
  signed in     -> 200, empty form (any role)

POST /Account/ChangePassword          [anti-forgery enforced globally]
  ModelState invalid (blank, < 8 chars, confirmation mismatch)
      -> redisplay with field errors
  ChangePasswordHandler
      no user id on the cookie      -> DomainException
      new password == current       -> Outcome.Failure(...)      -> ModelState
      UserManager.ChangePasswordAsync
          wrong current password    -> "Incorrect password."     -> ModelState
          policy violation          -> Identity's descriptions   -> ModelState
          ok -> hash + security stamp rewritten
             -> RefreshSignInAsync  (session kept alive)
  -> TempData["Success"] -> redirect to dashboard
```

## 10.7 Verification — run against the live application

```
dotnet build Optical.slnx  -> Build succeeded. 0 Warning(s), 0 Error(s)

anonymous GET /Account/ChangePassword
    => 302 /Account/Login?ReturnUrl=%2FAccount%2FChangePassword
dropdown link present on every page  => href="/Account/ChangePassword"
signed-in GET                        => 200

1. wrong current password        -> 200, "Incorrect password."
2. confirmation mismatch         -> 200, "The passwords do not match."
3. new password == current       -> 200, "The new password must be different
                                          from the current password."
4. new password "abc" (too short)-> 200, "The new password must be at least 8 characters."

5. happy path (Admin#12345 -> Temp#Pass2026)   => 302 -> /
     PasswordHash   AQAAAAIAAYagAAAAEM0i... -> AQAAAAIAAYagAAAAED/n...   (changed)
     SecurityStamp  UHRQBUQGUJ3R            -> AWFVJDKD6PPU              (rotated)

6. same cookie, GET /            => 200 + "Your password has been changed."
     (proves RefreshSignInAsync kept the session alive despite the stamp rotation)

7. login with the OLD password   => 200  REJECTED
   login with the NEW password   => 302  ACCEPTED

8. changed back to Admin#12345, re-login verified => 302
```

Test 6 is the one that matters most: without `RefreshSignInAsync` a successful password change would
sign the user out, which reads as a failure to the person doing it.

The admin password was restored to its original value at the end of the run.

## 10.8 Architecture compliance

| Rule | Result |
|---|---|
| Domain has no outward dependency | untouched |
| Application has no Web dependency | handler uses two Application abstractions only |
| Infrastructure implements Application abstractions | `IdentityService.ChangePasswordAsync` |
| Vertical slice in Features/ | `Features/Account/ChangePassword.cs` — command + handler in one file |
| Controllers stay thin | validate, call handler, map errors, redirect |
| No DB access from controllers | no EF or `UserManager` type in Web |
| Password hashing via Identity | `UserManager.ChangePasswordAsync`; nothing hand-rolled |
| No passwords in logs | nothing is logged on this path |
| No new abstractions | one method on an existing interface, one result record |
| No new packages, no migration | zero |

## 10.9 Known limitations

- **An administrator cannot reset another user's password.** Only self-service exists. That is a
  different feature (no current-password check to lean on) and belongs with a user-management screen,
  which does not exist yet.
- **No "forgot password" flow.** Phase 2 excluded it deliberately — it needs email infrastructure and
  token providers (`AddDefaultTokenProviders()` is still not registered). If an account is locked out
  of its own password, recovery is manual.
- **No password history.** A user may alternate between two passwords; only "different from the
  current one" is enforced.
- **Changing a password does not sign out that user's other sessions.** `RefreshSignInAsync` updates
  the cookie in hand; other browsers keep working until their cookie expires.

---

## 10.10 Show / hide password

All three fields carry a show/hide button on the right-hand edge of the input, so a typed password can
be checked before submitting. This matters most on **Confirm new password**, where a mismatch is
otherwise only discoverable by failing.

### Markup — one partial, used three times

`Views/Shared/_PasswordToggle.cshtml` (new) sits inside a Bootstrap `.input-group`, directly after the
input:

```html
<div class="input-group">
    <input asp-for="NewPassword" class="form-control" autocomplete="new-password" />
    <partial name="_PasswordToggle" />
</div>
<span asp-validation-for="NewPassword" class="text-danger small d-block mt-1"></span>
```

The partial holds a `type="button"` element and **two** inline SVGs — an open eye (`data-icon="show"`)
and a struck-through eye (`data-icon="hide"`, `hidden` initially). `type="button"` is not incidental:
the default inside a `<form>` is `type="submit"`, so without it every click would submit the form.

The validation `<span>` moved outside the input-group and gained `d-block mt-1` — inside an
input-group it would have been treated as another group member and laid out on the same row.

### Behaviour — delegated, in `site.js`

```js
document.addEventListener('click', function (event) {
    const toggle = event.target.closest('[data-password-toggle]');
    if (!toggle) return;

    const field = toggle.closest('.input-group')?.querySelector('input');
    if (!field) return;

    const reveal = field.type === 'password';
    field.type = reveal ? 'text' : 'password';

    toggle.setAttribute('aria-pressed', String(reveal));
    toggle.setAttribute('aria-label', reveal ? 'Hide password' : 'Show password');
    ...
    field.focus();   // keep the caret where the user was typing
});
```

One delegated listener on `document`, not three per-field bindings: any page that uses the partial gets
the behaviour with no extra wiring, including markup re-rendered after a validation failure. Plain
DOM APIs, no jQuery — CLAUDE.md §15 says jQuery only where it earns its place, and this does not need
it.

### Accessibility

- the button is a real `<button>`, so it is keyboard reachable and Enter/Space activate it
- `aria-pressed` flips `false` -> `true`, announcing it as a toggle rather than an action
- `aria-label` changes between "Show password" and "Hide password" so the state is spoken, not implied
  by the icon
- the icons are `aria-hidden="true"` — the label carries the meaning
- `:focus-visible` outline uses the same token as the rest of the app
- focus returns to the input after toggling, so typing continues uninterrupted

### Verification

```
GET /Account/ChangePassword                     => 200
toggle buttons rendered                         => 3
.input-group wrappers                           => 3
type="button" on every toggle                   => 3   (cannot submit the form)
aria-label="Show password" aria-pressed="false"  present
icon pairs: 3 x data-icon="show" visible, 3 x data-icon="hide" hidden
all three inputs render type="password" on load, client-validation attributes intact
/js/site.js  => 200, contains the handler       node --check site.js -> parses OK
/css/site.css => 200, .password-toggle rules present

toggle logic exercised directly:
  1st click -> type=text     aria-pressed=true   show hidden, hide shown
  2nd click -> type=password aria-pressed=false  show shown,  hide hidden

regression: POST with a wrong current password still returns 200 with
"Incorrect password.", and all 3 toggles survive the redisplay
```

### Not done

The **sign-in** page has no toggle. It was not in the request, and a shared password field on a login
form is a slightly different judgement call (shoulder-surfing risk at a shop counter). The partial is
reusable, so adding it there is a two-line change if wanted.

---

# 11. Addendum — Profile name

Added on request: **a user changes their own display name from the user panel** — the same avatar
dropdown that holds Change password (§10).

This finally puts `ApplicationUser.FullName` to work. The field was created in Phase 2 and, until now,
was written once by the seeder and never read, edited, or displayed anywhere.

## 11.1 Design decisions

| Decision | Choice | Reason |
|---|---|---|
| Which "name" | **`FullName`** (display name), not `UserName` | `UserName` is the sign-in credential: changing it invalidates what the user types to log in and collides with the unique index. "Change my name" on a profile page means the display name. |
| Whose name | **Only the signed-in user's own** | Id comes from `ICurrentUserService`; the command carries no id, so there is nothing to tamper with — the same guarantee as §10. |
| Who may use it | **Any authenticated role** | A cashier owns their own name too. |
| Where it shows | **Header, replacing the username**, falling back to the username when blank | A name nobody can see is a name nobody will set. This is the same mistake made with the business name and corrected in `Platform-UI-And-Database.md` §15.2 — not repeated here. |
| Username still visible | Dropdown header keeps "Signed in as **admin**" | The sign-in credential must stay discoverable once the header stops showing it. |
| Refreshing the header | `RefreshSignInAsync` after the update | Same mechanism as the password change: re-issues the cookie so the new name appears on the very next request. |
| Column length | `varchar(100)`, required | The property was `text` with no constraint. A display name has no business being unbounded. |

## 11.2 Optical.Domain

**No changes.** A user's display name is an identity concern, not a domain concept.

## 11.3 Optical.Application

```
Optical.Application/
├── DependencyInjection.cs                 (edited: +2 registrations)
├── Abstractions/Identity/
│   ├── IIdentityService.cs                (edited: +2 methods)
│   └── UserProfile.cs                     (new)
└── Features/Account/
    ├── GetProfile.cs                      (new)
    └── UpdateProfile.cs                   (new)
```

**`Abstractions/Identity/UserProfile.cs`** (new)

```csharp
public sealed record UserProfile(string UserName, string FullName, string? Email);
```

**`IIdentityService`** gains two members:

```csharp
Task<UserProfile?> GetProfileAsync(string userId);
Task UpdateFullNameAsync(string userId, string fullName);
```

**`Features/Account/GetProfile.cs`** and **`UpdateProfile.cs`** — two slices beside `ChangePassword.cs`
in the same feature folder. Both resolve the user from the cookie and refuse to work without one:

```csharp
var userId = currentUser.UserId
    ?? throw new DomainException("You must be signed in to change your name.");

await identityService.UpdateFullNameAsync(userId, command.FullName.Trim());
```

`UpdateProfileCommand` carries **only** `FullName`. As with `ChangePasswordCommand`, there is no user
id in the request body to swap.

No outcome record here, unlike `ChangePasswordOutcome`: a name change has no policy to violate. The
only failure is "the account vanished mid-request", which is a genuine `NotFoundException`, not a
message to render on a form.

## 11.4 Optical.Infrastructure

**`Identity/IdentityService.cs`** — two methods added, no new file:

```csharp
public async Task<UserProfile?> GetProfileAsync(string userId)
{
    var user = await userManager.FindByIdAsync(userId);

    return user is null
        ? null
        : new UserProfile(user.UserName ?? string.Empty, user.FullName, user.Email);
}

public async Task UpdateFullNameAsync(string userId, string fullName)
{
    var user = await userManager.FindByIdAsync(userId)
        ?? throw new NotFoundException("Your account could not be found.");

    user.FullName = fullName;

    var result = await userManager.UpdateAsync(user);
    if (!result.Succeeded)
    {
        throw new DomainException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    // Re-issue the cookie so the header shows the new name on the very next request.
    await signInManager.RefreshSignInAsync(user);
}
```

**`Persistence/Configurations/ApplicationUserConfiguration.cs`** (new) — the first EF configuration for
an Identity entity:

```csharp
builder.Property(u => u.FullName).IsRequired().HasMaxLength(100);
```

**Migration `20260904170517_ConstrainUserFullName`** — `AspNetUsers.FullName`: `text` ->
`character varying(100)`, NOT NULL.

EF flagged this as *"An operation was scaffolded that may result in the loss of data"* — correct, since
narrowing a column truncates anything longer. Checked before applying:

```
 UserName |       FullName       | len
----------+----------------------+-----
 admin    | System Administrator |  20

rows with length > 100: 0
```

Safe, then applied. The warning was verified rather than dismissed.

## 11.5 Optical.Web

| File | Change |
|---|---|
| `ViewModels/ProfileViewModel.cs` | new — `FullName` (required, 2-100); `UserName` and `Email` display-only |
| `Controllers/AccountController.cs` | edited — `Profile` GET + POST, and a `RestoreReadOnlyFieldsAsync` helper |
| `Views/Account/Profile.cshtml` | new — name form + read-only Account card |
| `Views/Shared/_Layout.cshtml` | edited — **Your profile** dropdown item (later folded into **Profile Management**, §12); header shows the display name |

**The header now reads the display name:**

```csharp
var isAuthenticated = User.Identity?.IsAuthenticated == true;
var profile = isAuthenticated ? await ProfileQuery.HandleAsync(Context.RequestAborted) : null;
var displayName = string.IsNullOrWhiteSpace(profile?.FullName)
    ? User.Identity?.Name ?? string.Empty
    : profile!.FullName;
```

The `isAuthenticated` guard matters: the handler throws when there is no signed-in user, and the layout
also renders the anonymous sign-in page. Without the guard, the login page would have thrown.

**`RestoreReadOnlyFieldsAsync`** exists for a specific reason: username and email are rendered as text,
not inputs, so they are **not** posted back. Without reloading them, a validation failure would
redisplay the Account card with blank values. Verified in test 1 below.

Reuses the existing design system with no new CSS: `app-card`, `app-table`, `chip-category` for the
role, `_InfoIcon` for the explanatory note.

## 11.6 Request flow

```
Avatar dropdown -> Your profile

GET /Account/Profile
  not signed in -> 302 /Account/Login?ReturnUrl=%2FAccount%2FProfile
  signed in     -> 200, form prefilled with FullName; username/email/role shown read-only

POST /Account/Profile                 [anti-forgery enforced globally]
  ModelState invalid -> reload read-only fields -> redisplay with errors
  UpdateProfileHandler
      no user id on the cookie -> DomainException
      account gone             -> NotFoundException
      ok -> trim -> UserManager.UpdateAsync
              -> RefreshSignInAsync   (header updates on the next request)
  -> TempData["Success"] -> redirect back to the profile page
```

## 11.7 Verification — run against the live application

```
dotnet build Optical.slnx  -> Build succeeded. 0 Warning(s), 0 Error(s)
dotnet ef database update  -> Applied 20260904170517_ConstrainUserFullName
                              AspNetUsers.FullName -> character varying(100), not null

anonymous GET /Account/Profile => 302 /Account/Login?ReturnUrl=%2FAccount%2FProfile
anonymous GET /Account/Login   => 200   (regression: the layout guard holds with no user)

header before any edit  -> "System Administrator"   (the seeded FullName, not "admin")
dropdown header         -> "Signed in as admin"     (username still discoverable)
dropdown items          -> /Account/Profile and /Account/ChangePassword
GET /Account/Profile    -> 200, value="System Administrator",
                           username "admin", role chip ADMIN

1. blank name              -> 200, "The Display name field is required."
                              read-only fields still populated after redisplay
2. one character           -> 200, "Display name must be between 2 and 100 characters."
3. "  Rahim Uddin  "       -> 302; stored as [Rahim Uddin]  (padding trimmed)
4. same cookie, GET /      -> header now "Rahim Uddin" + "Your name has been updated."
                              (proves RefreshSignInAsync re-issued the principal)
5. login as "admin"        -> 302   (the sign-in username was untouched)
6. restored to "System Administrator" -> verified in the database
```

Test 4 is the one that matters: without `RefreshSignInAsync` the name would have changed in the
database while the header kept showing the old one until the next sign-in.

Test 5 confirms the important boundary — this screen changes the *display* name and nothing about how
the user signs in.

## 11.8 Architecture compliance

| Rule | Result |
|---|---|
| Domain has no outward dependency | untouched |
| Application has no Web dependency | both handlers use two Application abstractions only |
| Infrastructure implements Application abstractions | `GetProfileAsync`, `UpdateFullNameAsync` |
| Vertical slice in Features/ | `Features/Account/{GetProfile,UpdateProfile}.cs` |
| Controllers stay thin | load, bind, call handler, redirect; one read-only-field helper |
| No DB access from controllers | no EF or `UserManager` type in Web |
| Business rules server-side | id from the cookie, trimming in the handler, length in the database |
| No new abstractions | two methods on an existing interface, one record |
| No new packages | zero |

## 11.9 Known limitations

- **The sign-in username cannot be changed.** Deliberate: it is the login credential and carries a
  unique index. Changing it is a different feature with a different risk profile.
- **Email is read-only.** It is displayed for reference but not editable — changing an email normally
  wants a confirmation step, and no mail infrastructure exists.
- **An administrator cannot rename another user.** Self-service only, same boundary as §10.9.
- **One extra query per rendered page.** The layout now looks up the profile alongside the shop
  settings. Both are primary-key reads; no cache, for the same reason given in
  `Platform-UI-And-Database.md` §15.2 — a cache would make the header show a stale name after an edit.
  Storing the name as a claim at sign-in would remove the query entirely; worth doing only if page
  rendering ever measures slow.

---

# 12. Addendum — Profile Management panel

A UI restructure of §10 and §11, on request. The account menu had grown two separate entries and the
profile page carried a redundant button to the other screen. Both are now one destination with two
tabs.

## 12.1 What changed

**Account dropdown** — two items collapse into one:

```
before                          after
------------------------        ------------------------
Signed in as admin              Signed in as admin
------------------------        ------------------------
Your profile                    Profile Management
Change password                 ------------------------
------------------------        Sign out
Sign out
```

**Page header** — both screens now carry the same tab strip beside the title:

```
Your profile                                    [ Profile ] [ Password ]
The name shown across the application
```

**Removed** — the "Change password" button that sat in the Profile page's side card. With a Password
tab one click away, a second route to the same screen is noise.

## 12.2 Design decision — links, not a JavaScript tab widget

The tabs look like a segmented control but each one is a **real page** with its own route, its own
form and its own POST:

| Tab | Route |
|---|---|
| Profile | `GET/POST /Account/Profile` |
| Password | `GET/POST /Account/ChangePassword` |

Merging both forms onto one page was rejected. Two forms sharing one `ModelState` means a failed
password change re-renders the name form as well, and a single POST endpoint would have to work out
which half was submitted. Keeping them as separate pages preserves what already works: each screen
validates independently, is bookmarkable, survives a refresh through Post-Redirect-Get, and needs no
JavaScript at all.

**No controller or Application change was required.** This is presentation only — the two routes,
their handlers and their slices are exactly as documented in §10 and §11.

## 12.3 Files

| File | Change |
|---|---|
| `Views/Shared/_AccountTabs.cshtml` | **new** — the tab strip, with the active tab derived from the current route |
| `Views/Account/Profile.cshtml` | edited — header becomes a flex row: title block left, tabs right; redundant button removed |
| `Views/Account/ChangePassword.cshtml` | edited — same header row |
| `Views/Shared/_Layout.cshtml` | edited — one **Profile Management** dropdown item |
| `wwwroot/css/site.css` | edited — `.tab-strip` / `.tab-link` |

The partial computes its own active state, so neither page passes it anything:

```csharp
var currentAction = ViewContext.RouteData.Values["action"] as string ?? string.Empty;

bool IsCurrent(string action) =>
    string.Equals(currentAction, action, StringComparison.OrdinalIgnoreCase);

string TabClass(string action) => IsCurrent(action) ? "tab-link active" : "tab-link";
```

Styling reuses existing tokens only — `--status-off-wash` for the track, `--surface` and
`--cat-1-ink` for the raised active tab. **No new colours were introduced**, so the validated palette
from `Platform-UI-And-Database.md` §6 still holds unchanged.

## 12.4 Accessibility

- the strip is a `<nav aria-label="Profile management">`, so it is announced as navigation rather than
  as an unlabelled group of links
- the active tab carries `aria-current="page"` — exactly one per page, verified below
- tabs are ordinary links: keyboard reachable, focusable, openable in a new tab
- `:focus-visible` outline uses the same token as the rest of the app
- the active tab is distinguished by **background and elevation**, not colour alone

`role="tablist"` was deliberately not used: that ARIA pattern promises arrow-key navigation between
panels in the same document, which is not what these are. Mislabelling links as tabs would make the
markup lie to a screen reader.

## 12.5 Verification — run against the live application

```
dotnet build Optical.slnx  -> Build succeeded. 0 Warning(s), 0 Error(s)

dropdown
  "Profile Management" occurrences        1
  separate dropdown link to ChangePassword 0   (folded in)
  "Sign out"                               1   (still present)

GET /Account/Profile           => 200
  tab-link active + aria-current="page" -> /Account/Profile
  tab-link                              -> /Account/ChangePassword
  aria-current="page" count: 1

GET /Account/ChangePassword    => 200
  tab-link                              -> /Account/Profile
  tab-link active + aria-current="page" -> /Account/ChangePassword

both tabs render on both pages: 2 and 2
redundant "Change password" button on the profile card: 0  (removed)
/css/site.css contains .tab-link rules

regression - the restructure did not disturb either form:
  POST /Account/Profile        blank name    -> 200 "The Display name field is required."
                                                 tabs survive redisplay (2), read-only fields kept
  POST /Account/ChangePassword wrong current -> 200 "Incorrect password."
                                                 tabs survive redisplay (2), 3 password toggles intact
```

## 12.6 Architecture compliance

| Rule | Result |
|---|---|
| Presentation-only change | no Domain, Application or Infrastructure file touched |
| Controllers stay thin | unchanged — no controller edit was needed |
| No new abstractions | one Razor partial |
| No new packages, no migration, no JavaScript | zero |
| Design system reused | no new colour tokens; validated palette unaffected |
