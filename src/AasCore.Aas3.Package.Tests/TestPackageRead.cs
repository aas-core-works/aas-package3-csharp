using Convert = System.Convert;
using Directory = System.IO.Directory;
using File = System.IO.File;
using FileAccess = System.IO.FileAccess;
using FileFormatException = System.IO.FileFormatException;
using FileMode = System.IO.FileMode;
using InvalidDataException = System.IO.InvalidDataException;
using InvalidOperationException = System.InvalidOperationException;
using MemoryStream = System.IO.MemoryStream;
using Path = System.IO.Path;
using SystemPackage = System.IO.Packaging.Package; // renamed
using Uri = System.Uri;
using UriKind = System.UriKind;
using System.Collections.Generic; // can't alias
using System.Linq; // can't alias
using NUnit.Framework; // can't alias

namespace AasCore.Aas3.Package.Tests
{
    public class TestPackageRead
    {
        private static string GetTestResourcesPath()
        {
            return Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                Path.Combine(
                    new[]{
                    "TestResources",
                    $"{nameof(AasCore)}.{nameof(Aas3)}.{nameof(Package)}.{nameof(Tests)}",
                    nameof(TestPackageRead)}
                ));
        }

        /**
         * Execute the test action on the given package opened for reading.
         *
         * <remarks>
         * The files in the <paramref name="expectedDir"/> are grouped for the package.
         * Please make sure that all the file names that you pick are unique.
         * </remarks>
         */
        delegate void SampleReadTestAction(PackageRead package, string expectedDir);

        private static void ForEachReadOfSampleAasx(SampleReadTestAction action)
        {
            foreach (string pth in SampleAasxDir.ListPaths())
            {
                string name = Path.GetFileNameWithoutExtension(pth);

                string expectedDir = Path.Combine(
                    new[] { GetTestResourcesPath(), name });
                const bool record = false;

#pragma warning disable 162
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                // ReSharper disable once HeuristicUnreachableCode
                if (record) Directory.CreateDirectory(expectedDir);
#pragma warning restore 162

                Packaging packaging = new Packaging();

                using var pkgOrErr = packaging.OpenRead(pth);
                if (pkgOrErr.MaybeException != null)
                {
                    throw new AssertionException(
                        $"Failed to open for reading: {pth}",
                        pkgOrErr.MaybeException);
                }

                var pkg = pkgOrErr.Must();

                action(pkg, expectedDir);
            }
        }

        [Test]
        public void Test_listing_specs_against_the_recorded_outputs()
        {
            // Please set to true if you want to re-record the expected outputs.
            const bool record = false;

            ForEachReadOfSampleAasx((pkg, expectedDir) =>
            {
                Table tbl = new Table(new List<string> { "Content Type", "URIs" });
                foreach (var item in pkg.SpecsByContentType())
                {
                    tbl.Add(new List<string>
                    {
                        item.Key,
                        string.Join(
                            ", ",
                            item.Value.Select((spec) => spec.Uri.ToString()))
                    });
                }

                var tblPth = Path.Combine(
                    new[] { expectedDir, "specsTable.txt" });
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (record)
#pragma warning disable 162
                {
                    File.WriteAllText(tblPth, tbl.Render());
                    TestContext.Out.WriteLine($"Recorded: {tblPth}");
                }
#pragma warning restore 162

                Assert.AreEqual(
                    File.ReadAllText(tblPth),
                    tbl.Render());
            });
        }

        [Test]
        public void Test_reading_spec_content_against_the_recorded_outputs()
        {
            // Please set to true if you want to re-record the expected outputs.
            const bool record = false;

            ForEachReadOfSampleAasx((pkg, expectedDir) =>
            {
                HashSet<string> filenameSet = new HashSet<string>();

                foreach (var spec in pkg.Specs())
                {
                    Assert.IsFalse(
                        spec.Uri.IsAbsoluteUri,
                        "Expected a local URI of an AAS spec " +
                        "(*i.e.* an URI without a host), " +
                        "but got an absolute one: {spec.Uri}");

                    string filename = Path.GetFileName(spec.Uri.ToString());

                    // Assume that the file name of the specs is unique in the samples
                    if (filenameSet.Contains(filename))
                    {
                        throw new AssertionException(
                            "Assumed that all sample AASXs contain specs " +
                            "with unique file names, but the file name " +
                            $"for the spec is a duplicate: {filename} " +
                            $"(URI: {spec.Uri}, sample file: {pkg.Path}");
                    }

                    filenameSet.Add(filename);

                    string contentPth = Path.Combine(new[] { expectedDir, filename });

                    // We need to convert Windows newlines to Linux newlines since
                    // we can run the tests on both systems. Additionally, Git will
                    // most probably do the conversion for us anyhow.
                    string content = spec
                        .ReadAllText()
                        .Replace("\r\n", "\n");

                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (record)
#pragma warning disable 162
                    {
                        File.WriteAllText(contentPth, content);
                        TestContext.Out.WriteLine($"Recorded: {contentPth}");
                    }
#pragma warning restore 162

                    string expectedContent = File.ReadAllText(contentPth)
                        .Replace("\r\n", "\n");

                    Assert.AreEqual(expectedContent, content);
                }
            });
        }

        [Test]
        public void Test_listing_supplementary_files_against_the_recorded_outputs()
        {
            // Please set to true if you want to re-record the expected outputs.
            const bool record = false;

            ForEachReadOfSampleAasx((pkg, expectedDir) =>
            {
                Table tbl = new Table(new List<string> { "Content Type", "URI" });
                foreach (var suppl in pkg.Supplementaries())
                {
                    tbl.Add(new List<string>
                    {
                        suppl.ContentType, suppl.Uri.ToString()
                    });
                }

                var tblPth = Path.Combine(
                    new[] { expectedDir, "supplementariesTable.txt" });
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (record)
#pragma warning disable 162
                {
                    File.WriteAllText(tblPth, tbl.Render());
                    TestContext.Out.WriteLine($"Recorded: {tblPth}");
                }
#pragma warning restore 162

                Assert.AreEqual(
                    File.ReadAllText(tblPth),
                    tbl.Render());
            });
        }

