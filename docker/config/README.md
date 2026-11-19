# Docker configuration files

The application creates `MinooConfiguration` during startup and expects
`AppConfig.json` beside the published application files.

1. Place the deployment-specific `AppConfig.json` in this directory.
2. Do not commit that file; it may contain internal server names or credentials.
3. Set the SQL Server connection string and (if needed) Redis connection string
   in the root `.env` file, based on `.env.example`.

The Compose file mounts `AppConfig.json` read-only at `/app/AppConfig.json`.
Docker Compose intentionally fails if the file is missing rather than creating
an empty directory in its place.

If a deployment uses `OperationsConfig.json`, mount it in the same way at
`/app/OperationsConfig.json` after confirming the application code requires it.
