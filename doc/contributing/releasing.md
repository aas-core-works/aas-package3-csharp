# Releasing

## Versioning

We follow [Semantic Versioning].
The version X.Y.Z indicates:

* X is the major version (backward-incompatible w.r.t. command-line arguments),
* Y is the minor version (backward-compatible), and
* Z is the patch version (backward-compatible bug fix).

[Semantic Versioning]: http://semver.org/spec/v1.0.0.html 

## Build Solution for Release

To build the solution for release and publish it, invoke:

```powershell
.\src\Release.ps1
```

If you want to clean a previous release, call:

```powershell
.\src\Release.ps1 -clean
```

This will build and publish the solution in `artefacts/` directory.

In cases of substantial changes to the solution, you need to delete `bin` and `obj` subdirectories beneath `src` as dotnet  will not do that for you. 
We provide a shallow script to save you a couple of keystrokes:

```powershell
.\src\RemoveBinAndObj.ps1
```

# Release to NuGet

There is a dedicated GitHub workflow for publishing the package to NuGet.
Please see the `publish-to-nuget.yml` in [`.github/workflows`].

[`.github/workflows`]: https://github.com/aas-core-works/aas-package3-csharp9-dotnet5/tree/main/.github/workflows
