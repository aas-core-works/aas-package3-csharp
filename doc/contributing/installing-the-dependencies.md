# Installing the Dependencies

We provide PowerShell scripts to help you install the dependencies and build the solution from the command line.

We assume that both dotnet SDK 8.0 and 9.0 are installed.

The following script installs all the dependencies in one go:

```
.\src\InstallDependencies.ps1
```

This script *should not* require any admin privileges.

**Updating the dependencies**. Whenever the dependencies change, the install script needs to be re-run:

```
.\src\InstallDependencies.ps1
```

Obsolete dependencies will not be removed (*e.g.*, unused NuGet packages in `packages/` or AASX samples).
You need to manually remove them.
