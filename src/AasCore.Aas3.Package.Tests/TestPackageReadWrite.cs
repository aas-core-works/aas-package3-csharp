using System.Linq; // can't alias
using NUnit.Framework; // can't alias
using ArgumentException = System.ArgumentException;
using DirectoryNotFoundException = System.IO.DirectoryNotFoundException;
using Encoding = System.Text.Encoding;
using File = System.IO.File;
using FileAccess = System.IO.FileAccess;
using FileFormatException = System.IO.FileFormatException;
using FileMode = System.IO.FileMode;
using FileShare = System.IO.FileShare;
using InvalidDataException = System.IO.InvalidDataException;
using MemoryStream = System.IO.MemoryStream;
using Path = System.IO.Path;
using SystemPackage = System.IO.Packaging.Package; // renamed
using Uri = System.Uri;
using UriKind = System.UriKind;

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

    public class TestLegacy
    {
        private const string RelationTypeDeprecatedBase = "http://www.admin-shell.io/aasx/relationships";

        [Test]
        public void Test_reading_and_unmaking_legacy_spec()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "legacy.aasx" });

            var specUri = new Uri("/aasx/legacy-spec.xml", UriKind.Relative);
            var specContent = Encoding.UTF8.GetBytes("<aas>legacy</aas>");

            // Manually create a legacy package
            {
                using var pkg = SystemPackage.Open(pth, FileMode.Create);
                var originPart = pkg.CreatePart(new Uri("/aasx/aasx-origin", UriKind.Relative), "text/plain");
                pkg.CreateRelationship(
                    originPart.Uri,
                    System.IO.Packaging.TargetMode.Internal,
                    RelationTypeDeprecatedBase + "/aasx-origin"
                );

                var specPart = pkg.CreatePart(specUri, "application/xml");
                using (var s = specPart.GetStream())
                    s.Write(specContent, 0, specContent.Length);

                originPart.CreateRelationship(specUri, System.IO.Packaging.TargetMode.Internal, RelationTypeDeprecatedBase + "/aasx-spec");
                pkg.Flush();
            }

            Packaging packaging = new Packaging();

            // 1. Verify we can read it
            {
                using var pkgOrErr = packaging.OpenRead(pth);
                var pkgRead = pkgOrErr.Must();
                var spec = pkgRead.Specs().FirstOrDefault();
                Assert.IsNotNull(spec, "Legacy spec should be found");
                Assert.AreEqual(specUri, spec!.Uri);
                Assert.IsTrue(pkgRead.IsSpec(spec), "IsSpec should return true for legacy URIs");
            }

            // 2. Verify we can unmake it (delete legacy relationship)
            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkgWrite = pkgOrErr.Must();
                var spec = pkgWrite.Specs().First();

                pkgWrite.UnmakeSpec(spec);
                pkgWrite.Flush();

                Assert.IsFalse(pkgWrite.Specs().Any(), "Legacy relationship should have been deleted");
            }
        }

        [Test]
        public void Test_reading_and_unmaking_legacy_supplementary()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "legacy_suppl.aasx" });

            var specUri = new Uri("/aasx/spec.xml", UriKind.Relative);
            var supplUri = new Uri("/aasx/legacy-suppl.pdf", UriKind.Relative);

            // Manually create a legacy package with legacy supplementary relationship
            {
                using var pkg = SystemPackage.Open(pth, FileMode.Create);
                var originPart = pkg.CreatePart(new Uri("/aasx/aasx-origin", UriKind.Relative), "text/plain");
                pkg.CreateRelationship(
                    originPart.Uri,
                    System.IO.Packaging.TargetMode.Internal,
                    RelationTypeDeprecatedBase + "/aasx-origin"
                );

                var specPart = pkg.CreatePart(specUri, "application/xml");
                originPart.CreateRelationship(specUri, System.IO.Packaging.TargetMode.Internal, RelationTypeDeprecatedBase + "/aasx-spec");

                _ = pkg.CreatePart(supplUri, "application/pdf");
                specPart.CreateRelationship(supplUri, System.IO.Packaging.TargetMode.Internal, RelationTypeDeprecatedBase + "/aas-suppl");
                pkg.Flush();
            }

            Packaging packaging = new Packaging();

            // 1. Verify we can read the relationship
            {
                using var pkgOrErr = packaging.OpenRead(pth);
                var pkgRead = pkgOrErr.Must();
                var spec = pkgRead.Specs().First();
                var suppl = pkgRead.SupplementariesFor(spec).FirstOrDefault();

                Assert.IsNotNull(suppl, "Legacy supplementary should be found");
                Assert.AreEqual(supplUri, suppl!.Uri);
            }

            // 2. Verify Unrelate works for legacy URIs
            {
                using var pkgOrErr = packaging.OpenReadWrite(pth);
                var pkgReadWrite = pkgOrErr.Must();
                var spec = pkgReadWrite.Specs().First();
                var suppl = pkgReadWrite.SupplementariesFor(spec).First();

                pkgReadWrite.UnrelateSupplementaryFromSpec(suppl, spec);
                pkgReadWrite.Flush();

                Assert.IsFalse(pkgReadWrite.SupplementariesFor(spec).Any(), "Legacy supplementary relationship should be gone");
            }
        }

        [Test]
        public void Test_prevent_duplicate_relationship_if_legacy_exists()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "legacy_dup.aasx" });

            var specUri = new Uri("/aasx/spec.xml", UriKind.Relative);
            var supplUri = new Uri("/aasx/suppl.pdf", UriKind.Relative);

            // Create package with a LEGACY supplementary relationship
            using (var pkg = SystemPackage.Open(pth, FileMode.Create))
            {
                var originPart = pkg.CreatePart(new Uri("/aasx/aasx-origin", UriKind.Relative), "text/plain");
                pkg.CreateRelationship(
                    originPart.Uri,
                    System.IO.Packaging.TargetMode.Internal,
                    RelationTypeDeprecatedBase + "/aasx-origin"
                );

                var specPart = pkg.CreatePart(specUri, "application/xml");
                originPart.CreateRelationship(specUri, System.IO.Packaging.TargetMode.Internal, RelationTypeDeprecatedBase + "/aasx-spec");

                _ = pkg.CreatePart(supplUri, "application/pdf");
                specPart.CreateRelationship(supplUri, System.IO.Packaging.TargetMode.Internal, RelationTypeDeprecatedBase + "/aas-suppl");
                pkg.Flush();
            }

            Packaging packaging = new Packaging();
            using (var pkgOrErr = packaging.OpenReadWrite(pth))
            {
                var pkgrw = pkgOrErr.Must();
                var spec = pkgrw.MustPart(specUri);
                var suppl = pkgrw.MustPart(supplUri);

                // Attempt to relate them again using modern logic
                pkgrw.RelateSupplementaryToSpec(suppl, spec);
                pkgrw.Flush();
            }

            // Verify only ONE relationship exists (the legacy one)
            using (var pkg = SystemPackage.Open(pth, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var specPart = pkg.GetPart(specUri);
                var rels = specPart.GetRelationships();
                Assert.AreEqual(1, rels.Count(), "Should not create a modern relationship if a legacy one exists for the same target");
            }
        }

        [Test]
        public void Test_mixed_legacy_spec_and_modern_supplementary()
        {
            using TemporaryDirectory tmpdir = new TemporaryDirectory();
            string pth = Path.Combine(new[] { tmpdir.Path, "mixed.aasx" });

            var specUri = new Uri("/aasx/legacy-spec.xml", UriKind.Relative);

            // 1. Create with legacy Spec
            {
                using var pkg = SystemPackage.Open(pth, FileMode.Create);
                var originPart = pkg.CreatePart(new Uri("/aasx/aasx-origin", UriKind.Relative), "text/plain");
                pkg.CreateRelationship(
                    originPart.Uri,
                    System.IO.Packaging.TargetMode.Internal,
                    RelationTypeDeprecatedBase + "/aasx-origin"
                );
                pkg.CreatePart(specUri, "application/xml");
                originPart.CreateRelationship(specUri, System.IO.Packaging.TargetMode.Internal, RelationTypeDeprecatedBase + "/aasx-spec");
                pkg.Flush();
            }

            // 2. Add modern supplementary via Packaging API
            Packaging packaging = new Packaging();
            using (var pkgOrErr = packaging.OpenReadWrite(pth))
            {
                var pkgReadWrite = pkgOrErr.Must();
                var spec = pkgReadWrite.Specs().First();
                var suppl = pkgReadWrite.PutPart(new Uri("/aasx/new-suppl.txt", UriKind.Relative), "text/plain",
                    Encoding.UTF8.GetBytes("new"));
                pkgReadWrite.RelateSupplementaryToSpec(suppl, spec);
                pkgReadWrite.Flush();
            }

            // 3. Verify both are accessible
            using (var pkgOrErr = packaging.OpenRead(pth))
            {
                var pkgRead = pkgOrErr.Must();
                var spec = pkgRead.Specs().First();
                Assert.AreEqual(1, pkgRead.SupplementariesFor(spec).Count(), "Should find modern supplementary related to legacy spec");
            }
        }
    }
}
