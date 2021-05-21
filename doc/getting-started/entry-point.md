# Entry Point

The class [`Packaging`] provides the main entry to the library:

[`Packaging`]: ../api/AasCore.Aas3.Package.Packaging.yml

```csharp
var packaging = new AasCore.Aas3.Package.Packaging();
```

We decided not to use a static class, or a singleton to allow for mocking (in client tests) as well as avoid problems in the future if we are to add configuration options *etc*.

All operations on packages are performed using the resulting `packaging` instance.