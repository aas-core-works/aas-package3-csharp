# aas-package3-csharp

[![Test](https://github.com/aas-core-works/aas-package3-csharp/actions/workflows/test.yml/badge.svg?branch=main)](https://github.com/aas-core-works/aas-package3-csharp/actions/workflows/test.yml
) [![Check style](https://github.com/aas-core-works/aas-package3-csharp/actions/workflows/check-style.yml/badge.svg)](https://github.com/aas-core-works/aas-package3-csharp/actions/workflows/check-style.yml
) [![Check Doc](https://github.com/aas-core-works/aas-package3-csharp/actions/workflows/check-doc.yml/badge.svg)](https://github.com/aas-core-works/aas-package3-csharp/actions/workflows/check-doc.yml
) [![Coverage Status](https://coveralls.io/repos/github/aas-core-works/aas-package3-csharp/badge.svg?branch=main)](https://coveralls.io/github/aas-core-works/aas-package3-csharp?branch=main
) [![Nuget](
https://img.shields.io/nuget/v/AasCore.Aas3.Package)](
https://www.nuget.org/packages/AasCore.Aas3.Package
)

Aas-package3-csharp is a library for reading and writing packaged file format of an [Asset Administration Shell (AAS)] in C#.

[Asset Administration Shell (AAS)]: https://www.plattform-i40.de/PI40/Redaktion/DE/Downloads/Publikation/Details_of_the_Asset_Administration_Shell_Part1_V3.html

## Status

The library is thoroughly tested and ready to be used in production.

Both NET Standard 2.1 and NET 5 are supported.

## Documentation

The documentation is available at https://aas-core-works.github.io/aas-package3-csharp/doc.

### Teaser

Here is are short snippets to demonstrate how you can use the library.

To create and write to a package:

```csharp
// General packaging handler to be shared accross the program
var packaging = new AasCore.Aas3.Package.Packaging();

// Create a package
{
    byte[] specContent = ...;
    byte[] thumbnailContent = ...;
    byte[] supplementaryContent = ...;

    using var pkg = packaging.Create("/path/to/some/file");
    pkg.PutSpec(
        new Uri("/aasx/some-company/data.json", UriKind.Relative),
        "text/json",
        specContent);

    pkg.PutThumbnail(
        new Uri("/some-thumbnail.png", UriKind.Relative),
        "image/png",
        thumbnailContent,
        true);

    pkg.PutSupplementary(
        new Uri("/aasx-suppl/some-company/some-manual.pdf", UriKind.Relative),
        "application/pdf",
        supplementaryContent);

    pkg.Flush();
}
```

To read from the package:

```csharp
// General packaging handler to be shared accross the program
var packaging = new AasCore.Aas3.Package.Packaging();

// Read from the package
byte[] specContent;
byte[] thumbnailContent;
byte[] supplementaryContent;

{
    using var pkgOrErr = packaging.OpenRead(
        "/path/to/some/file");

    var pkg = pkgOrErr.Must();

    // Read the specs
    var specsByContentType = pkg.SpecsByContentType();
    if (!specsByContentType.Contains("text/json"))
    {
        throw new ArgumentException("No json specs");
    }
    var spec = specsByContentType["text/json"].First();
    specContent = spec.ReadAllBytes();

    // Read the thumbnail
    thumbnailContent = pkg.Thumbnail().ReadAllBytes();

    // Read the supplementary file
    supplementaryContent = pkg
        .FindPart(
            new Uri("/aasx-suppl/some-company/some-manual.pdf", UriKind.Relative))
        .ReadAllBytes();
}
```

Please see the full documentation at [https://aas-core-works.github.io/aas-package3-csharp/doc] for more details.

## Installation

The library is available on NuGet at: https://www.nuget.org/packages/AasCore.Aas3.Package/

## Versioning

The name of the library indicates the supported version of the [Asset Administration Shell (AAS)].

In case of `aas-package3-csharp`, this means that the Version 3 of the [Asset Administration Shell (AAS)] is supported.

We follow [Semantic Versioning] to version the library.
The version X.Y.Z indicates:

[Semantic Versioning]: http://semver.org/spec/v1.0.0.html

* X is the major version (backward-incompatible),
* Y is the minor version (backward-compatible), and
* Z is the patch version (backward-compatible bug fix).
