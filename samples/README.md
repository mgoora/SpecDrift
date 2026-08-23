# DriftyApi — sample fixture

A small ASP.NET Web API 2 client KYC registry, built to be analysed by Spec Drift.

`openapi.yaml` is the contract as an architect wrote it in Phase 1. `DriftyApi/` is the
implementation, faithful to that contract as of Phase 2. Phase 3 then simulated six
months of maintenance on the **code only** — `openapi.yaml` has not been touched since
Phase 1 and never will be.

**The spec is intentionally stale.** It documents an endpoint that was never built,
stays silent about one that was, and its description of `DELETE` is no longer true.
That gap between spec and code is the entire point of this fixture — see
[`DriftyApi/EXPECTED.md`](DriftyApi/EXPECTED.md) for the ten drifts, each with its spec
location (JSON Pointer), its code location (`file:line`), and what a correct Spec Drift
report should say about it.

## Building

`DriftyApi/` is .NET Framework 4.8 with a classic `.csproj` and `packages.config`. It
builds **only** with MSBuild or Visual Studio, after a NuGet restore — `dotnet build`
will not work on it (there is no SDK-style project here to convert to; being a genuine
legacy project is the fixture's purpose).

```powershell
msbuild DriftyApi\DriftyApi.csproj -t:restore -p:RestorePackagesConfig=true -p:SolutionDir=(Resolve-Path .)\
msbuild DriftyApi\DriftyApi.csproj -p:Configuration=Debug
```

(`RestorePackagesConfig` and `SolutionDir` are only needed because there's no `.sln`
file in this folder; opening the project in Visual Studio and using its Restore /
Build commands works without them.)

## Running

```powershell
"C:\Program Files\IIS Express\iisexpress.exe" /path:DriftyApi /port:8099
```

The registry seeds itself with six clients on first request — no database, no
configuration. Try:

```
GET  http://localhost:8099/api/clients/11111111-1111-1111-1111-111111111111
POST http://localhost:8099/api/clients/search   (search is POST in code — see SD003)
```
