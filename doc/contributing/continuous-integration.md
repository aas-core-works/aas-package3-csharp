# Continuous Integration

To establish confidence in the software as well as to continuously maintain the code quality, we provide scripts to run pre-merge checks on your local machine as well as Github workflows to run remotely.

All the following scripts *should not* require any administration privilege.

## Building for Debug

To build the solution for debugging and testing (*e.g.*, with other libraries), invoke:

```powershell
.\src\BuildForDebug.ps1
```

To clean the build, call:
```powershell
.\src\BuildForDebug.ps1 -clean
```

In cases of substantial changes to the solution, you need to delete `bin` and `obj` subdirectories beneath `src` as dotnet  will not do that for you. 
We provide a shallow script to save you a couple of keystrokes:

```powershell
.\src\RemoveBinAndObj.ps1
```

## Reformatting Code

We use `dontet-format` to automatically fix the formatting of the code to comply with the style guideline.
To reformat the code in-place, call:

```powershell
.\src\FormatCode.ps1
```

## Generate Doctests

We use [doctest-csharp] for [doctests].
To extract the doctests and generate the corresponding unit tests:

```powershell
.\src\Doctest.ps1
```

[doctest-csharp]: https://github.com/mristin/doctest-csharp
[doctests]: https://en.wikipedia.org/wiki/Doctest 

## Running Checks Locally

We bundled all the checks in a single script, `src\Check.ps1`. 
If you want to run all the checks, simply call:

```powershell
.\src\Check.ps1
```

The script `src\Check.ps1` will inform you which individual commands were run.
In case of failures, you can just run the last failing command.

Please see the source code of `src\Check.ps1` for more details.

Our tests with external dependencies use environment variables to specify the location of the dependencies.
While `src\Check.ps1` and related scripts set up the expected locations in the environment automatically, you need to adjust your test setting accordingly.

## Github Workflows

Github Actions allow for running continuous integration on Github servers.
For a general introduction, see [Github's documentation on Actions].

[Github's documentation on Actions]: https://docs.github.com/en/actions 

The specification of our Github workflows can be found in [`.github/workflows`] directory.
Please see the corresponding `*.yml` files for more details.

[`.github/workflows`]: https://github.com/aas-core-works/aas-package3-csharp/tree/main/.github/workflows
