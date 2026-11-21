# MinooWebCore

Starter solution for a modern ASP.NET Core MVC login experience targeting .NET 10.

## Included
- ASP.NET Core MVC / .NET 10
- Cookie Authentication
- Remember me (`IsPersistent`) support
- Login form validation and show/hide password
- Forgot-password button wired to a placeholder endpoint
- Responsive industrial-inspired design and animated orbit of subsidiary name badges
- Database-backed login using the string `Users.UserCode` identity

## Run
Requires the .NET 10 SDK.

```bash
dotnet restore MinooWebCore.sln
dotnet run --project src/MinooWebCore/MinooWebCore.csproj
```

Open the HTTPS/HTTP URL printed in the console.

## Authentication

The login form validates the supplied username against `Users.UserCode` without numeric conversion, preserving leading zeroes. Password validation continues through the existing password decryption service. Database credentials and encryption configuration are read from `src/MinooWebCore/appsettings.json` in this deployment.

## MinooWebReports single sign-on

The parent portal issues a short-lived, one-time ticket when an authenticated user opens either Reports application. MinooWebReports redeems that ticket through `POST /api/sso/reports/redeem` using the shared `X-Reports-Client-Secret` header, then creates its own secure authentication cookie.

Configure the following values directly in `src/MinooWebCore/appsettings.json` under `Sso:Reports` and `Sso:TicketLifetimeSeconds`. Use the Reports server IP address in `BaseUrl`, and set the same high-entropy `ClientSecret` in MinooWebReports under `Sso:Parent:ClientSecret`. Both applications and the redemption endpoint should use HTTPS in production. The user login identifier is the string `Users.UserCode`; it is preserved as a string across the SSO boundary.

## Logo assets
The orbital marks currently show company names as text labels, not fabricated official logos. Put verified logo files under `wwwroot/images/logos/` and replace the text badges in `Views/Account/Login.cshtml` with `<img>` tags. The project is designed to work without external image/CDN requests.

## Forgot password
The button posts to `/Account/ForgotPassword`. The action currently returns a friendly placeholder view; add your own recovery workflow there.

## Main folders
- `Controllers/` MVC controllers
- `Models/` view models
- `Services/` authentication abstraction + demo implementation
- `Views/Account/` login and forgot-password views
- `wwwroot/css/` styling and orbital animation
- `wwwroot/js/` password visibility behavior

## Report server selection

The report registration form supports two execution modes for SQL reports:

- Default server: the report uses the currently selected server of the user.
- Multiple servers: the report stores one or more selected VahedeTejariSN values and the Reports application validates that the user is authorized for every configured target before execution.

The database change is in database/scripts/20261108_AddMultiServerReportTargets.sql. Run it against the database containing dbo.ReportDefinitions before deploying. Existing single-server report definitions are migrated to the new server-target table automatically.
