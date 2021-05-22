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
using SystemPackage = System.IO.Packaging.Package;  // renamed
using Uri = System.Uri;
using UriKind = System.UriKind;

using System.Linq;  // can't alias
using NUnit.Framework;  // can't alias

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
                pkg.PutSpec(
                    new Uri("/aasx/some-company/data.txt", UriKind.Relative),
                    "text/plain",
                    originalContent);
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
            var originalContent = Encoding.UTF8.GetBytes("some content");

            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            Packaging packaging = new Packaging();

            {
                using var pkg = packaging.Create(pth);
                pkg.PutSupplementary(
                    new Uri("/aasx/some-company/suppl.txt", UriKind.Relative),
                    "text/plain",
                    originalContent);
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenRead(pth);
                var pkg = pkgOrErr.Must();
                var gotContent = pkg.Supplementaries().First().ReadAllBytes();
                Assert.That(gotContent, Is.EqualTo(originalContent));
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
                pkg.PutThumbnail(
                    new Uri("/some-thumbnail.txt", UriKind.Relative),
                    "text/plain",
                    originalContent,
                    true);
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
                pkg.PutThumbnail(
                    new Uri("/some-thumbnail.txt", UriKind.Relative),
                    "text/plain",
                    originalContent,
                    true);
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
                pkg.PutThumbnail(
                    uri,
                    "text/plain",
                    originalContent,
                    true);
                pkg.Flush();
            }

            var newContent = Encoding.UTF8.GetBytes("another content");

            // Modify
            {
                using var pkgOrErr = packaging.OpenReadWrite(stream);
                var pkg = pkgOrErr.Must();
                pkg.PutThumbnail(
                    uri,
                    "text/plain",
                    newContent,
                    true);
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

            Packaging packaging = new Packaging();

            {
                using var pkg = packaging.Create(pth);
                pkg.PutSpec(
                    new Uri("/aasx/some-company/data.txt", UriKind.Relative),
                    "text/plain",
                    originalContent);
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();
                pkg.PutSpec(
                    new Uri("/aasx/some-company/data.txt", UriKind.Relative),
                    "text/plain",
                    newContent);
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
                pkg.PutSupplementary(uri, "text/plain", originalContent);
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();
                pkg.PutSupplementary(uri, "text/plain", newContent);
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
        public void Test_the_exception_when_overwriting_a_spec_as_non_spec()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            Packaging packaging = new Packaging();

            var uri = new Uri("/aasx/suppl/data.txt", UriKind.Relative);

            {
                using var pkg = packaging.Create(pth);

                // We put a supplementary here.
                pkg.PutSupplementary(
                    uri,
                    "text/plain",
                    Encoding.UTF8.GetBytes("some old content"));
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();

                // We try to overwrite a supplementary as a spec here.
                Assert.Catch<InvalidDataException>(() =>
                {
                    pkg.PutSpec(
                        uri,
                        "text/plain",
                        Encoding.UTF8.GetBytes("new content"));
                });
            }
        }

        [Test]
        public void Test_the_exception_when_overwriting_a_suppl_as_non_suppl()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            Packaging packaging = new Packaging();

            var uri = new Uri("/aasx/data.txt", UriKind.Relative);

            {
                using var pkg = packaging.Create(pth);

                // We put a spec here.
                pkg.PutSpec(
                    uri,
                    "text/plain",
                    Encoding.UTF8.GetBytes("some old content"));
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();

                // We try to overwrite a spec as a supplementary here.
                Assert.Catch<InvalidDataException>(() =>
                {
                    pkg.PutSupplementary(
                        uri,
                        "text/plain",
                        Encoding.UTF8.GetBytes("new content"));
                });
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
                pkg.PutThumbnail(
                    new Uri("/some-thumbnail.txt", UriKind.Relative),
                    "text/plain",
                    Encoding.UTF8.GetBytes("some content"),
                    true);
                pkg.Flush();
            }

            var newContent = Encoding.UTF8.GetBytes("some new content");

            // Put new content
            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();

                pkg.PutThumbnail(
                    new Uri("/another-thumbnail.txt", UriKind.Relative),
                    "text/plain",
                    newContent,
                    true);
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
                pkg.PutThumbnail(
                    originalUri,
                    "text/plain",
                    originalContent,
                    true);
                pkg.Flush();
            }

            var newContent = Encoding.UTF8.GetBytes("some new content");

            // Put new content
            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();

                pkg.PutThumbnail(
                    new Uri("/another-thumbnail.txt", UriKind.Relative),
                    "text/plain",
                    newContent,
                    false);
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

        [Test]
        public void Test_the_exception_when_putting_a_thumb_on_a_non_thumb()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            Packaging packaging = new Packaging();

            var uri = new Uri("/aasx/suppl/data.txt", UriKind.Relative);

            {
                using var pkg = packaging.Create(pth);

                // We put a supplementary here.
                pkg.PutSupplementary(
                    uri,
                    "text/plain",
                    Encoding.UTF8.GetBytes("some old content"));
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();

                // We try to overwrite a supplementary as a thumbnail here.
                Assert.Catch<InvalidDataException>(() =>
                {
                    pkg.PutThumbnail(
                        uri,
                        "text/plain",
                        Encoding.UTF8.GetBytes("new content"),
                        true);
                });
            }
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
                pkg.PutSpec(
                    uri,
                    "text/plain",
                    Encoding.UTF8.GetBytes("some content"));

                pkg.PutSpec(
                    anotherUri,
                    "text/plain",
                    Encoding.UTF8.GetBytes("another content"));

                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();
                pkg.RemoveSpec(uri);
                Assert.IsNull(pkg.FindPart(uri));
                ((System.IDisposable)pkgOrErr).Dispose();
            }
        }


        [Test]
        public void Test_the_exception_when_deleting_a_non_spec_as_spec()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            Packaging packaging = new Packaging();

            var uri = new Uri("/this-is-not-a/spec.txt", UriKind.Relative);

            {
                using var pkg = packaging.Create(pth);

                // We put a supplementary here instead of spec.
                pkg.PutSupplementary(
                    uri,
                    "text/plain",
                    Encoding.UTF8.GetBytes("some content"));
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();

                Assert.Catch<InvalidDataException>(() =>
                {
                    pkg.RemoveSpec(uri);
                });
            }
        }

        [Test]
        public void Test_the_exception_when_deleting_a_non_suppl_as_suppl()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            Packaging packaging = new Packaging();

            var uri = new Uri(
                "/this-is-not-a/supplementary.txt", UriKind.Relative);

            {
                using var pkg = packaging.Create(pth);

                // We put a spec here instead of a supplementary.
                pkg.PutSpec(
                    uri,
                    "text/plain",
                    Encoding.UTF8.GetBytes("some content"));
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();

                Assert.Catch<InvalidDataException>(() =>
                {
                    pkg.RemoveSupplementary(uri);
                });
            }
        }

        [Test]
        public void Test_deleting_a_supplementary()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "dummy.aasx" });

            Packaging packaging = new Packaging();

            var uri = new Uri("/aasx/some-company/suppl.txt", UriKind.Relative);

            // Add another supplementary to make sure that only *one* supplementary
            // is deleted
            var anotherUri = new Uri(
                "/aasx/some-company/suppl1.txt", UriKind.Relative);

            {
                using var pkg = packaging.Create(pth);
                pkg.PutSupplementary(
                    uri,
                    "text/plain",
                    Encoding.UTF8.GetBytes("some content"));

                pkg.PutSupplementary(
                    anotherUri,
                    "text/plain",
                    Encoding.UTF8.GetBytes("another content"));

                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();
                pkg.RemoveSupplementary(uri);
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
                pkg.PutThumbnail(
                    uri,
                    "text/plain",
                    Encoding.UTF8.GetBytes("some content"),
                    true);
                pkg.Flush();
            }

            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkg = pkgOrErr.Must();
                pkg.RemoveThumbnail();
                Assert.IsNull(pkg.FindPart(uri));
            }
        }
    }
}