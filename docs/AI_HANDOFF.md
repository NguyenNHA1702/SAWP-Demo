# 🤖 AI HANDOFF – SAWP Demo (READ THIS FIRST)

> This file is the **single source of truth** for any AI agent / model continuing this project.
> If you are a new model picking this up: read this whole file, then look at **§6 Progress Tracker**,
> pick the first unchecked item, implement it following **§4 Conventions**, build, then tick it here.
> **Always update §6 and §7 before you stop.**

---

## 1. What we are building
**SAWP – School Administration & Workflow Platform** for a **Vietnamese primary school** (Grades 1–5).
Requirements: `SEP490_G45_Report3_SRS.docx.md` (repo root). Use cases UC-01…UC-60, business rules BR-01…BR-55,
messages MSG01…MSG50 (Vietnamese text, see `src/SAWP.Web/Services/Messages.cs`).

It is a **demo but fully working** (real DB, real auth, real workflows) – NOT static mockups.

## 2. Locked decisions (do not change without the user)
| Topic | Decision |
|---|---|
| Stack | **ASP.NET Core 8 Razor Pages**, **EF Core 8 + SQL Server**, cookie auth |
| DB | Default **LocalDB** `(localdb)\MSSQLLocalDB`, DB name `SAWP_Demo`. Alt: `.\MSI` (see appsettings note) |
| Ready-to-run | `Program.cs` applies migrations + seeds demo data automatically on startup |
| UI language | **Vietnamese only** for all UI text. Code, comments, logs in **English** |
| Theme | **Playful Geometric** design system (tokens in `wwwroot/css/tokens.css`) – see §5 |
| Roles | Parent=Phụ huynh, Teacher=Giáo viên chủ nhiệm (GVCN), Office=Giáo vụ, Service=Nhân viên bếp ăn, Board=Ban Giám hiệu (BGH), Admin=Quản trị hệ thống |
| Leave escalation | Setting `Leave.MaxDaysForTeacher` (default 3). Longer requests → BGH. Editable in Admin › Settings |
| Document types | ReferenceData `DocumentType`: "Giấy xác nhận đang học tại trường" (Giáo vụ), "Giấy xác nhận chuyên cần" (Giáo vụ), "Bản sao học bạ / hồ sơ chuyển trường" (needs BGH) |
| Email/OTP | Simulated → `EmailOutbox` table + log. Admin can view at Admin › Email Outbox |
| Demo login | Cards under login form, click = autofill, dblclick = login. Password `Demo@123` for all |
| Comments | XML `///` docs on public types/methods; reference UC/BR/MSG IDs inline |
| Logging | Very detailed console logs (startup, seeding counts, each request, business events, denials, errors) |

### Demo accounts
| Username | Role | Notes |
|---|---|---|
| `admin` | Admin | |
| `bgh.hung` | Board | Hiệu trưởng |
| `giaovu.mai` | Office | |
| `gv.lan` | Teacher | GVCN of class 1A |
| `bep.tuan` | Service | |
| `ph.nam` | Parent | 2 linked children (one in 1A) |

## 3. Solution layout
```
Demo/
├─ SAWP.sln
├─ .config/dotnet-tools.json        (dotnet-ef 8 local tool)
├─ docs/AI_HANDOFF.md               (this file)
└─ src/SAWP.Web/
   ├─ Program.cs                    DI, auth, policies, logging, migrate+seed
   ├─ appsettings.json              connection string + NOTE for teammates
   ├─ Domain/                       Entities.cs, Enums.cs (27 SRS entities + extras)
   ├─ Data/                         SawpDbContext.cs, DemoSeeder.cs, Migrations/
   ├─ Services/                     Messages, CurrentUser, Scope, Audit, Notification,
   │                                RequestWorkflow, Attendance, Lunch, Calendar, Settings,
   │                                FileStorage, Import, Report, Email(simulated)
   ├─ Infrastructure/               RequestLoggingMiddleware, PageBase (toast helpers), Extensions
   ├─ TagHelpers/                   IconTagHelper (<icon name="" shape="" tone="">)
   ├─ Pages/
   │   ├─ Index (landing), Account/*, Notifications, Shared/*
   │   ├─ Parent/*, Teacher/*, Office/*, Service/*, Board/*, Admin/*
   └─ wwwroot/css (tokens, base, components, layout, patterns), js/site.js, img/
```

