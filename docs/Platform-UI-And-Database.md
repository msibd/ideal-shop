# Platform — Database Bring-Up, App Shell and Dashboard UI

Status: **Complete** — build passes, database live, all pages verified against the running app
Date: 2026-09-04 · *updated 2026-09-04 — see §15 Changelog*

> **Not a numbered phase.** This work sits outside the CLAUDE.md §25 phase order: it is the database
> bring-up, the application shell, the dashboard UI, and a validated colour system — groundwork the
> phases share rather than a business feature. It was briefly filed as `Phase4.md`; that name now
> belongs to the real **Phase 4 — Product**.

---

## 1. What this phase covers

1. Getting PostgreSQL connected and the schema live (correcting a wrong conclusion from Phases 2–3)
2. Fixing a real authorization bug that left the login page unstyled
3. Rebuilding the app shell to match the supplied reference screenshot
4. Building the Dashboard page
5. Replacing the ad-hoc palette with a **validated** colour system
6. Restyling Category and Brand pages onto that system

Not covered: Product, Supplier, Purchase, Inventory, Customer, POS, dark mode.

---

## 2. Correction carried over from Phases 2 and 3

Both earlier documents state that "PostgreSQL is not installed/running on the development machine."
**That was wrong.** The check only looked for a Windows service (`Get-Service postgres*`) and for
`psql` on `PATH`. PostgreSQL was running the whole time — in Docker:

```
CONTAINER          IMAGE             PORTS
PostgreSQL_Server  postgres:18       0.0.0.0:5434->5432/tcp   (healthy)
PostgreSQL_PgAdmin dpage/pgadmin4    0.0.0.0:5050->80/tcp
```

The actual fault was the guessed connection string: it used port **5432**, the container publishes
**5434**. `docs/Phase3.md` §6 has since been corrected with an explicit note. **`docs/Phase2.md` §6/§7 is
still stale** on this point.

### Bring-up steps performed

| # | Action | Result |
|---|---|---|
| 1 | `CREATE DATABASE opticalshop` in the container | created; the unrelated `ios_dev` database untouched |
| 2 | Corrected the user-secrets connection string to port 5434 | saved |
| 3 | `dotnet ef database update` | applied `InitialIdentity` + `AddCategoryAndBrand` |
| 4 | Started the app once so `DatabaseSeeder` ran | roles + admin created |

Verified in the database:

```
 UserName | IsActive | role  | has_password        Name
----------+----------+-------+--------------      ---------
 admin    | t        | Admin | t                   Admin
                                                   Cashier
```

10 tables: 7 Identity + `Categories` + `Brands` + `__EFMigrationsHistory`.

Working connection string (user-secrets, not source):

```
Host=localhost;Port=5434;Database=opticalshop;Username=postgres;Password=postgres
```

---

## 3. Bug fixed — static files were behind the login wall

**Symptom.** `GET /css/site.css` returned `302 → /Account/Login` for a signed-out visitor. The login
page had been rendering with **no CSS at all** since Phase 2. This was found by requesting the
stylesheet directly during verification, not by reading the code.

**Cause.** `MapStaticAssets()` registers real endpoints. The global fallback authorization policy
added in Phase 2 applies to every endpoint that carries no authorization metadata — which includes
those static-asset endpoints.

**Fix** — [Program.cs](../Optical.Web/Program.cs):

```csharp
// Static files must stay reachable for signed-out users, otherwise the global
// fallback authorization policy would also redirect CSS and JS to the login page.
app.MapStaticAssets().AllowAnonymous();
```

**Verified after the fix:**

```
site.css, bootstrap.min.css, bootstrap.bundle.min.js, site.js  (anon) => 200   (was 302)
GET /  (anon) => 302        GET /Categories (anon) => 302                      (still protected)
```

The fallback policy still guards every page; only static assets opt out.

---

## 4. Layer-by-layer changes

### 4.1 Optical.Domain

**No changes.** Still `BaseEntity`, `Category`, `Brand`, two exceptions, zero references.

---

### 4.2 Optical.Application

One new vertical slice.

