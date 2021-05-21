# Read-Writing

We show here how you can open a package for both read/write operations.

We are going to use an instance of [api/AasCore.Aas3.Package.Packaging] to interact with the library.
Please see [intro] for more details.

## Creating a New Package

Creating a new package is straightforward:

```csharp
using var pkg = packaging.Create("/path/to/some/file");
```

Please do not forget to dispose the package if you do not use `using` statement. 

## Opening a Package for Read/Writing

Opening a package for read/writing is similar to how we open a package for reading:

```csharp
using var pkgOrErr = packaging.OpenReadWrite(stream);
```

The result is a discriminated union, either a package, or an exception (see [reading] for more details).

For example, to check for an exception:

```csharp
if (pkgOrErr.MaybeException() != null)
{
    throw new System.ArgumentException(
        "something went wrong", pkgOrErr.MaybeException());
}
```

or to get the package (which will throw the exception for you, if any):

```csharp
var pkg = pkgOrErr.Must();
```

## Writing Specs

Whether you want to over-write existing specs or add a new spec, call `PutSpec`:

```csharp
using Uri = System.Uri;
using UriKind = System.UriKind;

byte[] content = ...;

pkg.PutSpec(
    new Uri("/aasx/data.json", UriKind.Relative),
    "text/json",
    content);
```

You can also use streams:

```csharp
using Stream = System.IO.Stream;
using Uri = System.Uri;
using UriKind = System.UriKind;

using Stream stream = ...;

pkg.PutSpec(
    new Uri("/aasx/data.json", UriKind.Relative),
    "text/json",
    stream);
```

## Writing Supplementary Parts

Similar to how you write specs, you write supplementary parts with `PutSupplementary`:

```csharp
using Uri = System.Uri;
using UriKind = System.UriKind;

byte[] content = ...;

pkg.PutSupplementary(
    new Uri("/aasx/suppl/something.pdf", UriKind.Relative),
    "application/pdf",
    content);
```

Streams can also be written:

```csharp
using Stream = System.IO.Stream;
using Uri = System.Uri;
using UriKind = System.UriKind;

Stream stream = ...;

pkg.PutSupplementary(
    new Uri("/aasx/suppl/something.pdf", UriKind.Relative),
    "application/pdf",
    stream);
```

## Writing a Thumbnail

There can be only one thumbnail per package.
You can add a thumbnail (or overwrite it if it already exists) with:

```csharp
using Uri = System.Uri;
using UriKind = System.UriKind;

byte[] content = ...;

pkg.PutThumbnail(
    new Uri("/thumbnail.png", UriKind.Relative),
    "image/png",
    content,
    true);
```

The last parameter (here set to `true`) determines whether the part of the previous thumbnail should be deleted (or kept in the package).

You can also write a thumbnail from a stream:

```csharp
using Stream = System.IO.Stream;
using Uri = System.Uri;
using UriKind = System.UriKind;

Stream stream = ...;

pkg.PutThumbnail(
    new Uri("/thumbnail.png", UriKind.Relative),
    "image/png",
    stream,
    true);
```

## Flushing

Flushing is necessary if you want to make sure that the changes you made to the package are persisted properly.

We do not implement a separate flushing operation, but simply delegate the flushing request to the underlying [System.IO.Packaging.Package] instance.

[System.IO.Packaging.Package]: https://docs.microsoft.com/en-us/dotnet/api/system.io.packaging.package

To flush:

```csharp
pkg.Flush();
```

## Conflicts

Please be careful if you read parts while you are writing.
The library is **NOT** thread-safe, and you need to take care of locking issues yourself.

Additionally, be careful to re-read the proper nuggets of information if you changed them.
For example, capturing the groups of specs by content type becomes invalid if you add a new spec:

```csharp
var specsByContentType = pkg.SpecsByContentType();

foreach(var (contentType, specs) in specsByContentType)
{
    pkg.PutSpec(...);
    // specsByContentType is now stale and needs to be re-read.
}
```
