# Phase 5 — Supplier, Purchase and Inventory

Status: **Complete** — build passes, 19 tests pass, migration applied, dashboard rendered against the running application
Date: 2026-09-05

> §12 of this file covers the dashboard rebuild that followed the phase, done to the
> reference screenshot supplied by the shop owner.

---

## 1. Scope

Implemented:

- `Supplier` — list, search, create, edit, active/inactive
- `Purchase` + `PurchaseItem` — supplier, date, optional invoice number, line items, server-side totals
- `InventoryItem` — one stock row per product
- Stock increase from a purchase
- Manual stock adjustment
- Low-stock visibility (dedicated filter on the inventory list, plus the dashboard panel)
- Atomic purchase creation

Not implemented, per the phase brief:

- warehouse, stock transfer, batch, serial number, FIFO/LIFO
- inventory ledger / stock movement history
- POS, sales, returns
- purchase edit or delete (a recorded purchase is a historical fact; corrections come via stock adjustment)

---

## 2. Design decisions

| Decision | Choice | Reason |
|---|---|---|
| Transaction | **One `SaveChangesAsync`** | EF Core wraps a single SaveChanges in one database transaction. Purchase, items and stock changes are all tracked first and written once, so a purchase can never exist without its stock movement. An explicit `BeginTransactionAsync` would add an abstraction to `IApplicationDbContext` for behaviour we already get. |
| Stock ownership | `InventoryItem`, one row per product, unique index on `ProductId` | Decided in Phase 4. Product never carries a quantity, so there is one source of truth. |
| Missing stock row | Treated as **zero**, created on first movement | Products are listed from the product side with a left join, so a product that has never been purchased still appears with a quantity of 0 instead of dropping out of the list and the counts. |
| Manual adjustment | **Absolute set**, not a delta | "Enter what you counted" matches a physical stock take and covers both increase and decrease. A delta needs a ledger to be auditable, and §14 rules a ledger out. |
| Duplicate product lines | **Rejected** with a clear message | Silently summing them hides a data-entry mistake. |
| `PurchaseDate` | `DateOnly` → Postgres `date` | A purchase happens on a day, not at an instant. Also sidesteps the Npgsql `timestamptz` requirement that a `DateTime` be UTC. |
| Line cost | Stored on `PurchaseItem` as `PurchasePrice` + `LineTotal` | The cost at the time of purchase is history. Later edits to the product's price must not rewrite it. |
| Purchase delete behaviour | `Cascade` from Purchase to PurchaseItem, `Restrict` everywhere else | Items only exist as part of their purchase. A supplier or product with history cannot be deleted out from under it. |
| Database guards | `CHECK` constraints on quantity and money | The handler validates first; these are the last line of defence, and they will matter in Phase 7 when POS decreases stock. |
| Test project | `Optical.Tests` — xUnit + SQLite built from the real EF model | §23 names purchase stock increase and inventory adjustment as business-critical. Building the schema from the real model means foreign keys and check constraints are actually enforced, so the rollback claim is verified rather than asserted. |

---

## 3. Optical.Domain

Four entities, all `BaseEntity` (Id, CreatedAt, UpdatedAt):

```
Supplier        Name, Phone?, Email?, Address?, IsActive
Purchase        SupplierId, PurchaseDate (DateOnly), InvoiceNumber?, TotalAmount, Items
PurchaseItem    PurchaseId, ProductId, Quantity, PurchasePrice, LineTotal
InventoryItem   ProductId, Quantity
```

No domain events, no value objects, no domain services. `TotalAmount` and `LineTotal` are
plain properties written by the handler, never by the browser.

---

## 4. Optical.Application

Three feature folders, each holding only the files it needs.

```
Features/Suppliers/     GetSuppliers, GetSupplierById, CreateSupplier, UpdateSupplier
Features/Purchases/     GetPurchases, GetPurchaseById, GetPurchaseFormOptions, CreatePurchase
Features/Inventory/     GetInventory, GetProductStock, AdjustStock
```

