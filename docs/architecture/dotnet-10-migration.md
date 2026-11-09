# .NET 10 migration plan

This repository stays on `net8.0` because that is the installed SDK (`global.json` pins 8.0.403 with latest 8.0 feature roll-forward). .NET 8 reaches end of support on 10 November 2026.

When an SDK 10 install is available:

1. Change `global.json` and `Directory.Build.props` to `net10.0`.
2. Re-test `Microsoft.Extensions.AI` 9.5 and `ModelContextProtocol` 1.4.0, which already include net8 and newer targets.
3. Run `dotnet test` and the API and Blazor smoke commands.
4. Do not change retrieval behavior in the same commit as the target-framework change.

This plan was not executed.