```
Optical.Application/
├── DependencyInjection.cs                    (edited: +1 registration)
└── Features/
    └── Dashboard/                            (new)
        └── GetDashboardSummary.cs
```

**`Features/Dashboard/GetDashboardSummary.cs`**

```csharp
public sealed record RecentRecord(string Name, string? Description, string Type, bool IsActive, DateTime CreatedAt);

public sealed record DashboardSummary(
    int CategoryCount, int BrandCount, int InactiveCount, IReadOnlyList<RecentRecord> Recent);
```

The handler runs four `CountAsync` calls and two `AsNoTracking` projections (top 5 categories, top 5
brands by `CreatedAt`), then merges and re-sorts them in memory and takes the newest 5. Two small
projected queries merged in memory is simpler and cheaper here than a SQL `UNION` across two tables;
revisit only if the recent list ever needs paging.

**`DependencyInjection.cs`** — one line: `services.AddScoped<GetDashboardSummaryHandler>();`

---

### 4.3 Optical.Infrastructure

**No code changes.** Only the two existing migrations were applied to the live database.

---

### 4.4 Optical.Web

```
Optical.Web/
├── Program.cs                             (edited: MapStaticAssets().AllowAnonymous())
├── Controllers/
│   └── HomeController.cs                  (rewritten)
├── Views/
│   ├── Home/Index.cshtml                  (rewritten — dashboard)
│   ├── Home/Privacy.cshtml                (DELETED)
│   ├── Shared/_Layout.cshtml              (rewritten — app shell)
│   ├── Shared/_InfoIcon.cshtml            (new partial)
│   ├── Shared/_Layout.cshtml.css          (DELETED — scaffold leftover)
│   ├── Categories/ Index, Create, Edit    (restyled)
│   └── Brands/ Index, Create, Edit        (restyled)
└── wwwroot/css/site.css                   (rewritten — design system)
```

**`Controllers/HomeController.cs`** — `Index` is now async and calls the dashboard handler; `Privacy`
was removed with its view. Still thin: one handler call, no EF.

```csharp
public class HomeController(GetDashboardSummaryHandler getDashboardSummary) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await getDashboardSummary.HandleAsync(cancellationToken));
    ...
}
```

**`Views/Shared/_Layout.cshtml`** — rebuilt to the reference screenshot:

- rounded blue app icon (inline spectacles SVG) + shop title + grey subtitle
  *(the title and subtitle were hard-coded when first built; they now come from System Settings — §15.2)*
- right side: username bold, role beneath, circular avatar, chevron → dropdown containing Sign out
- nav tab row, icon + label, active tab in blue with a 3px blue underline
- `TempData["Success"]` alert retained above the body
- no icon-font dependency anywhere — every icon is an inline SVG, so the app works offline

Tab highlighting is computed from the route:

```csharp
string TabClass(string name) =>
    string.Equals(controller, name, StringComparison.OrdinalIgnoreCase)
        ? "app-nav-link active" : "app-nav-link";
```

**`Views/Shared/_InfoIcon.cshtml`** — the ⓘ glyph repeated on all four stat tiles, extracted so the
markup is not duplicated four times.

**`Views/Home/Index.cshtml`** — the dashboard: a four-tile stat row, then a ~2:1 row holding
**Recent Activity** (left) and **Quick Actions** (right).

**Category / Brand pages** — moved onto the shared card, table and chip classes so the whole app reads
as one system. Forms sit inside `.app-card`; list rows use the new chips and row accents.

---

## 5. Dashboard content — an honest deviation

The reference screenshot shows *Total Items, Low Stock, Total Transactions, Recent Transaction
Activity, Export Data, Sync Stock.* **None of that data exists yet** — there are no products, no
stock and no sales until Phases 4–8. Rather than display invented numbers, the same visual structure
was mapped onto data that is real:

| Reference tile | Built as | Source |
|---|---|---|
| Total Items | **Categories** | `Categories.Count()` |
| Low Stock | **Brands** | `Brands.Count()` |
| Total Transactions | **Inactive** | count of `!IsActive` across both tables |
| System Status | **System Status** | static "Online" |
| Recent Transaction Activity | **Recent Activity** | 5 newest categories + brands by `CreatedAt` |
| Quick Actions | **Quick Actions** | New/All Category, New/All Brand |

