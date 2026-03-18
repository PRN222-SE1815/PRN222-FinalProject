# Copilot Instructions – FinalProject Student Management System (Refactor from Assignment 2)

## 0) Project Context
You are assisting with **refactoring Assignment 2** into **FinalProject – Student Management System** using:

- ASP.NET Core Razor Pages + Areas
- EF Core DB-First + SQL Server
- SignalR (chat)
- Wallet + MoMo (deposit only)
- Gemini tool-calling AI assistant
- CSV/XLS/XLSX import/export (gradebook)

Primary solution layers:

- `Presentation` (UI only)
- `BusinessLogic` (business rules, validation, authorization, orchestration, transactions)
- `DataAccess` (EF Core repository/query/command only)
- `BusinessObject` (DB-first scaffolded entities/enums/constants)

Mandatory dependency direction:

`Presentation -> BusinessLogic -> DataAccess -> BusinessObject`

Do not violate this layering.

---

## 1) Core Refactor Objective
Refactor old Assignment-2 style implementation to support FinalProject features:

1. Registration **Cart + one-time Checkout**
2. **Wallet/MoMo** payment flow
3. **Transaction History**
4. Gradebook management (teacher input/edit, admin approve/publish/lock)
5. Grade import/export (`.csv`, `.xls`, `.xlsx`)
6. **Grade Appeal** (replace Quiz entirely)
7. SignalR chat with room membership validation
8. AI assistant via Gemini tool-calling (no direct DB access by AI)

---

## 2) Scope Changes (Critical)
- **Remove Quiz/auto-grade completely** from all layers.
- Replace Quiz domain/use-cases with **Grade Appeal**.
- Registration must support **cart + one-time checkout**.
- Add **wallet transaction history** and registration/payment history.
- Add **grade import/export** and gradebook lifecycle (draft/approval/published/locked).

If legacy code conflicts with this scope, prioritize FinalProject scope.

---

## 3) Hard Architecture Rules
1. `Presentation` must never call `DbContext`, entity sets, or repositories directly.
2. `Presentation` only calls `BusinessLogic` services/interfaces.
3. `BusinessLogic` handles:
   - authz/authn checks from claims-derived user identity,
   - validations,
   - orchestration across repositories,
   - transaction boundaries,
   - mapping to DTO/view models,
   - `ServiceResult`.
4. `DataAccess` contains only EF Core query/command logic.
5. No business workflow/orchestration in repositories.
6. Keep methods asynchronous end-to-end (`async/await` only).
7. Never use `.Result`, `.Wait()`, sync-over-async patterns.
8. Use `AsNoTracking()` for read-only queries.
9. Avoid N+1; prefer projection and proper Include/select shaping.
10. Lists must support paging + deterministic stable sorting.

---

## 4) DB-First Discipline
Because this project is DB-first:

1. If statuses/tables/columns/constraints change:
   - update SQL schema first,
   - re-scaffold entities/context,
   - then update enums/constants/DTOs/services/UI.
2. Do not invent unsupported DB states.
3. Do not add fake enum values not backed by DB constraints and existing data model.

Always align code with current DB schema (`SchoolManagementDbContext` and scaffolded entities).

---

## 5) Naming & Contract Standards
Use these naming conventions consistently:

- Interfaces: `IRegistrationCartService`, `IGradeAppealService`, etc.
- Implementations: `RegistrationCartService`, `GradeAppealService`
- DTOs: `AddToCartRequest`, `CheckoutCartRequest`, `GradeAppealRequest`, `PagedResult<T>`
- Status enums/constants:
  - `EnrollmentStatus`
  - `CartStatus`
  - `RegistrationOrderStatus`
  - `GradeBookStatus`
  - `GradeAppealStatus`
  - `WalletTransactionType`

For business operations return `ServiceResult`:

- `IsSuccess`
- `ErrorCode`
- `Message`
- `Data`

Business validation failures must return failed `ServiceResult`, not throw exceptions.  
Throw exceptions only for unexpected system failures.

---

## 6) Authorization & Identity Rules
- Use cookie authentication.
- Resolve `UserId` and role from claims in backend.
- All critical authorization must be enforced in `BusinessLogic`.
- Never rely on UI-only authorization checks.
- Enforce ownership checks (student can access only their own cart/orders/wallet/appeals, etc.).

---

## 7) Critical Invariants to Preserve

### 7.1 Registration Cart + Checkout
Checkout must be **atomic** in one DB transaction:

- Validate semester registration window
- Validate prerequisite completion
- Validate duplicate enrollment restrictions
- Validate credit limits (semester min/max policy context)
- Validate schedule time conflicts
- Validate class open/capacity
- Validate wallet balance
- Create `RegistrationOrders` + `RegistrationOrderItems`
- Deduct wallet balance
- Insert `WalletTransactions`
- Create `Enrollments`
- Update `ClassSections.CurrentEnrollment`
- Finalize cart state

