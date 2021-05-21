# Installing the Dependencies

We provide PowerShell scripts to help you install the dependencies and build the solution from the command line.

The solution relies on many solution-specific tools (such as a tool for code formatting) as well as third-party libraries.

The solution dependencies are split into three different categories:

* Tools for the check workflow (such as Resharper CLI and dotnet-format),
* The third-party libraries, and
* Sample AASX packages used for integration testing (provided at 
  http://admin-shell-io.com/samples/).

The following script installs all the dependencies in one go:

```
.\src\InstallSolutionDependencies.ps1
```

This script *should not* require any admin privileges.

**Updating the dependencies**. Whenever the dependencies change, the install script needs to be re-run:

```
.\src\InstallSolutionDependencies.ps1
```

Obsolete dependencies will not be removed (*e.g.*, unused NuGet packages in `packages/` or AASX samples).
You need to manually remove them.
