# Style Guide

We subscribe to [ReSharper's InspectCode tool] for enforcing a general coding style guide.

[ReSharper's InspectCode tool]: https://www.jetbrains.com/help/resharper/InspectCode.html

We also follow a couple of solution-specific guidelines which we document here.

## Line Width

We enforce max. **90 characters** per line.

This makes it easy for visibility-impaired people to read the code in large font.
This limit also makes it possible to diff in vertical panes in IDE on a laptop.

## Exceptions *versus* errors

Exceptions are meant for parts of the logic which should *very* rarely occur:

* We prefer to return tuples `(result, error)` in most situations instead.
 For example, if you think your clients would write `try ... catch ...` in order to steer program logic, use errors.

* The reason for this decision is that exceptions incur a substantial overhead (see [this StackOverflow question about the cost of exceptions in C#]).
  Notably, imagine if the client needs to run your operation thousands of times — the exceptions would be simply prohibitively expensive for such use cases.

[this StackOverflow question about the cost of exceptions in C#]: https://stackoverflow.com/questions/891217/how-expensive-are-exceptions-in-c

* For public functions we also provide companion functions prefixed with `Must...`, following the Golang convention, which raise exceptions instead of returning errors.
They are simply meant as shortcuts for use cases where error handling can be handled in a sloppy way (*e.g.*, for experimental code).

## Pre-conditions and Post-conditions

Use [design-by-contract] by implicitly writing checks using `if`-statements.

For pre-conditions, raise `System.ArgumentException` (and related subclasses such as `System.ArgumentOutOfRangeException`).

For post-conditions, raise `System.InvalidOperationException`.

## No Asterisk Usings

Since we do not assume the developers to have the full knowledge of the third-party libraries, help the reader by prefixing the used types.

For example, instead of writing:

```csharp
using System.IO.Directory;
```

write:

```csharp
using Directory = System.IO.Directory;
```

Notable exceptions here are `System.Linq`, `System.CommandLine` and other libraries which introduce [extensions].
Please note in your code when this is the case to differentiate from cases where you simply forgot to alias the used type.

[extensions]: https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods

## TODOs

We use [opinionated-csharp-todos] to enforce a common style on all the TODO comments.

[opinionated-csharp-todos]: https://github.com/mristin/opinionated-csharp-todos

Please distinguish between issues and TODOs left in code.
The issues concern the features and broader refactorings.
The TODOs in code are meant to be fixed soon or mark code spots which will be affected by a future refactoring or bug fixing.

Please use `// DONT-CHECK-IN: ` to mark the parts of code which you do not want to make part of the pull request.
[Opinionated csharp-todos] will alert you accordingly before you commit or merge in the code.
