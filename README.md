# ProCricket Auction

An interactive cricket auction manager with server-backed organizer accounts, tournament/player/team screens, auctioneer controls, captain bidding, a projector view, and reports.

The interface uses a Bhagwa Stadium theme: charcoal broadcast surfaces, saffron primary actions, warm gold highlights, and responsive tournament controls.

## Run locally

Requirements: Node.js 20+, npm, and the .NET 10 SDK.

Open two PowerShell terminals at the repository root.

**Terminal 1 — API and account database**

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet restore backend/ProCricketAuction.sln
dotnet run --project backend/src/ProCricketAuction.Api/ProCricketAuction.Api.csproj --no-launch-profile --urls http://127.0.0.1:5080
```

The API creates a local SQLite development database on first start. Swagger/OpenAPI is available at `http://127.0.0.1:5080/openapi/v1.json`.

The database provider is configurable. The development default is SQLite. To select SQL Server in a deployment environment, configure `Database__Provider=SqlServer` and provide the `ConnectionStrings__DefaultConnection` environment variable through your hosting platform's secret store. Do not put production credentials in `appsettings.json`.

**Terminal 2 — website**

```powershell
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`, choose **Create an organizer account**, and sign up with your name, email, and a password of at least 12 characters containing uppercase and lowercase letters, a number, and a symbol. Once registered, the same email/password can be used to sign in again.

## Authentication and access

- ASP.NET Core Identity hashes passwords; plaintext passwords are never stored.
- Sign-in uses an HttpOnly, SameSite cookie with an eight-hour sliding expiration.
- Registration assigns the `Organizer` role only. Public registration cannot create administrators.
- The Super Administrator authorization policy verifies the persisted role, confirmed account email, and normalized designated address (`jadhavpratikraje1998@gmail.com`) on every policy check. Registration and profile editing do not grant or change that role.
- No verified email delivery or first-admin bootstrap workflow is configured yet. Consequently, no account is automatically promoted, and the designated administrator still needs a secure verified setup process before any Super Administrator-only endpoint can be used.
- Login and registration are rate-limited; authentication changes require an antiforgery token.
- Authenticated pages redirect signed-out visitors to login.
- Tournaments, teams, and players are stored in the configured database, scoped to the authenticated organizer. Older browser tournament/team/player data is imported in one database transaction on sign-in when no server tournaments exist, preserving team purses and sold-player assignments.
- Live auction state and bidding are validated and persisted by the server, with SignalR updates for connected auction screens.
- The auctioneer can share `/auction/watch/{auctionId}` as a public, read-only auction display. It does not grant access to tournament management or captain bidding.

To reset an account, remove its row from the local SQLite database only after stopping the API. Never delete or alter the database on a production service.

## Interactive website controls

- Sidebar links open dashboard, tournaments, players, teams, auction room, reports, projector display, settings, and help.
- Workspace selector lists saved tournaments and lets the organizer switch between them; the menu closes when clicking outside, pressing Escape, navigating, or selecting a tournament.
- Delete is also available beside the active tournament on the dashboard and tournament page. It requires confirmation and deletes the selected tournament and its database-owned teams and players.
- Activity opens auction results; the bell shows recent auction activity; the profile dropdown closes on outside click or Escape and provides profile, account settings, password change, help, and sign-out.
- The profile dropdown's About page explains the project's purpose and identifies its creator.
- Profile display-name updates and password changes are validated and saved by the authenticated API. Password changes require the current password and refresh the secure session.
- The dashboard **This tournament** selector opens its reporting-scope menu and closes on outside click or Escape.
- Create tournament, Add team, and Add player save records to the configured database. The tournament sets one starting purse for new teams; short codes and distinct team colors are generated automatically. Team cards expand to show squad members, purchase prices, and recent accepted bids from saved auction events. Adding a player directly to a team records a purchase and checks/deducts that team’s purse.
- Auction controls support server-validated bidding, purse and squad checks, pause/resume, sold/unsold results, and persisted reports. The minimum next-bid increment is ₹2,000; captains may enter any higher direct bid within their selected team's purse.
- The notifications menu closes when you click outside it, press Escape, or follow one of its links.
- New accounts start without sample tournaments, teams, or players. On load, the app removes only the old known built-in sample IDs; other organizer-created browser data is retained.
- Older browser tournament, team, and player records are imported only when the account has no saved tournament. Browser-only bid previews are not treated as accepted server bids or included in auction activity.
- The local SQLite file was inspected read-only before adding the persistent workspace tables. The existing account and database records were preserved; no user records were deleted.

## Build and tests

```powershell
cd frontend
npm run build
npm test
cd ..
dotnet build backend/ProCricketAuction.sln
dotnet test backend/ProCricketAuction.sln
```

## Current limitations

Account credentials, tournament management, team purses, player registrations, accepted bids, auction control transitions, confirmed purchases, and corrections are handled by the API and stored in the configured database. Bids are validated against server state, and connected auction screens receive committed SignalR updates. Dashboard and team purse summaries use saved workspace data; recent bid activity is loaded from saved auction events.

The development database defaults to a local SQLite file. SQL Server can be selected with environment configuration. The API initializes the Identity schema and idempotently creates the `ManagedTournaments`, `ManagedTeams`, and `ManagedPlayers` tables for existing databases; these are data-preserving schema additions and do not delete the older empty scaffold tables. Production deployments still require a reviewed EF migration workflow, persistent database configuration, HTTPS, secret storage, backups, and monitoring. SQL Server operation and synchronized bidding between separately connected production clients have not been verified in this development environment. This application uses virtual purse values and is not a real-money payment system.