        [Test]
        public void Test_getting_a_thumbnail_against_recorded_outputs()
        {
            // Please set to true if you want to re-record the expected outputs.
            const bool record = false;

            ForEachReadOfSampleAasx((pkg, expectedDir) =>
            {
                var thumbnail = pkg.Thumbnail();

                var thumbSummaryPth = Path.Combine(
                    new[] { expectedDir, "thumbnail.txt" });

                var thumbUriText = thumbnail == null
                    ? "Not available"
                    : thumbnail.Uri.ToString();

                var firstBytes = new byte[] { 0, 0, 0, 0 };
                using var stream = thumbnail?.Stream();
                stream?.Read(firstBytes, 0, 4);

                var firstBytesText = string.Join(
                    ", ",
                    firstBytes.Select(
                        aByte => aByte > 32 && aByte < 127
                            ? Convert.ToChar(aByte).ToString()
                            : aByte.ToString()));

                var text = $"URI: {thumbUriText}, " +
                           $"first bytes: {firstBytesText}";

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (record)
#pragma warning disable 162
                {
                    File.WriteAllText(thumbSummaryPth, text);
                    TestContext.Out.WriteLine($"Recorded: {thumbSummaryPth}");
                }
#pragma warning restore 162

                Assert.AreEqual(
                    File.ReadAllText(thumbSummaryPth), text);
            });
        }

        [Test]
        // ReSharper disable once InconsistentNaming
        public void Test_open_stream_against_open_path()
        {
            foreach (string pth in SampleAasxDir.ListPaths())
            {
                Packaging packaging = new Packaging();

                using var pkgPathOrErr = packaging.OpenRead(pth);
                var pkgPath = pkgPathOrErr.Must();

                using var stream = File.OpenRead(pth);
                using var pkgStreamOrErr = packaging.OpenRead(stream);
                var pkgStream = pkgStreamOrErr.Must();

                var uriSpecsPath =
                    pkgPath.Specs().Select(spec => spec.Uri.ToString()).ToList();

                var uriSpecsStream =
                    pkgStream.Specs().Select(spec => spec.Uri.ToString()).ToList();

                Assert.That(uriSpecsPath, Is.EqualTo(uriSpecsStream));
            }
        }

        [Test]
        public void Test_that_part_returns_null_if_it_does_not_exist()
        {
            var pth = SampleAasxDir.Path34Festo();

            Packaging packaging = new Packaging();

            using var pkgPathOrErr = packaging.OpenRead(pth);
            var pkg = pkgPathOrErr.Must();

            Assert.IsNull(
                pkg.FindPart(
                    new Uri(
                        "/the/supplementary/does/not/exist", UriKind.Relative)));
        }

        [Test]
        public void Test_that_must_part_throws_the_adequate_exception()
        {
            var pth = SampleAasxDir.Path34Festo();

            Packaging packaging = new Packaging();

            using var pkgPathOrErr = packaging.OpenRead(pth);
            var pkg = pkgPathOrErr.Must();

            Assert.Catch<InvalidOperationException>(() =>
            {
                pkg.MustPart(
                    new Uri("/the/supplementary/does/not/exist", UriKind.Relative));
            });
        }

        [Test]
        public void Test_that_opening_a_non_package_file_returns_the_exception()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });
            {
                // Create an invalid file w.r.t. Open Package Convention
                File.WriteAllText(pth, "This is not OPC.");
            }

            Packaging packaging = new Packaging();

            {
                using var pkgOrErr = packaging.OpenRead(pth);

                Assert.IsNotNull(pkgOrErr.MaybeException);
                Assert.IsInstanceOf<FileFormatException>(pkgOrErr.MaybeException);
            }
        }

        [Test]
        public void Test_that_opening_a_file_without_origin_returns_the_exception()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });
            {
                // Create an empty Open Package Convention package, *i.e.* an invalid
                // AAS package
                using var pkgOpc = SystemPackage.Open(
                    pth, FileMode.Create, FileAccess.Write);
            }

            Packaging packaging = new Packaging();

            {
                using var pkgOrErr = packaging.OpenRead(pth);

                Assert.IsNotNull(pkgOrErr.MaybeException);
                Assert.IsInstanceOf<InvalidDataException>(pkgOrErr.MaybeException);
            }
        }

        [Test]
        public void Test_that_opening_a_stream_without_origin_returns_the_exception()
        {
            MemoryStream stream = new MemoryStream();
            {
                // Create an empty Open Package Convention package, *i.e.* an invalid
                // AAS package
                using var pkgOpc = SystemPackage.Open(
                    stream, FileMode.Create, FileAccess.Write);
            }

            Packaging packaging = new Packaging();

            {
                using var pkgOrErr = packaging.OpenRead(stream);

                Assert.IsNotNull(pkgOrErr.MaybeException);
                Assert.IsInstanceOf<InvalidDataException>(pkgOrErr.MaybeException);
            }
        }

        [Test]
        public void Test_that_opening_an_empty_stream_returns_the_exception()
        {
            Packaging packaging = new Packaging();

            using MemoryStream stream = new MemoryStream();

            using var pkgOrErr = packaging.OpenRead(stream);

            Assert.IsNotNull(pkgOrErr.MaybeException);
            Assert.IsInstanceOf<FileFormatException>(pkgOrErr.MaybeException);
        }
    }
}