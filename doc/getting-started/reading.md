# Reading

Using the instance `packaging` of class [`Packaging`] (see [entry-point.md]), we open a package for reading:

[`Packaging`]: ../api/AasCore.Aas3.Package.Packaging.yml

```csharp
var packaging = new AasCore.Aas3.Package.Packaging();

using var pkgOrErr = packaging.OpenRead(
        "/path/to/some/file");
```

The result is an instance of a [discriminated union], an either a package or an exception.

[discriminated union]: https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/discriminated-unions  
You can check for an exception:

```
if (pkgOrErr.MaybeException() != null)
{
    throw new System.ArgumentException(
        "something went wrong", pkgOrErr.MaybeException());
}
```

The instance of the package, assuming a happy path, is retrieved using `Must()`:

```
var pkg = pkgOrErr.Must();
```

This mechanism based on a [discriminated union] allows you to check the packages in a much more efficient manner, while using a try-catch mechanism would be less intuitive and slower (see the [best practices on exceptions in dotnet documentation]).

[best practices on exceptions in dotnet documentation]: https://docs.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions

## Parts

Parts are the cornerstone of a package indicating a unit, analogue to files in a file system.
We distinguish between three categories of parts in context of AAS package:

* Specs,
* Supplementaries, and
* Thumbnail.

Each part has a content type (given as a [MIME type]) and gives you access to its content.

[MIME type]: https://developer.mozilla.org/en-US/docs/Web/HTTP/Basics_of_HTTP/MIME_types

A part is modeled as an instance of class [`Part`].
You can read all bytes or all text from it, or open a read stream:

[`Part`]: ../api/AasCore.Aas3.Package.Part.yml

```csharp
var part = ...;

byte[] content = part.ReadAllBytes();

string text = part.ReadAllText();

using System.IO.Stream stream = part.Stream(); 
```

If you open a stream, do not forget to close it yourself!

### Specs

Specs define the data of your administration shell.

You can list all the specs available in the package with:

```csharp
foreach(AasCore.Aas3.Package.Part spec in pkg.Specs())
{
    // do something with spec
}
```

You can also group the specs by their content type:

```csharp
var specsByContentType = pkg.SpecsByContentType();
if(specsByContentType.Contains("text/json"))
{
    var spec = specsByContentType["text/json"].First();
   
    // Do something with JSON spec
}
else if(specsByContentType.Contains("text/xml"))
{
    var spec = specsByContentType["text/xml"].First();
    
    // Do something with XML spec
}
else
{
    // Report that we could not find neither JSON nor XML
}
```

According to [Details of the Asset Administration Shell v3], specs should all represent the same data model albeit in a different format.
Multiple equivalent models per content type are also possible.

## Supplementary Materials

If you know the [URI] of a supplementary part within the package, you can directly access it:

[URI]: https://en.wikipedia.org/wiki/Uniform_Resource_Identifier

```csharp
using Uri = System.Uri;
using UriKind = System.UriKind;

AasCore.Aas3.Package.Part? suppl = pkg.FindPart(
    new Uri("/aasx/suppl/something.pdf", UriKind.Relative));
    
if(suppl != null)
{
    // Do something with the supplementary part.
    // For example, read all the bytes.
    byte[] content = suppl.ReadAllBytes();
}
```

Otherwise, if you want to inspect all the supplementary parts for a given spec, you can list them:

```csharp
var spec = pkg.MustPart(...);

foreach(AasCore.Aas3.Package.Part suppl in pkg.SupplementariesFor(spec))
{
    // Do something with suppl
}
```

## Thumbnail

Here is how to query for a thumbnail of the package:

```csharp
AasCore.Aas3.Package.Part? thumb = pkg.Thumbnail();

if (thumb != null)
{
    // Do something with the thumbnail.

    // For example, read all the bytes and show them.
    byte[] content = thumb.ReadAllBytes();
    
    // ...
}
```
