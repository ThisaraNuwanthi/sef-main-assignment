# Viva Preparation — SKCA Enrol

Questions an examiner is likely to ask, grouped by build phase, with short
answers and the file to open when explaining. Practise answering **without**
reading the answer first, then open the file and trace the code yourself.

---

## Phase 1 — Scaffold and backend core

### 1. Why is capacity checked in the DTO *and* as a database check constraint?
The DTO rule (`[Range(1, 100)]` in `Dtos/ClassDtos.cs`) gives the user a friendly
400 message early. The check constraint (`CK_Classes_Capacity_Positive` in
`Data/AppDbContext.cs`) is the last line of defence: even if a future bug, a
seed script or someone using SQL directly bypasses the API, PostgreSQL refuses
the row. The test `Database_check_constraint_blocks_zero_capacity_even_if_api_validation_is_bypassed`
writes straight through the DbContext (skipping validation) and proves the DB rejects it.

### 2. A parent with a valid JWT requests `GET /api/children/7`, which belongs to someone else. What returns the 403?
1. `[Authorize(Roles = "Parent,Admin")]` on `ChildrenController.Get` passes — the role is fine.
2. `ChildService.GetAsync` calls `LoadAuthorisedAsync`, which loads child 7 and compares
   `child.ParentId` with the user id from the token (`User.GetUserId()` reads the `sub` claim).
3. They differ and the user is not Admin, so it throws `ForbiddenException`.
4. `GlobalExceptionHandler` maps that to a 403 ProblemDetails response.

Role checks answer "what kind of user are you?"; ownership checks answer "is this *your* record?". Both are needed.

### 3. Why is `ParentId` taken from the token and never from the request body?
The body is controlled by the client, so a malicious parent could send someone
else's id. The JWT is signed with our secret key, so its `sub` claim cannot be
changed without the signature failing. (`ChildService.CreateAsync`.)

### 4. Why does the photo validator read the first bytes of the file?
The `Content-Type` header is chosen by the client and can lie (e.g. a script
renamed to `.png`). Real JPEGs start with `FF D8 FF` and PNGs with `89 50 4E 47`
("magic bytes"). We also pick the saved file extension ourselves and generate
a GUID file name, so a user can never choose the path. (`Services/PhotoStorage.cs`.)

### 5. How does JWT authentication work here, step by step?
Login → `AuthService.LoginAsync` checks the password with `BCrypt.Verify` →
`TokenService.CreateToken` builds a token with `sub`, `email`, `name`, `role`
claims, signed with HMAC-SHA256 using `Jwt:Key`. The client sends
`Authorization: Bearer <token>`. `AddJwtBearer` in `Program.cs` validates the
signature, issuer, audience and expiry, then `RoleClaimType = "role"` makes
`[Authorize(Roles = ...)]` work.

### 6. Why BCrypt instead of SHA-256 for passwords?
BCrypt is deliberately slow and salts every hash automatically, so the same
password gives different hashes and brute-forcing a leaked table is expensive.
SHA-256 is fast, which helps attackers.

### 7. Why does login give the same error for "unknown email" and "wrong password"?
So attackers cannot use the login form to discover which emails have accounts
(user enumeration).

### 8. Where do secrets live?
Never in git. Locally: `dotnet user-secrets` (stored in your user profile, outside
the repo). In production: environment variables (`Jwt__Key`,
`ConnectionStrings__Default`) set on the hosting platform. `Program.cs` fails
fast at startup if the JWT key is missing or shorter than 32 characters.

### 9. How are errors returned?
Services throw typed exceptions (`Common/AppExceptions.cs`). One
`GlobalExceptionHandler` converts them to RFC 7807 ProblemDetails with the right
status (400/401/403/404/409). Unknown errors become a generic 500 and are logged
with the stack trace, which is never sent to the client. Postgres unique and
check violations are mapped to 409 and 400.

### 10. Why does `DayOfWeek` stay an integer in the DB while other enums are text?
Text enums are readable in the DB. But sorting "by day" on text would give
Friday, Monday, Saturday… (alphabetical). As an integer (0 = Sunday) the sort
follows the week.

### 11. What happens on startup?
`Program.cs` runs EF migrations (`Database:MigrateOnStartup`), then the seeder
if `Seed:Enabled` is true and the Users table is empty. Seeding is in code, not
`HasData`, because BCrypt hashes change on every run and would create a new
migration every time.

### 12. How do the integration tests get a real database?
`tests/.../Infrastructure/ApiFactory.cs`: if `TEST_DB_CONNECTION` is set (CI's
postgres service container) it uses that server, otherwise Testcontainers starts
a throw-away `postgres:16-alpine` container. Each test class gets its own
freshly-migrated database, deleted at the end. `WebApplicationFactory<Program>`
runs the real API in memory.

### 13. What does "Scoped" mean in `AddScoped<IClassService, ClassService>()`?
One instance per HTTP request. It matches `AppDbContext`'s lifetime: a
DbContext is not thread-safe and should not live longer than one request.

### 14. Why can't two `[Authorize]` attributes with different roles on class and action give "either"?
Multiple `[Authorize]` attributes are combined with AND. That's why
`ChildrenController` has plain `[Authorize]` on the class and the roles on each
action.
