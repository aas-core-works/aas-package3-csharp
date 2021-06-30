using ArgumentException = System.ArgumentException;
using DirectoryNotFoundException = System.IO.DirectoryNotFoundException;
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
using System.Linq; // can't alias
using NUnit.Framework; // can't alias

namespace AasCore.Aas3.Package.Tests
{
    public class TestCreate
    {
        [Test]
        public void Test_adding_spec_to_new_package()
        {
            var originalContent = Encoding.UTF8.GetBytes("some content");

            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            Packaging packaging = new Packaging();

            {
                using var pkg = packaging.Create(pth);

                pkg.MakeSpec(
                    pkg.PutPart(
                        new Uri("/aasx/some-company/data.txt", UriKind.Relative),
                        "text/plain",
                        originalContent));

                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenRead(pth);
                var pkg = pkgOrErr.Must();
                var content = pkg.Specs().First().ReadAllBytes();
                Assert.That(content, Is.EqualTo(originalContent));
            }
        }

        [Test]
        public void Test_adding_supplementary_file_to_new_package()
        {
            var supplContent = Encoding.UTF8.GetBytes(
                "some supplementary content");

            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            Packaging packaging = new Packaging();

            {
                using var pkg = packaging.Create(pth);

                var spec = pkg.MakeSpec(
                    pkg.PutPart(
                        new Uri("/aasx/some-company/data.txt", UriKind.Relative),
                        "text/plain",
                        Encoding.UTF8.GetBytes("some spec content")));

                pkg.RelateSupplementaryToSpec(
                    pkg.PutPart(
                        new Uri(
                            "/aasx/some-company/suppl.txt", UriKind.Relative),
                        "text/plain",
                        supplContent),
                    spec);

                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenRead(pth);
                var pkg = pkgOrErr.Must();

                var gotContent = pkg
                    .SupplementaryRelationships()
                    .First()
                    .Supplementary
                    .ReadAllBytes();

                Assert.That(gotContent, Is.EqualTo(supplContent));
            }
        }

        [Test]
        public void Test_adding_thumbnail_to_new_package()
        {
            var originalContent = Encoding.UTF8.GetBytes("some content");

            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            Packaging packaging = new Packaging();

            {
                using var pkg = packaging.Create(pth);
                pkg.MakeThumbnail(
                    pkg.PutPart(
                        new Uri("/some-thumbnail.txt", UriKind.Relative),
                        "text/plain",
                        originalContent));
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenRead(pth);
                var pkg = pkgOrErr.Must();
                Assert.IsNotNull(pkg.Thumbnail());
                var gotContent = pkg.Thumbnail()!.ReadAllBytes();
                Assert.That(gotContent, Is.EqualTo(originalContent));
            }
        }

        [Test]
        public void Test_creating_a_package_in_a_stream()
        {
            Packaging packaging = new Packaging();

            var originalContent = Encoding.UTF8.GetBytes("some content");

            MemoryStream stream = new MemoryStream();
            {
                using var pkg = packaging.Create(stream);
                pkg.MakeThumbnail(
                    pkg.PutPart(
                        new Uri("/some-thumbnail.txt", UriKind.Relative),
                        "text/plain",
                        originalContent));
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenRead(stream);
                var pkg = pkgOrErr.Must();
                Assert.IsNotNull(pkg.Thumbnail());
                var gotContent = pkg.Thumbnail()!.ReadAllBytes();
                Assert.That(gotContent, Is.EqualTo(originalContent));
            }
        }

        [Test]
        public void Test_the_exception_when_creating_a_package_at_non_reachable_path()
        {
            var packaging = new Packaging();

            Assert.Catch<DirectoryNotFoundException>(() =>
            {
                packaging.Create("/this/path/can/not/possibly/exist");
            });
        }

        [Test]
        public void Test_the_exception_when_creating_a_package_in_read_only_stream()
        {
            var packaging = new Packaging();

            Assert.Catch<ArgumentException>(() =>
            {
                using var stream = new MemoryStream(new byte[] { }, false);
                packaging.Create(stream);
            });
        }
    }

    public class TestOpening
    {
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
                using var pkgOrErr = packaging.OpenReadWrite(pth);

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
                using var pkgOrErr = packaging.OpenReadWrite(pth);

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
                using var pkgOrErr = packaging.OpenReadWrite(stream);

