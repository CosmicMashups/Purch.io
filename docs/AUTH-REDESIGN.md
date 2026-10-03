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
