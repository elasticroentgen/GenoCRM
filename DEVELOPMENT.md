# Local development setup

GenoCRM is a .NET 10 Blazor app (Server + WebAssembly) backed by PostgreSQL 16.
This document describes the local setup on a Fedora workstation using rootless
Podman as the container runtime. Everything is installed in user space (no root).

## What is installed where

| Component | Location | Notes |
|---|---|---|
| .NET 10 SDK | `~/.dotnet` | installed via `dotnet-install.sh --channel 10.0` |
| `dotnet-ef` | `~/.dotnet/tools` | EF Core CLI for migrations |
| JetBrains Rider | `~/.local/opt/JetBrains/Rider` | launcher `~/.local/bin/rider`, desktop entry `jetbrains-rider.desktop` |
| Docker Compose v2 | `~/.local/bin/docker-compose` | used by `docker compose` / `podman compose` |
| Podman API socket | `/run/user/$UID/podman/podman.sock` | `systemctl --user enable --now podman.socket` |

Shell environment (PATH, `DOTNET_ROOT`, `DOCKER_HOST`) lives in
`scripts/dev-env.sh`. Add it to your profile once:

```bash
echo 'source ~/dev/GenoCRM/scripts/dev-env.sh' >> ~/.zshrc
```

## Database

Only PostgreSQL runs in a container; the app runs on the host.

```bash
docker compose -f docker-compose.dev.yml up -d     # start (container: genocrm-dev-postgres)
docker compose -f docker-compose.dev.yml down       # stop, keep data
docker compose -f docker-compose.dev.yml down -v    # stop and wipe data
docker exec -it genocrm-dev-postgres psql -U genocrm -d genocrm   # SQL shell
```

Connection: `Host=localhost;Port=5432;Database=genocrm;Username=genocrm;Password=genocrm123`
(this is the default in `GenoCRM/GenoCRM/appsettings.json`).

EF Core migrations are applied automatically on app start (`Program.cs`).
To add a migration:

```bash
dotnet ef migrations add <Name> --project GenoCRM/GenoCRM/GenoCRM.csproj
```

## Running the app

```bash
dotnet run --project GenoCRM/GenoCRM/GenoCRM.csproj --launch-profile https
# http://localhost:5015  /  https://localhost:7273
```

In Rider: open `GenoCRM.sln`, select the `GenoCRM: https` run configuration.
Rider's Docker plugin: Settings → Build, Execution, Deployment → Docker → add a
connection with "Unix socket" `/run/user/1000/podman/podman.sock`.

The `https` launch profile sets the Nextcloud/SMTP test environment variables.
Alternatively copy `.env.example` to `.env`; `Program.cs` loads it on start.

## Importing the production database

```bash
scripts/import-prod-db.sh --host deploy@<production-host>
```

The script runs `pg_dump -Fc` inside the production postgres container over SSH,
streams it to `dumps/` (git-ignored), drops and recreates the local `genocrm`
database and restores into it. Then start the app; newer migrations are applied
automatically.

Options: `--container`, `--db`, `--user`, `--docker-cmd "sudo docker"`,
`--from-file <dump>` (restore an existing dump), `--dump-only`, `--delete-dump`, `-y`.
Defaults can be set in `.env` (`PROD_SSH_HOST`, `PROD_DB_CONTAINER`, ...; see `.env.example`).

The production database contains real member data. Delete dumps when done and
never commit or copy them elsewhere.

## Tests

```bash
dotnet test GenoCRM.Tests/GenoCRM.Tests.csproj
```
