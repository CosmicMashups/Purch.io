# Login and access redesign

Status: planned, nothing built yet. Decisions below were agreed question by question.

## Why

Today a Register signs staff in with a device pairing code plus a PIN, and the server finds the person by comparing the PIN
against every active staff member. The PIN is both the username and the password, PINs must be globally unique within a
business (which leaks that a PIN is valid), unattended devices pair with a reusable PIN, and device pairing codes never
expire. Roles also do not depend on the device type for staff sessions.

## Identity

- **Supabase Auth** checks email and password. Public sign-up is turned off; accounts are created only by our backend
  through the admin API, with the email marked confirmed. **No email verification** (no SMTP budget).
- Permissions never live in Supabase metadata. Membership, duties and branches live in our database.
- After Supabase accepts the password, the .NET API issues **its own short-lived access token and refresh token**, as it
  does today (tenant, role/duties, device id). Existing authorization checks keep working.
- One account (email) can have **memberships in several businesses**, with its own duties and branches in each. Sign-in
  on a personal device asks which business when there is more than one. A paired device already belongs to one business.
- **Forgot password** uses Supabase's built-in reset email. Known risk: the built-in sender is a testing service, rate
  limited and normally only delivers to project team members, so resets may not reach real inboxes until a custom sender
  is added. Plan for an admin-issued reset link as a later fallback.

## Supabase setup (step 2)

Settings the API reads (all optional at startup, so an existing deployment keeps running; email sign-in reports it is
not configured until they are set):

- `SUPABASE_AUTH_URL`: the project URL, for example `https://abcdefgh.supabase.co`.
- `SUPABASE_AUTH_SERVICE_KEY`: the service-role key. Creates logins. Never sent to a client.
- `SUPABASE_AUTH_ANON_KEY`: the public key, used to check a password. Optional; the service key works too.

In the Supabase dashboard (Authentication, Sign In / Providers, Email): turn **off** "Allow new users to sign up".
Logins are created only by our backend through the admin API, with the email already confirmed. Local deployment mode
and the tests use a credential table in our own database instead (`LocalCredentials`).

Endpoints so far: `POST /auth/sign-in` (email, password, optional tenantId; returns tokens, or the list of businesses to
choose from) and the existing `POST /onboarding/bootstrap`, which now also creates the owner's account and Admin
membership when an email and password are given. The old PIN login is untouched until step 8.

Known stopgaps: a personal-device session gets the single API role the existing checks understand (Warehouse if the
person has that duty, otherwise Cashier; none means no sign-in), and a staff member with several branches gets tenant
scope because a token carries one scope id. Both are replaced when sessions are tied to devices (step 6).

## Device pairing (step 3)

- Admin: `POST /devices/pairing-requests` (name, type, branch, and the Register for a customer display) creates a
  Pending device and returns its one-time code once. `POST /devices/{id}/pairing-code` makes a fresh code (and, on a
  paired device, ends everything it holds). `POST /devices/{id}/revoke` takes it out of service.
- Device: `POST /devices/pair` exchanges the code for a long-lived credential (kept in `localStorage` on the web).
  `POST /devices/session` starts a session from that credential: Kiosk, Order board, Kitchen display and Customer
  display get an access token and a refresh token (renewed by the ordinary `/auth/refresh`); a Register or Warehouse
  device is recognised but gets no token until a person signs in (step 5).
- Revoking or pairing again bumps the device's session version, revokes its credential and refresh tokens, and the
  middleware also refuses any token of a revoked device, so a session already issued stops at once.
- Web: Business, Devices creates devices and shows the code in a dialog; `/pair` is the one pairing screen for every
  kind of device. The old per-device pairing PIN screens redirect there. The Flutter client still uses the old pairing.
- The old permanent pairing code, pairing PIN and `/kiosk/session` style endpoints still exist until step 8.

## Staff enrolment (step 4)

- Admin or Manager: `POST /staff/invites` (name, email, role, duties, branches) returns a single-use token once, shown as a
  link and QR code (`/enrol/<token>`, valid three days). A Manager can only invite and manage Staff. A staff invite needs at
  least one duty and one branch. `GET /staff/invites` lists the waiting ones, `DELETE /staff/invites/{id}` cancels one.
