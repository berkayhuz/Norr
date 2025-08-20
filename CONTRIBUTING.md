# Contributing

Thanks for considering contributing!

## Development
- .NET SDK: 9.0.x (see `global.json`)
- Build: `dotnet build Norr.sln -c Release`
- Pack: `dotnet pack <project> -c Release -o out`

## Pull Requests
- Prefer small, focused PRs.
- Follow the code style enforced by `.editorconfig`.
- Ensure CI passes.

## Release
- Tag with `vX.Y.Z` and push; NuGet publish is automated.