The nav shows only Dashboard, Categories and Brands for the same reason. As later phases land, tiles
and tabs are swapped for the real ones with no layout change.

---

## 6. The colour system

### 6.1 Method

Colours were **computed and validated**, not chosen by eye. The palette was run through a
CVD-simulation and contrast validator; the reported figures below are its output, against the actual
white card surface the app renders on.

### 6.2 Categorical — entity identity

Two hues carry identity, fixed and never cycled. Category is blue, Brand is orange, everywhere they
appear (chip, row accent, stat tile icon, action button).

| Role | Hue | Wash | Text ink | Ink contrast on wash |
|---|---|---|---|---|
| Category | `#2a78d6` | `#e4eefb` | `#1c5cab` | 5.66:1 |
| Brand | `#eb6834` | `#fdeee6` | `#a8420f` | 5.37:1 |

Validator output for the pair the eye actually compares in the Type column:

```
Palette (light, surface #ffffff, categorical): 2 slots
  [PASS] Lightness band         both inside L 0.43-0.77
  [PASS] Chroma floor           both >= 0.1
  [PASS] CVD separation         dE 22.6 (protan) · 26.6 (tritan)     target >= 8
  [PASS] Normal-vision floor    dE 27.7                              floor >= 15
  [PASS] Contrast vs surface    both >= 3:1
  -> ALL CHECKS PASS
```

### 6.3 Status — reserved, never reused as an entity colour

| Role | Hue | Wash | Text ink | Ink contrast on wash |
|---|---|---|---|---|
| good / Active | `#0ca30c` | `#e4f4e6` | `#006300` | 6.60:1 |
| warning / Inactive count | `#fab219` | `#fdf1d9` | `#7a5200` | — |
| off / Inactive state | neutral | `#eef0f3` | `#52514e` | 6.95:1 |

### 6.4 Ink and surfaces

| Token | Value | Contrast on white |
|---|---|---|
| `--ink` | `#0b0b0b` | 19.68:1 |
| `--ink-secondary` | `#52514e` | 7.94:1 |
| `--ink-muted` (icons only) | `#898781` | 3.59:1 |
| `--surface` | `#ffffff` | — |
| `--page` | `#f7f8fa` | — |
| `--border` | `#e6e8ec` | — |

`--ink-muted` is used for icons only, never for text — 3.59:1 clears the 3:1 non-text bar but not the
4.5:1 text bar. Small meta text uses `--ink-secondary`.

---

## 7. Recent Activity — column colours and accessibility

Every column carries meaning, and **no column depends on hue alone**.

| Column | Encoding |
|---|---|
| Date & Time | date in ink with `tabular-nums` so digits align down the column; time beneath in secondary ink; calendar/clock icons demoted to muted grey |
| Item | name in primary ink, description in secondary ink, `—` when empty |
| Type | chip = coloured **dot** + wash + uppercase **text label** (CATEGORY / BRAND) |
| Status | `ACTIVE` filled dot vs `INACTIVE` **hollow ring** + text label |
| Row | 3px left accent bar in the entity's hue, so identity reads at row level |

Additional channels:

- a **legend** sits in the card header beside the title
- every chip carries a 1px border, so its boundary survives greyscale printing
- `@media (forced-colors: active)` strips washes and falls back to system colours
- `@media print` renders chips as outlined black-on-white
- `<caption class="visually-hidden">` describes the table for screen readers; `<th scope="col">` on every header; decorative SVGs marked `aria-hidden="true"`
- `:focus-visible` outlines on links, buttons, nav tabs and action buttons

---

## 8. Three deliberate departures from the reference image

| # | Reference | Built | Reason |
|---|---|---|---|
| 1 | "Low Stock" number printed in orange | stat values are **always ink**; colour lives in the icon tile | a number whose meaning depends on hue is unreadable to a colourblind user and in print. The tile + label carry the signal instead. |
| 2 | Quick Actions in blue / green / purple / dark | blue and orange only, matching the chips | four arbitrary hues added no meaning. Now *New Category* is blue and *New Brand* is orange, so button colour reinforces the identity mapping. |
| 3 | Blue calendar icon in the activity table | muted grey | blue means "Category" in this app; a decorative blue icon would dilute that. |

