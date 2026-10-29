# MinooWebCore

Starter solution for a modern ASP.NET Core MVC login experience targeting .NET 10.

## Included
- ASP.NET Core MVC / .NET 10
- Cookie Authentication
- Remember me (`IsPersistent`) support
- Login form validation and show/hide password
- Forgot-password button wired to a placeholder endpoint
- Responsive industrial-inspired design and animated orbit of subsidiary name badges
- Demo authentication service that can later be replaced with an API or database-backed implementation

## Run
Requires the .NET 10 SDK.

```bash
dotnet restore MinooWebCore.sln
dotnet run --project src/MinooWebCore/MinooWebCore.csproj
```

Open the HTTPS/HTTP URL printed in the console.

## Demo credentials
- Username: `admin`
- Password: `Minoo@123`

**Important:** This is only a starter/demo authentication provider. Do not deploy it as-is. Replace `DemoAuthenticationService` with your real API/database authentication and remove the demo credentials before deployment. In production, use HTTPS, a proper secret store, and review cookie settings for your environment.

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
