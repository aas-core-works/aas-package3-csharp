using Encoding = System.Text.Encoding;
using File = System.IO.File;
using FileAccess = System.IO.FileAccess;
using FileFormatException = System.IO.FileFormatException;
using FileMode = System.IO.FileMode;
using InvalidDataException = System.IO.InvalidDataException;
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
        [Test]
        public void Test_that_must_returns_the_exception()
        {
            using var stream = new MemoryStream();

            var packaging = new Packaging();

            using var pkgOrErr = packaging.OpenRead(stream);

            Assert.Catch<FileFormatException>(() => { pkgOrErr.Must(); });
        }

        [Test]
        public void Test_grouping_specs_by_content_type()
        {
            var packaging = new Packaging();

            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            {
                using var pkg = packaging.Create(pth);
                pkg.MakeSpec(
                    pkg.PutPart(
                        new Uri("/aasx/some-company/data.json", UriKind.Relative),
                        "text/json",
                        Encoding.UTF8.GetBytes("{}")));

                pkg.MakeSpec(
                    pkg.PutPart(
                        new Uri(
                            "/aasx/some-company/data1.json", UriKind.Relative),
                        "text/json",
                        Encoding.UTF8.GetBytes("{x: 1}")));

                pkg.MakeSpec(
                    pkg.PutPart(
                        new Uri("/aasx/some-company/data.xml", UriKind.Relative),
                        "text/xml",
                        Encoding.UTF8.GetBytes("<something></something>")));

                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenRead(pth);
                var pkg = pkgOrErr.Must();

                var specsByContentType = pkg.SpecsByContentType();

                Assert.That(specsByContentType.Keys.ToList(),
                    Is.EquivalentTo(new List<string> { "text/json", "text/xml" }));

                // Test this for the Readme.
                Assert.That(specsByContentType.ContainsKey("text/json"));
                Assert.That(specsByContentType.ContainsKey("text/xml"));

                foreach (var item in specsByContentType)
                {
                    var contentType = item.Key;
                    var specs = item.Value;

                    // Test only the values for JSON as it is the only relevant case.
                    // The case with XML is trivial.
                    if (contentType != "text/json") continue;

                    Assert.AreEqual(2, specs.Count);
                    Assert.AreEqual(
                        "/aasx/some-company/data.json",
                        specs[0].Uri.ToString());

                    Assert.AreEqual(
                        "/aasx/some-company/data1.json",
                        specs[1].Uri.ToString());
                }
            }
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

        [Test]
        public void Test_querying_a_thumbnail_in_a_package_without_one()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            Packaging packaging = new Packaging();

            {
                using var pkg = packaging.Create(pth);
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenRead(pth);
                var pkg = pkgOrErr.Must();
                Assert.IsNull(pkg.Thumbnail());
            }
        }

        [Test]
        public void Test_the_exception_if_thumbnail_relationship_exists_without_part()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            Packaging packaging = new Packaging();

            // Initialize
            {
                using var pkg = packaging.Create(pth);
                pkg.MakeThumbnail(
                    pkg.PutPart(
                        new Uri("/some-thumbnail.txt", UriKind.Relative),
                        "text/plain",
                        Encoding.UTF8.GetBytes("some content")));
                pkg.Flush();
            }

            // Remove the thumbnail as part, but not as relationship
            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();

                var oldThumbnail = pkg.Thumbnail();
                if (oldThumbnail == null)
                {
                    throw new AssertionException(
                        $"Unexpected {nameof(oldThumbnail)}");
                }

                pkg.RemovePart(oldThumbnail);
                pkg.Flush();
            }

            // Try to read the non-existing thumbnail part
            {
                using var pkgOrErr = packaging.OpenRead(pth);
                var pkg = pkgOrErr.Must();

                Assert.Catch<InvalidDataException>(() => pkg.Thumbnail());
            }
        }
    }
}
