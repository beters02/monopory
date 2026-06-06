# Rent Rush Achievements

Reusable achievements backend, contracts, client, and evaluation helpers.

## Projects

- `src/RentRush.Achievements.Contracts` contains API DTOs and service interfaces. Pack this for every consumer.
- `src/RentRush.Achievements.Client` contains the HTTP client used by games/tools to authenticate, read state, report events, and check cosmetics.
- `src/RentRush.Achievements.Core` contains local achievement evaluation helpers that can be reused by a server, tests, or offline tools.
- `src/RentRush.Achievements.Server` is the deployable ASP.NET Core backend. It is not packed as a NuGet package.

## Build

```powershell
dotnet build RentRush.Achievements.slnx
```

## Pack NuGet Packages

```powershell
dotnet pack src\RentRush.Achievements.Contracts -c Release
dotnet pack src\RentRush.Achievements.Client -c Release
dotnet pack src\RentRush.Achievements.Core -c Release
```

The `.nupkg` files are written under each project's `bin\Release` folder.

## Run The Server

```powershell
dotnet run --project src\RentRush.Achievements.Server
```

Configure Postgres with one of:

- `ConnectionStrings:AchievementsPostgres`
- `Achievements:PostgresConnectionString`
- `ACHIEVEMENTS_POSTGRES`

If no Postgres connection string is configured, the server uses in-memory storage.

## Moving This Folder To Its Own Repo

From the original game repo, either copy this folder into a new repository or preserve history with a subtree split.

If `RentRush.Achievements` has not been committed yet, commit it first:

```powershell
git add RentRush.Achievements
git commit -m "Extract Rent Rush achievements projects"
```

Then split it:

```powershell
git subtree split --prefix=RentRush.Achievements -b rentrush-achievements-split
```

Then create the new remote repository and push that split branch.

If you want to preserve only the older standalone backend history from before this extraction scaffold, split the original service folder instead:

```powershell
git subtree split --prefix=AchievementsService -b achievements-service-split
```

After publishing packages, replace local game-side achievement client/model files with package references:

```xml
<PackageReference Include="RentRush.Achievements.Contracts" Version="0.1.0" />
<PackageReference Include="RentRush.Achievements.Client" Version="0.1.0" />
```

Keep game-specific catalogs, cosmetics, and s&box/Forkbox auth glue in the game unless you intentionally want this shared repo to own them.