Must prevent overbooking and double-spend with proper transaction + concurrency handling.

### 7.2 MoMo
- MoMo is **deposit to wallet only**.
- Callback/notify handling must be idempotent (process exactly once).
- Never double-credit wallet for same payment transaction (`MoMoOrderId` uniqueness respected).

### 7.3 Transaction History
History must include at least:

- deposit
- checkout payment
- refund
- failed/cancelled transaction traces

### 7.4 Gradebook
Support full lifecycle:

- teacher input/edit grades
- admin approval flow
- publish and lock
- audit logs on score changes
- import from `.csv/.xls/.xlsx`
- export to Excel
- students can see only published grades

### 7.5 Grade Appeal
- This fully replaces quiz-related workflow.
- One appeal per student per gradebook/class as defined by DB uniqueness.
- Only allow submission after gradebook is published.
- No duplicate active/duplicate appeal creation.
- Teacher/admin review must use valid DB statuses only.
- Approved score changes must write audit logs.

### 7.6 Chat + AI
- Validate chat room membership in backend before read/send actions.
- AI assistant must not query DB directly; it can only use approved services/tools.

---

## 8) Refactor Guidance (from Assignment 2 to FinalProject)
When converting old code:

1. Identify old quiz modules/pages/services and remove them safely.
2. Replace quiz entry points with grade appeal pages/services/workflow.
3. Extract business rules from UI/controllers/pages into BusinessLogic services.
4. Move direct EF calls from UI to repositories/services following layer rules.
5. Convert sync code to async.
6. Introduce DTOs and mapping boundaries instead of exposing entities to UI.
7. Centralize status strings into constants/enums aligned with DB values.
8. Add tests (or at minimum service-level validation scenarios) for critical invariants.

---

## 9) Coding Behavior Expectations for Copilot
When generating code, Copilot must:

1. **Plan first**, then propose file-by-file changes.
2. Respect existing project structure and naming.
3. Generate minimal, focused diffs (no unrelated formatting churn).
4. Keep methods short and explicit.
5. Add clear comments only for non-obvious business rules.
6. Prefer guard clauses for validation.
7. Ensure cancellation token support where appropriate.
8. Ensure every read query is explicit about tracking behavior.
9. Avoid hidden magic strings; use constants/enums.
10. Never reintroduce Quiz artifacts.

---

## 10) Output Format Required from Copilot
For each request, always respond in this order:

1. **Plan**
2. **Files to change**
3. **Code blocks**
4. **Notes**

In “Files to change”, include brief purpose per file.  
In “Notes”, list assumptions, migration notes, and follow-up tasks (if any).

---

## 11) Forbidden Actions
- Do not invent tables/columns/statuses that are not in DB schema.
- Do not write direct SQL inside Razor Pages/UI.
- Do not bypass repository/service pattern.
- Do not use synchronous EF methods in request flow.
- Do not reintroduce Quiz pages/services/tables/code paths.

---

## 12) Suggested Initial Backlog for Refactor
Use this order to reduce risk:

1. Foundation:
   - ServiceResult base contracts
   - enums/constants for statuses
   - authentication/claims helper
2. Registration Cart:
   - add-to-cart/remove/list/cart-summary
3. Checkout atomic workflow:
   - validation + transaction + order + enrollment + wallet deduction
4. MoMo deposit + callback idempotency
5. Wallet/transaction history pages + APIs
6. Gradebook lifecycle + audit log
7. Grade import/export
8. Grade Appeal flow
9. SignalR chat membership enforcement
10. AI assistant tool-calling orchestration

---

## 13) Quality Checklist Before Finalizing Any Change
- [ ] Layering rule is respected
- [ ] Async only, no `.Result` / `.Wait()`
- [ ] Business validations in service layer
- [ ] Transaction boundary present for checkout critical section
- [ ] Correct status transitions only
- [ ] Idempotency ensured for payment callback
- [ ] `AsNoTracking()` used for reads
- [ ] Paging + stable sorting for list endpoints
- [ ] No Quiz code remains
- [ ] Output follows: Plan -> Files to change -> Code blocks -> Notes

---

## 14) Repository-Specific Notes from Current Schema
Current schema already includes entities relevant to scope:
`RegistrationCart`, `RegistrationCartItem`, `RegistrationOrder`, `RegistrationOrderItem`,
`StudentWallet`, `WalletTransaction`, `PaymentTransaction`,
`GradeBook`, `GradeItem`, `GradeEntry`, `GradeBookApproval`, `GradeAuditLog`, `GradeAppeal`,
and chat/AI entities.

Therefore, favor implementing business workflows on top of existing schema first; avoid schema expansion unless absolutely required and approved.