`IApplicationDbContext` gained four `DbSet`s. No new abstractions.

### CreatePurchase — the order that matters

```
1  items not empty
2  supplier exists and is active
3  no duplicate product lines
4  load every product in one query
5  per line: product exists, is active, quantity > 0, price >= 0
6  build Purchase + PurchaseItems, LineTotal = quantity × price
7  TotalAmount = sum of line totals          <- server, never the browser
8  load existing stock rows in one query
9  increase or create each stock row
10 ONE SaveChangesAsync                       <- one transaction
```

Any failure between 1 and 9 throws before anything is written. Nothing is partially saved.

---

## 5. Optical.Infrastructure

Four configurations and one migration, `20260904193418_AddSupplierPurchaseInventory`.

| Table | Constraints |
|---|---|
| `Suppliers` | Name required (150), Phone (30), Email (150), Address (250), index on Name |
| `Purchases` | InvoiceNumber (50), TotalAmount `numeric(18,2)`, index on PurchaseDate, index + FK Restrict on SupplierId, `CHECK TotalAmount >= 0` |
| `PurchaseItems` | PurchasePrice / LineTotal `numeric(18,2)`, FK Cascade to Purchase, FK Restrict to Product, `CHECK Quantity > 0`, `CHECK PurchasePrice >= 0` |
| `InventoryItems` | **Unique** index on ProductId, FK Restrict to Product, `CHECK Quantity >= 0` |

---

## 6. Optical.Web

| Controller | Actions | Views |
|---|---|---|
| `SuppliersController` | Index, Create, Edit | Index, Create, Edit |
| `PurchasesController` | Index, Details, Create | Index, Details, Create |
| `InventoryController` | Index, Adjust | Index, Adjust |

Every controller is bind → call handler → redirect or view. No EF Core, no business rules.

The purchase form is the only place with meaningful JavaScript: jQuery clones a `<template>`
row, reindexes `Items[n]` names, and shows a running total. **The displayed total is a
convenience.** The server recalculates every line and the grand total on POST.

---

## 7. Two definitions of "low stock"

They differ on purpose, and it is worth knowing which is which:

- **Inventory page filter** — `quantity <= reorderLevel`, *including zero*. It is a work
  list: everything that needs reordering, out-of-stock items first among them.
- **Dashboard buckets** — mutually exclusive. Out of stock is `quantity = 0`; low stock is
  `0 < quantity <= reorderLevel`; in stock is everything above the reorder level. They have
  to be exclusive because they are shown as shares of one whole and must sum to the product count.

---

## 8. Verification

```
dotnet build                → 0 errors, 0 warnings
dotnet test                 → 19 passed
dotnet ef database update   → applied to the dev database (Postgres, Docker, port 5434)
```

Business rules covered by tests:

| Test | Asserts |
|---|---|
| Purchase creates stock for a product that has none | stock row created at the purchased quantity |
| Purchase adds to existing stock | 4 + 6 = 10 |
| Total is calculated on the server | 3×25.50 + 2×10.00 = 96.50 |
| Empty items rejected | `DomainException` |
| Invalid quantity rolls the whole purchase back | `Purchases` **and** `PurchaseItems` empty, valid line's stock still 0 |
| Unknown product rolls the whole purchase back | same |
| Inactive product rejected | no stock movement |
| Inactive supplier rejected | `DomainException` |
| Same product twice rejected | `DomainException` |
| Adjustment sets the counted quantity | 7 then 3 |
| Adjustment cannot make stock negative | `DomainException`, stock unchanged |
| Low stock includes never-purchased products | a product with no stock row counts as 0 |

---

## 9. Architecture compliance

- Domain has no Infrastructure or Web dependency.
- Application has no Web dependency; it talks to `IApplicationDbContext` only.
- Controllers contain no EF Core and no business rules.
- No generic repository, no unit of work, no MediatR, no AutoMapper.
- Read-only queries use `AsNoTracking`, are async, and paginate.