                Assert.IsNotNull(pkgOrErr.MaybeException);
                Assert.IsInstanceOf<InvalidDataException>(pkgOrErr.MaybeException);
            }
        }

        [Test]
        public void Test_that_opening_an_empty_stream_returns_the_exception()
        {
            Packaging packaging = new Packaging();

            using MemoryStream stream = new MemoryStream();

            using var pkgOrErr = packaging.OpenReadWrite(stream);

            Assert.IsNotNull(pkgOrErr.MaybeException);
            Assert.IsInstanceOf<FileFormatException>(pkgOrErr.MaybeException);
        }
    }

    public class TestModify
    {
        [Test]
        public void Test_modify_package_in_a_stream()
        {
            Packaging packaging = new Packaging();

            var originalContent = Encoding.UTF8.GetBytes("some content");
            var uri = new Uri("/some-thumbnail.txt", UriKind.Relative);

            MemoryStream stream = new MemoryStream();

            // Create
            {
                using var pkg = packaging.Create(stream);
                pkg.MakeThumbnail(
                    pkg.PutPart(
                        uri,
                        "text/plain",
                        originalContent));
                pkg.Flush();
            }

            var newContent = Encoding.UTF8.GetBytes("another content");

            // Modify
            {
                using var pkgOrErr = packaging.OpenReadWrite(stream);
                var pkg = pkgOrErr.Must();
                pkg.MakeThumbnail(
                    pkg.PutPart(
                        uri,
                        "text/plain",
                        newContent));
            }

            // Read
            {
                using var pkgOrErr = packaging.OpenRead(stream);
                var pkg = pkgOrErr.Must();
                Assert.IsNotNull(pkg.Thumbnail());
                var gotContent = pkg.Thumbnail()!.ReadAllBytes();
                Assert.That(gotContent, Is.EqualTo(newContent));
            }
        }

        [Test]
        public void Test_overwriting_a_spec()
        {
            var originalContent = Encoding.UTF8.GetBytes("some old content");
            var newContent = Encoding.UTF8.GetBytes("new content");

            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            var uri = new Uri("/aasx/some-company/data.txt", UriKind.Relative);

            Packaging packaging = new Packaging();

            {
                using var pkg = packaging.Create(pth);
                pkg.MakeSpec(
                    pkg.PutPart(
                        uri,
                        "text/plain",
                        originalContent));
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();
                pkg.MakeSpec(
                    pkg.PutPart(
                        uri,
                        "text/plain",
                        newContent));
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenRead(pth);
                var pkg = pkgOrErr.Must();
                var content = pkg.Specs().First().ReadAllBytes();
                Assert.AreEqual(
                    Encoding.UTF8.GetString(newContent),
                    Encoding.UTF8.GetString(content));
            }
        }

        [Test]
        public void Test_overwriting_a_supplementary()
        {
            var originalContent = Encoding.UTF8.GetBytes("some old content");
            var newContent = Encoding.UTF8.GetBytes("new content");

            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            Packaging packaging = new Packaging();

            var uri = new Uri("/aasx/suppl/data.txt", UriKind.Relative);

            {
                using var pkg = packaging.Create(pth);

                var spec = pkg.MakeSpec(
                    pkg.PutPart(
                        new Uri("/aasx/some-company/data.txt", UriKind.Relative),
                        "text/plain",
                        Encoding.UTF8.GetBytes("some spec content")));

                pkg.RelateSupplementaryToSpec(
                    pkg.PutPart(uri, "text/plain", originalContent),
                    spec);
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();
                pkg.PutPart(uri, "text/plain", newContent);
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenRead(pth);
                var pkg = pkgOrErr.Must();
                var content = pkg.FindPart(uri)?.ReadAllBytes();

                if (content == null)
                {
                    throw new AssertionException("Unexpected null content");
                }

                Assert.AreEqual(
                    Encoding.UTF8.GetString(newContent),
                    Encoding.UTF8.GetString(content));
            }
        }

        [Test]
        public void Test_modify_thumbnail_and_delete_existing()
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

            var newContent = Encoding.UTF8.GetBytes("some new content");

            // Put new content
            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();

                var oldThumbnail = pkg.Thumbnail();
                if (oldThumbnail == null)
                {
                    throw new AssertionException(
                        $"Unexpected {nameof(oldThumbnail)}");
                }

                pkg.UnmakeThumbnail();
                pkg.RemovePart(oldThumbnail);

                pkg.MakeThumbnail(
                    pkg.PutPart(
                        new Uri("/another-thumbnail.txt", UriKind.Relative),
                        "text/plain",
                        newContent));
                pkg.Flush();
            }

            // Read new content
            {
                using var pkgOrErr = packaging.OpenRead(pth);
                var pkg = pkgOrErr.Must();
                Assert.IsNotNull(pkg.Thumbnail());
                var gotContent = pkg.Thumbnail()!.ReadAllBytes();
                Assert.That(gotContent, Is.EqualTo(newContent));
            }
        }

        [Test]
        public void Test_modify_thumbnail_and_dont_delete_existing()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            Packaging packaging = new Packaging();

            var originalContent = Encoding.UTF8.GetBytes("some content");
            var originalUri = new Uri("/some-thumbnail.txt", UriKind.Relative);

            // Initialize
            {
                using var pkg = packaging.Create(pth);
                pkg.MakeThumbnail(
                    pkg.PutPart(
                        originalUri,
                        "text/plain",
                        originalContent));
                pkg.Flush();
            }

            var newContent = Encoding.UTF8.GetBytes("some new content");

            // Put new content
            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();

                pkg.MakeThumbnail(
                    pkg.PutPart(
                        new Uri("/another-thumbnail.txt", UriKind.Relative),
                        "text/plain",
                        newContent));
                pkg.Flush();
            }

            // Read new and old content
            {
                using var pkgOrErr = packaging.OpenRead(pth);
                var pkg = pkgOrErr.Must();
                Assert.IsNotNull(pkg.Thumbnail());
                var gotContent = pkg.Thumbnail()!.ReadAllBytes();
                Assert.That(gotContent, Is.EqualTo(newContent));

                var oldContent = pkg.MustPart(originalUri).ReadAllBytes();
                Assert.That(oldContent, Is.EqualTo(originalContent));
            }
        }

        public class TestDelete
        {
            [Test]
            public void Test_deleting_a_spec()
            {
                using TemporaryDirectory tmpdir = new TemporaryDirectory();
                string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

                Packaging packaging = new Packaging();

                var uri = new Uri("/aasx/some-company/data.txt", UriKind.Relative);

                // Add another spec just to make sure that not *all* specs are deleted
                var anotherUri = new Uri(
                    "/aasx/some-company/anotherData.txt", UriKind.Relative);

                {
                    using var pkg = packaging.Create(pth);
                    pkg.MakeSpec(
                        pkg.PutPart(
                            uri,
                            "text/plain",
                            Encoding.UTF8.GetBytes("some content")));

                    pkg.MakeSpec(
                        pkg.PutPart(
                            anotherUri,
                            "text/plain",
                            Encoding.UTF8.GetBytes("another content")));

                    pkg.Flush();
                }

                {
                    using var pkgOrErr = packaging.OpenReadWrite(pth);
                    var pkg = pkgOrErr.Must();

                    var part = pkg.MustPart(uri);

                    pkg.RemovePart(pkg.UnmakeSpec(part));

                    Assert.IsNull(pkg.FindPart(uri));
                    ((System.IDisposable)pkgOrErr).Dispose();
                }
            }

            [Test]
            public void Test_deleting_a_supplementary()
            {
                using TemporaryDirectory tmpdir = new TemporaryDirectory();
                string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

                Packaging packaging = new Packaging();

                var uri = new Uri("/aasx/some-company/suppl.txt", UriKind.Relative);
                var uriSpec = new Uri("/aasx/some-company/data.txt", UriKind.Relative);

                // Add another supplementary to make sure that only *one* supplementary
                // is deleted
                var anotherUri = new Uri(
                    "/aasx/some-company/suppl1.txt", UriKind.Relative);

                {
                    using var pkg = packaging.Create(pth);

                    var spec = pkg.MakeSpec(
                        pkg.PutPart(
                            uriSpec,
                            "text/plain",
                            Encoding.UTF8.GetBytes("some spec content")));

                    pkg.RelateSupplementaryToSpec(
                        pkg.PutPart(
                            uri,
                            "text/plain",
                            Encoding.UTF8.GetBytes("some content")),
                        spec);

                    pkg.RelateSupplementaryToSpec(
                        pkg.PutPart(
                            anotherUri,
                            "text/plain",
                            Encoding.UTF8.GetBytes("another content")),
                        spec);


                    pkg.Flush();
                }

                {
                    using var pkgOrErr = packaging.OpenReadWrite(pth);
                    var pkg = pkgOrErr.Must();

                    var suppl = pkg.MustPart(uri);
                    var spec = pkg.MustPart(uriSpec);

                    pkg.UnrelateSupplementaryFromSpec(suppl, spec);
                    pkg.RemovePart(suppl);

                    Assert.IsNull(pkg.FindPart(uri));
                }
            }

            [Test]
            public void Test_deleting_a_thumbnail()
            {
                using TemporaryDirectory tmpdir = new TemporaryDirectory();
                string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

                Packaging packaging = new Packaging();

                var uri = new Uri("/aasx/some-company/thumb.txt", UriKind.Relative);

                {
                    using var pkg = packaging.Create(pth);
                    pkg.MakeThumbnail(
                        pkg.PutPart(
                            uri,
                            "text/plain",
                            Encoding.UTF8.GetBytes("some content")));
                    pkg.Flush();
                }

                {
                    using var pkgOrErr = packaging.OpenReadWrite(pth);
                    var pkg = pkgOrErr.Must();

                    var thumbnail = pkg.Thumbnail();
                    if (thumbnail == null)
                    {
                        throw new AssertionException(
                            "Unexpected null thumbnail");
                    }

                    pkg.UnmakeThumbnail();
                    pkg.RemovePart(thumbnail);

                    Assert.IsNull(pkg.FindPart(uri));
                }
            }
        }
    }
}