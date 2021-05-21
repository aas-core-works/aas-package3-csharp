# Test Resources

Please put the test resources of your test project into the subdirectory `TestResources/` followed by the project name.

For example, the test resources of `AasCore.Aas3.Package.Tests` go to:

```
src/AasCore.Aas3.Package.Tests/TestResources/AasCore.Aas3.Package.Tests
```

Then you include the test resources in the project file with:

```
<ItemGroup>
  <Folder Include="TestResources\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Folder>
</ItemGroup>
```

This will copy the test resources to the directory where the binaries will be published so that the test programs can find them.

Since the binaries all go to the same directory, we have to include the project name after `TestResources/` to avoid overwriting test resources of the other projects.

We use `TestResources` for a directory name just as convention.
 The files in the output directory (*e.g.*, where the debug solution is published) are thus easier to inspect.
 