---

## 10. Known limitations

- **Stock has no history.** There is no record of who adjusted what, or why. §14 rules out a
  ledger for the MVP; if an audit trail is ever needed, that is a new entity, not a refactor.
- **No concurrency control on stock.** Two simultaneous purchases of the same product both
  read then write the quantity; the second overwrite could be lost. Harmless while purchases
  are entered by one admin, but **POS in Phase 7 decreases stock and will need this addressed** —
  either a row-level lock or a `xmin` concurrency token.
- **A purchase cannot be edited or deleted.** Corrections go through a stock adjustment.
- **Adjustments carry no reason.** The field would be free text with nothing reading it.

---

## 11. Files

**Added** — `Supplier.cs`, `Purchase.cs`, `PurchaseItem.cs`, `InventoryItem.cs`;
`Features/Suppliers/` (4), `Features/Purchases/` (4), `Features/Inventory/` (3);
4 EF configurations + 1 migration; 3 controllers, 3 view models, 7 views;
`Optical.Tests` (`TestDatabase.cs`, `PurchaseTests.cs`).

**Modified** — `IApplicationDbContext`, `ApplicationDbContext`, `Optical.Application/DependencyInjection.cs`,
`_Layout.cshtml` (three nav links), `Optical.slnx`.

---

# 12. Addendum — Dashboard rebuilt to the reference screenshot

Date: 2026-09-05

The shop owner supplied a dashboard mockup. Two things in it could not be taken literally,
and both were settled by asking rather than guessing:

| Question | Answer | Effect |
|---|---|---|
| The mockup has a hamburger and no nav tabs — switch to a sidebar? | **Keep the top nav tabs** | `_Layout` untouched apart from a footer. No risk to working pages. |
| Today's Sales, Top Selling Products and Sales Summary need Phase 7–8 data | **Omit until Phase 7** | Four panels shipped instead of six. No placeholder or invented numbers. |

## 12.1 What shipped

Five stat cards — Total Products, Low Stock Items, Out of Stock, Stock Value, Today's Purchases —
then Stock Status Overview (donut) beside Low Stock Alerts, and Stock Value by Category (donut)
beside Recent Activities.

`GetDashboardSummaryHandler` was rewritten. It previously counted categories and brands; it now
returns the stat figures, the three stock buckets, the low-stock alert list, stock value split by
category, and a merged recent-activity feed. One handler, one call from the controller.

**Recent Activities** is built from what actually exists — purchases recorded, products added,
suppliers added — merged and sorted newest first. Stock adjustments are absent because nothing
logs them (§10).

**No invented trends.** The mockup shows "▲ 8.5% vs yesterday" on every card. Comparing product
counts against yesterday needs historical snapshots that the schema does not keep, so those cards
carry a descriptive line instead. Today's Purchases *can* be compared honestly, so it is — and
`PurchaseTrendPercent` returns `null` when yesterday had no purchases, which renders no trend at
all rather than a division by zero or a fake infinity.

## 12.2 Charts without a chart library

Both donuts are inline SVG: one `<circle>` per slice, `stroke-dasharray` for the arc,
`stroke-dashoffset` for its start, with a 2px gap of surface between segments. `_Donut.cshtml`
is a shared partial driven by `DonutViewModel`; the two donuts differ only in their slices.
A charting dependency for two ring shapes would have been exactly the over-engineering §18 bans.

## 12.3 Colour, validated rather than eyeballed

The palette was run through a colour-vision validator against the white card surface:

| Slices | Result |
|---|---|
| blue + orange + aqua | passes all-pairs |
| blue + orange + aqua + **violet** | **passes all-pairs** — worst CVD ΔE 9.2, worst normal-vision ΔE 16.3 |
| any fifth hue (magenta or red) | **fails** the normal-vision floor |

