# Local run guide

## Requirements

- Visual Studio 2022, .NET 8 SDK, and SQL Server Developer or Express reachable with the configured local connection string. SQL Express is not required.
- SQL Server Developer or Express reachable with a local connection string. SQL Express is not required.
- `dotnet-ef` for the optional migration drift gate.
- To use chat/calling, configure the optional ICE/TURN settings for your network; TURN credentials must be supplied locally and must not be committed.

## Configure and start

1. Set the database connection string and JWT signing configuration through .NET user-secrets or environment variables. Do not place real passwords or signing keys in source control. The API project uses its configured connection string name from `appsettings.json`.
2. Apply only pending migrations to the intended local database:

   ```powershell
   dotnet ef database update --project src/XiangqiOnline.Infrastructure --startup-project src/XiangqiOnline.API
   ```

   Inspect the target connection before applying. Never drop/reset a database to resolve startup issues.

3. In Visual Studio 2022, select `XiangqiOnline.API` as the startup project and press **Ctrl+F5** (or **F5**). The launch profile is configured to use the single HTTP base URL `http://localhost:5080`; the browser opens from Visual Studio.

   Alternatively, run the same profile from the repository root:

   ```powershell
   dotnet run --project src/XiangqiOnline.API --launch-profile XiangqiOnline.API
   ```

   The MVC/Razor/Vue interface and API share this origin. Do not start Vite when using this single-URL setup.

## Run verification

From repository root:

```powershell
./scripts/verify-phase8.ps1
```

The script records each gate as PASS, FAIL, or SKIPPED in `TestResults/phase8-verification.json`. SQL-backed integration tests require the configured SQL Server. Browser E2E and real WebRTC media are explicitly skipped because this checkout has no browser runner or two-client media fixture.

## Key routes

- MVC/Vue: `/Account/Login`, `/Account/Register`, `/Lobby/Index`, `/AiGame/Index`.
- AI game: `/AiGame/Play/{game-guid}`.
- Home: `http://localhost:5080/`.
- API health: `/api/v1/health`; API documentation is available in Development when enabled.