- Person (no session): `POST /enrol/preview` shows what the link is for; `POST /enrol/redeem` sets the password (a new
  account) or checks the existing one (someone already registered in another business), sets the personal PIN, creates the
  membership and signs them in. The link works once even if two people race for it (xmin row version).
- `GET /staff/members`, `PUT /staff/members/{id}` (role, duties, branches, active; the business always keeps one active
  Admin; a change ends that person's sessions) and `POST /staff/members/{id}/reset-link` (a single-use link to choose a new
  password, which also ends their sessions). Changes are written to the audit log.
- Web: Business, Staff is now the new people list, an invite form with the link and QR dialog, waiting links, and the reset
  link. `/enrol/:token` is the public page the person opens. The old PIN-only staff list is no longer shown (the rows
  remain until step 8).

## Locking and unlocking a till (step 5)

- A paired Register or Warehouse device shows its roster: `POST /devices/roster` (body: the device credential) lists the active
  people who may work on it (Admin and Manager always; staff only with the device's duty and its branch).
- `POST /devices/unlock` (credential, person, PIN) checks the PIN against that one person. Five wrong PINs lock that person
  out for five minutes (counted on the person, not the device); the answer says how many tries are left. A correct PIN
  issues a device-bound session: the device's id, branch and session version are in the token, so revoking the device ends
  it, and the device's duty decides a staff member's role (Register: Cashier, Warehouse device: Warehouse; Admin and
  Manager keep theirs). It renews through `/auth/refresh` and stays on the device.
- The web has `/unlock` (names, then a PIN pad), a Lock button on paired devices, and locks itself after five idle minutes
  (taps and key presses keep it awake). A signed-out paired device returns to the lock screen instead of the email login.
- The email login page now signs in with `/auth/sign-in` (asking which business when there are several) and falls back to
  the older back-office login for owners who only have that, until step 8.
- Much of the till still looks a person up as a `User`. A membership is shown to that code as a User built on the fly
  (`MembershipUserProjection`, never saved), so shifts, approvals, discounts and reports work for people who signed in this
  way; manager approval PINs are checked against memberships too. The older PIN sign-in never sees a membership.
- Not done: the offline PIN cache. The web has no offline selling, and storing PIN hashes on a device lets anyone holding it
  guess four-digit PINs offline, so it belongs with the Flutter client, which does sell offline, and needs a deliberate
  design there. The idle time is fixed at five minutes; making it an Admin setting is a later change.

## The access rule (step 6)

- Tabs by role, in the web and in Flutter: Admin and Manager get Home, Cashier, Inventory and Business; a Cashier gets the
  Cashier page only; a Warehouse user gets Inventory only; an unknown role gets the Cashier page only. Home, with the
  revenue dashboard, is for Admin and Manager alone.
- Where someone lands, and where they are sent for a page they have no tab for: Admin and Manager the dashboard, a
  Cashier the Cashier page, a Warehouse user Inventory. The web does this in `RequireTab`, Flutter in the router redirect.
  A role with a single tab gets no bottom navigation bar in Flutter.
- The API needed no change: the roles each kind of session gets already decide what it can call. `AccessMatrixTests` now
  pins it down: a Cashier on a Register reaches the till only; a Warehouse device Inventory only; a Manager everything but
  Devices wherever they unlock; an Admin every area; and the kiosk, order board, kitchen display and customer display
  reach none of the staff pages.

## Registering a business

Creates the tenant, its first branch and the owner's Supabase account, and signs the owner in with email and password.
This is an ordinary **personal-device session**, not a registered device. There is no "Admin" device type.

## Staff accounts

1. Admin or Manager opens Add Staff and enters name and email, the **duties** the person is qualified for (Cashier,
   Warehouse, Kitchen) and the branch or branches.
2. The system produces a **single-use enrolment link and QR code** that expires. The admin shows it to the staff member.
3. The staff member opens it on their own phone, sets a password and a personal PIN. No email is sent and none is verified.
4. A person who already has an account (another business) just gains the new membership.
5. Admin and Manager are qualified for every duty. Nobody can self-register into a business.

Resetting a staff member's password without email: the admin issues a new single-use link/QR the same way.

## Devices

Registered devices are created by an Admin on the Devices page, with a type and a branch.

| Device type | Duty it maps to | Who signs in |
| --- | --- | --- |
| Register | Cashier | Staff, with a personal PIN (see Sessions) |
| Warehouse | Warehouse | Staff, with a personal PIN |
| Kiosk | none | Nobody. The paired device is the credential |
| Order board | none | Nobody |
| Kitchen display | none (shows the Kitchen page) | Nobody |
| Customer display | none | Nobody. Paired to **one Register**, which relays its live cart through the server |

Pairing: the admin picks type and branch, the system shows a **one-time code valid for about 10 minutes**. The device
enters it and receives its own long-lived, **revocable device credential**. There is no reusable key or password. Revoking
a device from the Devices page ends its sessions at once. Re-pairing needs a new code. A paired device only accepts users
who belong to its business, and its branch.

Customer display keeps the same-browser second window as a simple option for a one-computer till.

## Personal devices

Not registered. A browser or the app on anyone's phone or computer: sign in with email and password and see only the
pages for the person's own duties. Admin sees everything. Manager sees everything except Admin-only tiles (Devices,
Business settings). Warehouse-only staff see Inventory only. Selling is **not** available here: a till must be a paired
Register, because receipt numbers, the cash drawer and the printer belong to a device.