So the category donut seats **four** hues and folds everything past the fourth into a neutral
grey "Other" slice — `MaxCategorySlices = 4` in the handler. That is why the cap exists; it is
not an arbitrary number.

New tokens in `site.css`: `--status-critical` (out of stock had no colour before) and
`--chart-1..4` + `--chart-other`. The chart slots are **positional**, assigned by a category's
place in a stable value-ordered list, never by rank within the current view — a value change
must not repaint a slice. Slots 1–2 share hexes with `--cat-1/--cat-2` because both come from
the same validated palette order.

The stock-status donut uses the reserved status colours. Red-vs-green sits at ΔE 4.1 under
deuteranopia, so every legend row carries a **swatch + icon + name + value + share**: identity
never rests on hue alone.

## 12.4 Two EF translation failures the tests caught

Both were found by tests before they reached a browser, and both are worth remembering:

1. **`GroupBy` on a two-hop navigation.** `InventoryItems.GroupBy(i => i.Product.Category.Name)`
   with `Sum(i => i.Quantity * i.Product.PurchasePrice)` will not translate — an aggregate cannot
   reach back through a navigation once the rows are grouped. Rewritten to drive from the category
   side with one correlated `Sum` per category.
2. **`Where`/`OrderBy` after projecting into a record constructor.** EF cannot map `c.Value` back
   through `new CategoryStockValue(...)`. Filtering and ordering now happen in memory — a shop has
   a handful of categories, so there is nothing to gain from a second round trip. Anonymous-type
   projections *do* support later filtering, which is why the product/stock left join uses one.

## 12.5 Verification

```
dotnet build   → 0 errors, 0 warnings
dotnet test    → 19 passed (7 new dashboard tests)
```

Rendered against the running application on `http://localhost:5210` after signing in: HTTP 200,
donut arcs summing exactly to the circumference (437.823 + 2 gap = 439.823), legend showing
`In Stock 1 (100%)` and `Frames ৳65,000 (100%)`, empty state on Low Stock Alerts, activity feed
listing the seeded product, footer reading "© 2026 Ideal Optics".

The dev database currently holds one product, so the multi-slice donut, the populated alert table
and the five-across card row were exercised by tests rather than on screen.

## 12.6 Density and collapsible widgets

Two follow-ups from the owner's marked-up screenshot:

**The five stat cards now fit one row.** They were breaking 3 + 2 at ~1360px because the grid
stepped `lg-3 → xxl-5`, and xxl only starts at 1400px. Changed to `md-3 → xl-5`, and the tiles
were made denser with a `.stat-tile` class — 1.75rem value (1.45rem for money), 40px icon, 1rem
padding. The class is scoped on purpose: `.stat-value` is also used on the Product, Purchase and
Adjust detail pages, which should stay roomy.

**All four panels collapse.** A chevron in each card header uses Bootstrap's own collapse — no
custom show/hide logic — via the shared `_CollapseToggle.cshtml` partial, which takes the target
id. The chevron rotates off `aria-expanded`, so the button state and the visual state cannot drift.

Two details worth keeping:

- A collapsed card must not keep its expanded neighbour's height. `.app-card:has(> .collapse:not(.show))`
  sets `height: auto !important` — the `!important` is required because `h-100` is a Bootstrap
  utility and carries its own.
- `site.js` remembers which sections are closed in `localStorage`, restored on `DOMContentLoaded`.
  Bootstrap's collapse events bubble, so one delegated listener covers every section on the page.
  Storage is best-effort: a browser that refuses it simply shows everything expanded.

## 12.7 Files

**Added** — `Views/Shared/_Donut.cshtml`, `ViewModels/DonutViewModel.cs`, `Optical.Tests/DashboardTests.cs`.

**Rewritten** — `Features/Dashboard/GetDashboardSummary.cs`, `Views/Home/Index.cshtml`.

**Modified** — `wwwroot/css/site.css` (status-critical + chart tokens, dashboard section),
`_Layout.cshtml` (footer).