---

## 9. Known limitation, stated rather than hidden

The **Active vs Inactive** pair fails a strict categorical check — normal-vision dE **14.4**, just
below the 15 floor — because "Inactive" is deliberately achromatic grey, meaning *no state*. The
validator scopes its checks to categorical palettes; for reserved status colours the required
mitigation is icon + label, which is what ships:

- filled dot (Active) vs hollow ring (Inactive)
- the words `ACTIVE` / `INACTIVE` always present
- different wash backgrounds

So state never rests on hue. Recorded here rather than reported as a clean pass.

---

## 10. Verification

All checks run against the **live application**, not by reading code.

```
dotnet build Optical.slnx
  -> Build succeeded. 0 Warning(s), 0 Error(s)

Anonymous:
  /css/site.css                      => 200   (was 302 before the fix)
  /lib/bootstrap/dist/css/*.min.css  => 200
  /lib/bootstrap/dist/js/*.min.js    => 200
  /js/site.js                        => 200
  GET /                              => 302   (still protected)
  GET /Categories                    => 302   (still protected)

Signed in as admin:
  /                    => 200      /Categories          => 200
  /Brands              => 200      /Categories/Create   => 200
  /Brands/Edit/1       => 200

Rendered dashboard HTML:
  stat values      3 / 2 / 1 / Online        (all ink, none hue-dependent)
  row accents      3 row-category · 2 row-brand
  chips            3 CATEGORY · 2 BRAND · 4 ACTIVE · 1 INACTIVE
  legend           present in card header
  quick actions    4 links, all targets correct

Rendered list pages:
  /Categories   2 chip-active · 1 chip-inactive · 3 row-category · action-solid-1
  /Brands       2 chip-active · 2 row-brand · action-solid-2

No leftover `pill` or `quick-action` classes anywhere in Views/.
```

---

## 11. Sample data added

Five rows were inserted so the dashboard could be verified with real content:

| Table | Rows |
|---|---|
| Categories | Frames, Sunglasses, Contact Lens *(inactive)* |
| Brands | Ray-Ban, Oakley |

Useful starter data; delete whenever you like:

```sql
DELETE FROM "Categories"; DELETE FROM "Brands";
```

---

## 12. Architecture compliance

| Rule | Result |
|---|---|
| Domain has no outward dependency | untouched this phase |
| Application has no Web dependency | dashboard slice uses `IApplicationDbContext` only |
| Controllers stay thin | `HomeController.Index` is one handler call |
| No DB access from controllers | no EF type anywhere in Web |
| `AsNoTracking` + projection on reads | both recent-record queries |
| No banned abstractions (§18) | none introduced |
| No new packages | zero — all icons are inline SVG, no icon font or CDN |

---

## 13. Open items

- ~~`docs/Phase3.md` is stale on the PostgreSQL claim.~~ **Corrected** — Phase3.md §6 now carries an
  explicit correction. **`docs/Phase2.md` §6/§7 is still stale** on the same point.
- ~~No Cashier user exists, so the Access Denied path is untested.~~ **Tested** — the role restriction
  was exercised by temporarily removing the admin's role (Phase3.md §11.7). A real Cashier *user*
  still does not exist, so the cashier-facing nav has never been seen in a browser.
- **No dark mode.** A correct dark theme means re-stepping every hue against a dark surface and
  re-validating, not inverting values. Deferred.
- The dashboard tiles are placeholders for real business metrics; they change as Phases 4–8 land.
- The dashboard has no tile for shop configuration; System Settings is reachable from the nav only.

---

## 14. Next step

**Phase 4 — Product** (CLAUDE.md §25), now implemented — see `docs/Phase4.md`. It will add
the first foreign keys (`CategoryId`, `BrandId`), the first `decimal(18,2)` money columns, a unique
SKU index, and the first list that genuinely needs pagination. The dashboard tiles and nav tabs built
here are designed to absorb it without layout change.

---

# 15. Changelog — changes made after this document was first written

Everything below happened after §1–§14 were written. The feature work itself is documented in
**`docs/Phase3.md` §11**; recorded here because it changed files this document describes.