## 4. Conventions (follow exactly)
- **Page base class**: inherit `SAWP.Web.Infrastructure.AppPageModel` → gives `Toast(msgCode)`, `CurrentUser`, `Db`, logger.
- **Authorization**: folder-level policies in `Program.cs` (`/Parent` → Role Parent, etc.). **Plus** record-level checks
  through `IScopeService` (BR-05/06/07). Never trust hidden UI only.
- **Messages**: never hardcode user messages – use `Messages.Get("MSG08")` / `Toast("MSG08")`.
- **Audit**: any create/update of student, guardian, class, attendance correction, lunch correction, settings, roles
  → `IAuditService.LogAsync(...)` (BR-11/45/49).
- **Notifications**: workflow events → `INotificationService.NotifyAsync(...)` (BR-42).
- **Request workflow**: only via `IRequestWorkflowService` (writes `RequestStatusHistory`, BR-35/36).
- **Soft delete**: deactivate/archive, never physical delete for referenced records (BR-52).
- **Dates**: school timezone `SE Asia Standard Time` / `Asia/Ho_Chi_Minh` via `IClock` (BR-53). Display `dd/MM/yyyy`.
- **UI**: use partials/components in `Pages/Shared` and classes from `components.css`. No inline one-off styles.
  Icons: `<icon name="check" shape="circle" tone="mint" />` (Lucide names). Status = icon + text (never color only).
- **Logging**: `_logger.LogInformation("[UC-29] Lunch registered Student={StudentId} Date={Date} By={User}", ...)`.

## 5. Design system quick reference (Playful Geometric)
bg `#FFFDF5` · fg `#1E293B` · accent violet `#8B5CF6` · secondary pink `#F472B6` · tertiary amber `#FBBF24` ·
quaternary mint `#34D399` · border `#E2E8F0`. Headings **Outfit 700/800**, body **Plus Jakarta Sans**.
Borders 2px dark, hard shadows `4px 4px 0 #1E293B` (hover 6px, active 2px), radii 8/16/24/full,
bouncy easing `cubic-bezier(.34,1.56,.64,1)`, confetti shapes, dot grids, squiggles. Respect reduced-motion.
Key classes: `.btn .btn-candy .btn-outline .btn-ghost .btn-danger`, `.sticker-card`, `.stat-sticker`, `.field`,
`.badge`, `.table-pop`, `.page-head`, `.confetti`, `.empty-state`, `.timeline`, `.tabs`.

## 6. Progress Tracker
Legend: `[x]` done · `[/]` in progress · `[ ]` todo

### Phase 1 – Foundation
- [x] Solution scaffold, packages, dotnet-ef tool
- [x] appsettings (with teammate note) + logging config
- [x] Domain entities + enums
- [x] DbContext + initial migration
- [x] Core services (Messages, Clock, CurrentUser, Scope, Audit, Notification, Settings, Email)
- [x] Program.cs (auth, policies, middleware, migrate+seed)
- [x] Demo seeder
- [x] Design system CSS + site.js + icon tag helper
- [x] Layouts (_PublicLayout, _AppLayout with role sidebar)
- [x] Landing page, Login (demo cards autofill), Logout, Forgot/OTP/Reset, Profile, Change password (UC-01..05)

### Phase 2 – Records & Classes (UC-06..20)
- [x] Students list/detail/create/edit (UC-06/07/08), Import (UC-09), Guardian links & contact (UC-10/11), History (UC-12)
- [x] School years (UC-13), Classes (UC-14), Assign GVCN (UC-15), Assign students (UC-16), Roster (UC-17)
- [x] Schedule editor/viewer (UC-18/19), School calendar (UC-20)

### Phase 3 – Attendance & Leave (UC-21..28)
- [x] Attendance sheet + edit/lock + office correction (UC-21/22), class/child view (UC-23/24)
- [x] Leave submit/review/escalate/approve (UC-25/26/27), summary (UC-28)

### Phase 4 – Lunch (UC-29..35)
- [x] Parent lunch calendar register/cancel (UC-29/30/31), service days + menu (UC-32), meal list (UC-33), adjust (UC-34), summary (UC-35)

### Phase 5 – Requests & Documents (UC-36..44)
- [x] Transfer submit/review/approve/apply (UC-36..39), Document submit/review/issue (UC-40..42), track/cancel (UC-43/44)

### Phase 6 – Notifications, Dashboards, Reports (UC-45..52)
- [x] Notifications (UC-45/46), Announcements (UC-47), role dashboards (UC-48), reports (UC-49..52)