## Access rule

Effective access = what the device type allows, limited by what the person's duties allow.

- On a Register, a person qualified for Cashier sees the Cashier page and its sub-pages only. A Warehouse-only person
  is refused at sign-in ("You are not assigned to this device type").
- On a Warehouse device, only Inventory pages.
- Admin and Manager keep full access wherever they sign in.
- Home (dashboard and revenue) is for Admin and Manager only.
- Multiple signed-in sessions are allowed (a manager's personal device and a warehouse person borrowing it each get
  their own session).

## Sessions on a paired Register

The device stays paired. The staff session **locks after about 5 minutes idle** (Admin can change it) and is unlocked
with the same person's **PIN**, or switched to another person by choosing their name. The PIN is checked against that one
user only, with a lockout after a few wrong tries. The PIN is a real password because the person is chosen first. Staff
PIN hashes for that branch are cached on the device so unlocking and selling **work offline**. First sign-in on a device
(email and password) needs a connection. Sales are attributed to the person who unlocked.

## Your example, mapped

- Device B (Cashier), User 3 (all-rounder): pair once, then User 3 picks their name and enters their PIN. Sees the
  Cashier page only. User 9 (Warehouse only) is refused.
- Device G (manager's personal device): User 2 signs in with email and password and sees every page. User 9 signs in on
  it as himself and sees Inventory only. The two sessions are independent.
- Devices C, D, E, F: pair once with a one-time code. Nobody signs in. They show the Customer display, Order board,
  Kitchen display and Kiosk pages.

## Replacing what exists

One release replaces the old mechanisms: staff PIN login at a pairing code, permanent pairing codes, unattended pairing
PINs, and the separate admin email login. Existing staff and devices stay as records and **re-enrol**: staff receive a
single-use link to set an email, password and new PIN; each device is re-paired with a one-time code.

## Open points and risks

- No email verification means email is only a login name. Typos are not caught, and the single-use link is the only
  proof of invitation.
- Password reset by the built-in Supabase email may not be delivered (see above).
- The owner has no self-service recovery if the only Admin forgets their password and the reset email never arrives.
  Recommend a second Admin account, or later: owner recovery codes.
- A Supabase outage stops first sign-in but not an already-paired till (PIN unlock works offline).
- PIN lockout thresholds, idle time, and enrolment link lifetime are to be chosen during build.

## Build order

1. Data model: memberships, duties, branches, one-time pairing codes, device credentials, enrolment links.
2. Supabase Auth wiring (public sign-up off, admin-created accounts) and token exchange in the API.
3. Pairing and device credentials; Devices page; revocation.
4. Add Staff, enrolment link/QR, PIN setup, reset links.
5. Register lock, PIN switch, offline PIN cache, lockout.
6. Access rule in the API and in web and Flutter navigation (Home for Admin and Manager only, landing page per duty).
7. Customer display pairing and server relay.
8. Remove the old mechanisms and re-enrol existing staff and devices.