## 15.1 System Settings feature

A new Admin-only screen for the shop's **business name and address**. Full write-up in Phase3.md §11.

Files touched that Phase4.md also covers:

| File | Change |
|---|---|
| `Views/Shared/_Layout.cshtml` | fourth nav tab — **Settings** (gear icon), inside the existing `@if (isAdmin)` block |
| `Optical.Application/DependencyInjection.cs` | two more handler registrations, beside the dashboard one |

New files (not previously described here): `Optical.Domain/Entities/ShopSettings.cs`,
`Optical.Application/Features/Settings/{GetShopSettings,SaveShopSettings}.cs`,
`Optical.Infrastructure/Persistence/Configurations/ShopSettingsConfiguration.cs`,
migration `20260904155650_AddShopSettings`, `Optical.Web/ViewModels/ShopSettingsViewModel.cs`,
`Optical.Web/Controllers/SettingsController.cs`, `Optical.Web/Views/Settings/Index.cshtml`.

The settings screen reuses the design system from §6–§7 unchanged — `app-card`, `app-card-header`,
`chip-active` / `chip-inactive` for the CONFIGURED / NOT SET UP state. No new CSS classes and no new
colours were needed, which is the point of having a system.

Live schema is now **11 tables** (§2 said 10): the seven Identity tables plus `Categories`, `Brands`,
`ShopSettings` and `__EFMigrationsHistory`.

## 15.2 The header now shows the configured business name

§4.4 originally described a hard-coded brand block reading "Optical Shop" / "Optical Shop Management
v1.0". Storing a business name that the application never displays is pointless, so the shell now
reads it from System Settings.

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

**Why not a ViewComponent.** It is the textbook answer, but it costs a class plus a
`Views/Shared/Components/.../Default.cshtml` file to render six lines of chrome — and it still could
not set `<title>`, so the layout would need the value anyway. One injected handler covers the header
and both page titles in one place. The dependency direction is unchanged: the view calls an
Application handler, never EF.

**Cost.** One primary-key-sized `AsNoTracking` projection per rendered page. No cache — for a single
shop the read is trivial, and a cache would introduce staleness (the header would keep showing the
old name after an edit) for no measurable gain.

## 15.3 CSS additions

Appended to `wwwroot/css/site.css` — the only change to the design system since §6:

```css
.app-brand-text { min-width: 0; }

.app-brand-text .app-brand-subtitle {
    max-width: 32ch;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
```

A long address cannot stretch the header; the full text stays available in a `title` tooltip. No
colour token changed, so the §6 validation results still stand.

## 15.4 Verification of these changes

```
dotnet build Optical.slnx   -> Build succeeded. 0 Warning(s), 0 Error(s)

Settings row: Vision Care Optical Ltd. | 99 New Elephant Road, Dhaka 1205

Login (anonymous)  <title>Sign in - Vision Care Optical Ltd.</title>
                   heading "Vision Care Optical Ltd."
Dashboard          <title>Dashboard - Vision Care Optical Ltd.</title>
                   brand title    "Vision Care Optical Ltd."
                   brand subtitle "99 New Elephant Road, Dhaka 1205"

1. Renamed to "Dhaka Eye Care" through the UI
   -> header immediately showed "Dhaka Eye Care" / "12 Mirpur Road, Dhaka 1207"
2. Deleted the settings row
   -> header fell back to "Optical Shop" / "Optical Shop Management v1.0"
   -> login page still 200 and showed "Optical Shop"
      (it is anonymous and renders before authentication, so it must work with no row)
3. Restored through the UI
   -> Id=1 | Vision Care Optical Ltd. | 99 New Elephant Road, Dhaka 1205
```

Role restriction on the new Settings screen was verified separately — Phase3.md §11.7.

## 15.5 What this changelog does *not* change

- The colour system (§6), Recent Activity encodings (§7) and the three departures from the reference
  image (§8) are untouched.
- The dashboard tiles are still Categories / Brands / Inactive / System Status — the deviation
  explained in §5 still stands, because Product and stock data still do not exist.
- Still no Product work. §14 remains the next step.