### Phase 7 – Admin (UC-53..60) & polish
- [x] Users, roles/permissions, links, reference data, templates, audit logs, settings, reset password, email outbox
- [x] Responsive/a11y pass, smoke tests, walkthrough

## 7. Session log (append newest at bottom)
- 2026-10-05 – Plan approved. Started Phase 1.
- 2026-10-05 – Completed Phase 1 Foundation: EF Core 8 installed, domain entities (27 SRS + auxiliary tables), SawpDbContext with relational configurations and InitialCreate migration, core services (Messages MSG01-50, Clock BR-53, CurrentUser, ScopeService BR-05..08, AuditService BR-11/45/49, NotificationService, SettingsService, EmailService, RequestWorkflowService, PasswordService), Program.cs with cookie auth and role policies, DemoSeeder with realistic dataset, Playful Geometric tokens/base/components/layout CSS, site.js, IconTagHelper with Lucide SVGs, _PublicLayout, _AppLayout with dynamic role sidebar, Index landing page, and full UC-01..05 auth suite (Login with 1-click/double-click demo cards, Logout, ForgotPassword/OTP/Reset, Profile, ChangePassword, AccessDenied, and role dashboard landing pages). All built with 0 warnings, 0 errors. Starting Phase 2.
- 2026-10-05 – Completed Phase 2 Records & Classes (UC-06..20): Office Students list/filters (UC-06), student detail multi-tab view (UC-06), student create with BR-09 unique code (UC-07), student edit with BR-52 soft status (UC-08), CSV batch import with row validation and error preview (UC-09), link/unlink guardians with BR-12/13 primary contact rules (UC-10, UC-11), chronological administrative history timeline (UC-12), parent children view (UC-06, BR-06), teacher roster (UC-17, BR-07), manage school years & classes (UC-13, UC-14, BR-14/15), assign GVCN with BR-16 single active teacher (UC-15), assign students to class with BR-15 capacity check (UC-16), manage weekly timetable with BR-19 conflict checks (UC-18), teacher & parent weekly schedule viewers with child switcher (UC-19), and school calendar manager with auto-generation and BR-14/53 checks (UC-20). Built with 0 warnings, 0 errors. Starting Phase 3.
- 2026-10-05 – Completed Phase 3 Attendance & Leave (UC-21..28): Attendance sheet + lock + office correction (UC-21/22), student attendance history (UC-24), Parent CreateLeave (UC-25 with BR-24 overlap check & dynamic >3 days BGH notice), Teacher Leave review (UC-26 routine <= 3 days with auto attendance AbsentWithLeave & lunch cancellation BR-29), Board Leave approval (UC-27 escalated > 3 days), Teacher monthly attendance summary (UC-28 with attendance rates & evaluation).
- 2026-10-05 – Completed Phase 4 Lunch Service (UC-29..35): Parent lunch registration & cancellation calendar (UC-29/30/31 with BR-28 08:00 AM cutoff & BR-30 duplicate checks), Service Lunch daily meal list with class preparation matrix (UC-33, BR-32), Staff lunch adjustment after cutoff with mandatory explanation note (UC-34, BR-31), Service menu management with auto-generate month tool (UC-32), Service monthly lunch financial reports (UC-35), and Teacher class-scoped lunch attendance list.
- 2026-10-05 – Completed Phase 5 Requests & Documents (UC-36..44): Parent Class Transfer request (UC-36 with same-grade & BR-15 class capacity verification), Office review & Board escalation (UC-37), Board approve/reject transfer (UC-38), Office apply class transfer to active class assignments (UC-39, BR-37, BR-38), Parent Document request (UC-40), Office review & official document issuance with generated serial number and parent notification (UC-41/42, BR-39/40, MSG36), Unified parent request tracking & cancellation (UC-43/44, BR-36).
- 2026-10-05 – Completed Phase 6 & 7 Dashboards, Announcements, Reports & Admin (UC-45..60): Board Announcements publisher with audience targeting & pin state (UC-47), Board School-wide Executive Reports (UC-49 students/classes, UC-50 attendance rates, UC-52 workflow efficiency), Admin User Management (UC-53 create/toggle active BR-52, UC-55 link staff/guardian profile, UC-60 reset password), Admin Roles & granular permissions matrix (UC-54), Admin Reference Data categories manager (UC-56), Admin System Settings (UC-59), Admin Security Audit Log viewer (UC-58), Simulated Email Outbox & OTP code viewer. Verified full end-to-end database startup, automatic migration, DemoSeeder execution, and HTTP 200 response on all core routes. Build completed with 0 warnings and 0 errors.
