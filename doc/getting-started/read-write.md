# Read-Writing

We show here how you can open a package for both read/write operations.

We are going to use an instance `packaging` of class [`Packaging`] to interact with the library.
Please see [intro] for more details.

[`Packaging`]: ../api/AasCore.Aas3.Package.Packaging.yml

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

## Putting `Part`'s together

The [Open Packaging Conventions] format is based on parts and relationships.
The parts represent the pieces of data, while relationships model how these pieces relate to each other. 

[Open Packaging Conventions]: https://en.wikipedia.org/wiki/Open_Packaging_Conventions

We wanted to encapsulate as much as possible the underlying format, but we decided to keep the structure of [Open Packaging Conventions].
This means that you need to first write a part, and then establish its relation to other parts (or package itself).

The parts are put to the package using `PutPart` (see [PackageReadWrite] class).

[PackageReadWrite]: ../api/AasCore.Aas3.Package.PackageReadWrite.yml

For example:

```csharp
var part = pkg.PutPart(
    new Uri("/aasx/some-company/data.json", UriKind.Relative),
    "text/json",
    Encoding.UTF8.GetBytes("{}");
```

You can also use streams:

```csharp
using Stream = System.IO.Stream;
using Uri = System.Uri;
using UriKind = System.UriKind;

using Stream stream = ...;

pkg.PutPart(
    new Uri("/aasx/data.json", UriKind.Relative),
    "text/json",
    stream);
```

The `PutPart` function returns a `Part` so that you can easily chain it with other functions (see below for examples).

#### Overwriting & Deleting

If a part already exists at the given URI, it is silently overwritten.
Therefore you need to be careful when you overwrite a part and make sure that the relationships are updated accordingly.

This also applies when deleting the parts.
Since it would be unfortunately inefficient to enforce consistency, the library indeed allows you make your package inconsistent (where relationships point to dangling parts).

### Specs

You establish a part as a spec by calling `MakeSpec` (from [PackageReadWrite] class):
 
```csharp
var part = pkg.PutPart(
    new Uri("/aasx/some-company/data.json", UriKind.Relative),
    "text/json",
    Encoding.UTF8.GetBytes("{}"));
 
pkg.MakeSpec(part);
```

Usually you want to chain the calls:

```csharp
pkg.MakeSpec(
    pkg.PutPart(
        new Uri("/aasx/some-company/data.json", UriKind.Relative),
        "text/json",
        Encoding.UTF8.GetBytes("{}")));
```

## Supplementary Parts

Similar to how you make parts into specs, you relate supplementary parts to the spec parts with `RelateSupplementaryToSpec`:

```csharp
Part spec = pkg.PutPart(...);
Part supplementary = pkg.PutPart(...); 

pkg.RelateSupplementaryToSpec(supplementary, spec);
```

The relation can also be undone:

```csharp
pkg.UnrelateSupplementaryFromSpec(supplementary, spec);
````

## Thumbnail

There can be only one thumbnail per package.

Similar to specs, you make a thumbnail relation from a package to a part:

```csharp
var thumbnail = pkg.PutPart(...);

pkg.MakeThumbnail(thumbnail);
```

If you want to undo the thumbnail, call:

```csharp
pkg.UnmakeThumbnail();
```

## Flushing

Flushing is necessary if you want to make sure that the changes you made to the package are persisted properly.

We do not implement a separate flushing operation, but simply delegate the flushing request to the underlying [System.IO.Packaging.Package] instance.

[System.IO.Packaging.Package]: https://docs.microsoft.com/en-us/dotnet/api/system.io.packaging.package

To flush:

```csharp
pkg.Flush();
```

## Concurrency

Please be careful if you read parts while you are writing.
The library is **NOT** thread-safe, and you need to take care of locking issues yourself.

Additionally, be careful to re-read the proper nuggets of information if you changed them.
For example, capturing the groups of specs by content type becomes invalid if you add a new spec:

```csharp
var specsByContentType = pkg.SpecsByContentType();

foreach(var (contentType, specs) in specsByContentType)
{
    // PutPart with a different content type
    pkg.PutPart(...);

    // specsByContentType is now stale and needs to be re-read.
}
```
