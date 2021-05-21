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
    public class TestPackageReadWrite
    {
        [Test]
        public void Test_adding_spec_to_new_package()
        {
            var originalContent = Encoding.UTF8.GetBytes("some content");

            using TemporaryDirectory tmpdir = new();
            string pth = Path.Join(tmpdir.Path, "dummy.aasx");

            Packaging packaging = new();

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

            using TemporaryDirectory tmpdir = new();
            string pth = Path.Join(tmpdir.Path, "dummy.aasx");

            Packaging packaging = new();

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

            using TemporaryDirectory tmpdir = new();
            string pth = Path.Join(tmpdir.Path, "dummy.aasx");

            Packaging packaging = new();

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
            Packaging packaging = new();

            var originalContent = Encoding.UTF8.GetBytes("some content");

            MemoryStream stream = new();
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
        public void Test_modify_package_in_a_stream()
        {
            Packaging packaging = new();

            var originalContent = Encoding.UTF8.GetBytes("some content");
            var uri = new Uri("/some-thumbnail.txt", UriKind.Relative);

            MemoryStream stream = new();

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
        public void Test_modify_thumbnail_and_delete_existing()
        {
            using TemporaryDirectory tmpdir = new();
            string pth = Path.Join(tmpdir.Path, "dummy.aasx");

            Packaging packaging = new();

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
            using TemporaryDirectory tmpdir = new();
            string pth = Path.Join(tmpdir.Path, "dummy.aasx");

            Packaging packaging = new();

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
        public void Test_deleting_a_spec()
        {
            using TemporaryDirectory tmpdir = new();
            string pth = Path.Join(tmpdir.Path, "dummy.aasx");

            Packaging packaging = new();

            var uri = new Uri("/aasx/some-company/data.txt", UriKind.Relative);

            {
                using var pkg = packaging.Create(pth);
                pkg.PutSpec(
                    uri,
                    "text/plain",
                    Encoding.UTF8.GetBytes("some content"));
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
        public void Test_deleting_a_supplementary()
        {
            using TemporaryDirectory tmpdir = new();
            string pth = Path.Join(tmpdir.Path, "dummy.aasx");

            Packaging packaging = new();

            var uri = new Uri("/aasx/some-company/suppl.txt", UriKind.Relative);

            {
                using var pkg = packaging.Create(pth);
                pkg.PutSupplementary(
                    uri,
                    "text/plain",
                    Encoding.UTF8.GetBytes("some content"));
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
            using TemporaryDirectory tmpdir = new();
            string pth = Path.Join(tmpdir.Path, "dummy.aasx");

            Packaging packaging = new();

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

        [Test]
        public void Test_that_opening_a_non_package_file_returns_the_exception()
        {
            using TemporaryDirectory tmpdir = new();
            string pth = Path.Join(tmpdir.Path, "dummy.aasx");
            {
                // Create an invalid file w.r.t. Open Package Convention
                File.WriteAllText(pth, "This is not OPC.");
            }

            Packaging packaging = new();

            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);

                Assert.IsNotNull(pkgOrErr.MaybeException);
                Assert.IsInstanceOf<FileFormatException>(pkgOrErr.MaybeException);
            }
        }

        [Test]
        public void Test_that_opening_a_file_without_origin_returns_the_exception()
        {
            using TemporaryDirectory tmpdir = new();
            string pth = Path.Join(tmpdir.Path, "dummy.aasx");
            {
                // Create an empty Open Package Convention package, *i.e.* an invalid
                // AAS package
                using var pkgOpc = SystemPackage.Open(
                    pth, FileMode.Create, FileAccess.Write);
            }

            Packaging packaging = new();

            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);

                Assert.IsNotNull(pkgOrErr.MaybeException);
                Assert.IsInstanceOf<InvalidDataException>(pkgOrErr.MaybeException);
            }
        }

        [Test]
        public void Test_that_opening_a_stream_without_origin_returns_the_exception()
        {
            MemoryStream stream = new();
            {
                // Create an empty Open Package Convention package, *i.e.* an invalid
                // AAS package
                using var pkgOpc = SystemPackage.Open(
                    stream, FileMode.Create, FileAccess.Write);
            }

            Packaging packaging = new();

            {
                using var pkgOrErr = packaging.OpenReadWrite(stream);

                Assert.IsNotNull(pkgOrErr.MaybeException);
                Assert.IsInstanceOf<InvalidDataException>(pkgOrErr.MaybeException);
            }
        }

        [Test]
        public void Test_that_opening_an_empty_stream_returns_the_exception()
        {
            Packaging packaging = new();

            using MemoryStream stream = new();

            using var pkgOrErr = packaging.OpenReadWrite(stream);

            Assert.IsNotNull(pkgOrErr.MaybeException);
            Assert.IsInstanceOf<FileFormatException>(pkgOrErr.MaybeException);
        }
    }
}