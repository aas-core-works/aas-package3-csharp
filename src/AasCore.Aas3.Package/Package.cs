using CompressionOption = System.IO.Packaging.CompressionOption;
using Encoding = System.Text.Encoding;
using Exception = System.Exception;
using File = System.IO.File;
using FileAccess = System.IO.FileAccess;
using FileMode = System.IO.FileMode;
using FileStream = System.IO.FileStream;
using GC = System.GC;
using IDisposable = System.IDisposable;
using InvalidDataException = System.IO.InvalidDataException;
using InvalidOperationException = System.InvalidOperationException;
using MemoryStream = System.IO.MemoryStream;
using PackagePart = System.IO.Packaging.PackagePart;
using Stream = System.IO.Stream;
using StringComparison = System.StringComparison;
using SystemPackage = System.IO.Packaging.Package; // renamed
using TargetMode = System.IO.Packaging.TargetMode;
using Uri = System.Uri;
using UriKind = System.UriKind;
using System.Collections.Generic; // can't alias
using System.IO; // can't alias
using System.Linq; // can't alias

// This is necessary only for InspectCode.
// See https://resharper-support.jetbrains.com/hc/en-us/community/posts/360008362700-Not-recognising-Nullable-enable-Nullable-
#nullable enable

namespace AasCore.Aas3.Package
{
    /**
     * <summary>
     * Either a <see cref="PackageRead"/> or an <see cref="Exception"/>.
     * </summary>
     */
    public class PackageOrException<TPackage> : IDisposable where TPackage : PackageRead
    {
        private readonly TPackage? _package;
        public readonly Exception? MaybeException;
        private bool _disposed;

        /**
         * <remarks>
         * This object takes over the ownership of <paramref name="package"/>.
         * </remarks>
         */
        internal PackageOrException(
            TPackage? package,
            Exception? exception)
        {
            try
            {
                #region Preconditions

                Dbc.Require(package is null ^ exception is null,
                    "Exclusivity expected, not both or none");

                #endregion

                _package = package;
                MaybeException = exception;
            }
            catch (Exception)
            {
                if (_package != null)
                {
                    ((IDisposable)_package).Dispose();
                }

                throw;
            }
        }

        /**
         * <summary>Return the package or throw the associated exception.</summary>
         */
        public TPackage Must()
        {
            if (MaybeException != null)
            {
                throw MaybeException;
            }

            if (_package == null)
            {
                throw new InvalidOperationException("Unexpected null _package");
            }

            return _package;
        }

        void IDisposable.Dispose()
        {
            if (!_disposed)
            {
                if (_package != null)
                {
                    ((IDisposable)_package).Dispose();
                }

                GC.SuppressFinalize(this);
            }

            _disposed = true;
        }
    }

    /**
     * <summary>Represent a part of an AAS package.</summary>
     */
    public class Part
    {
        public Uri Uri { get; }
        internal readonly PackagePart PackagePart;

        public Part(Uri uri, PackagePart packagePart)
        {
            Uri = uri;
            PackagePart = packagePart;
        }

        /**
         * MIME type
         */
        public string ContentType => PackagePart.ContentType;

        /**
         * <returns>
         * Open a read stream.
         *
         * The caller is responsible for disposing it.
         * </returns>
         */
        // ReSharper disable once MemberCanBePrivate.Global
        public Stream Stream()
        {
            return PackagePart.GetStream(FileMode.Open, FileAccess.Read);
        }

        /**
         * <summary>Read the whole content of the part as bytes.</summary>
         */
        public byte[] ReadAllBytes()
        {
            using var stream = Stream();
            MemoryStream ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        /**
         * <summary>Read the content of the part as UTF-8 text.</summary>
         */
        // ReSharper disable once UnusedMember.Global
        public string ReadAllText()
        {
            return ReadAllText(Encoding.UTF8);
        }

        /**
         * <summary>
         * Read the content of the part as text encoded according
         * to <paramref name="encoding"/>.
         * </summary>
         */
        // ReSharper disable once MemberCanBePrivate.Global
        public string ReadAllText(Encoding encoding)
        {
            using var sr = new StreamReader(
                Stream(), encoding, true);
            return sr.ReadToEnd();
        }
    }

    /**
     * <summary>Open and create packages.</summary>
     */
    public class Packaging
    {
        internal static class RelationType
        {
            internal const string AasxOrigin =
                "http://admin-shell.io/aasx/relationships/aasx-origin";

            internal const string AasxSpec =
                "http://admin-shell.io/aasx/relationships/aas-spec";

            internal const string AasxSupplementary =
                "http://admin-shell.io/aasx/relationships/aas-suppl";

            internal const string Thumbnail =
                "http://schemas.openxmlformats.org/package/2006/relationships/metadata/thumbnail";
        }

        /**
         * <summary>Create the AAS package at the given <paramref name="path"/>.</summary>
         */
        public PackageReadWrite Create(string path)
        {
            // These objects are to be disposed if the create operation fails.
            // Otherwise, their ownership should be transferred to the resulting object.
            // This list also includes the opened package.
            //
            // Capacity of 1 is meant for the opened package.
            List<IDisposable> toBeDisposed = new List<IDisposable>(1);

            try
            {
                var pkg = SystemPackage.Open(
                    path, FileMode.Create, FileAccess.ReadWrite);
                toBeDisposed.Add(pkg);

                return CreatePackage(path, pkg, toBeDisposed);
            }
            catch (Exception)
            {
                for (var i = toBeDisposed.Count - 1; i >= 0; i--)
                {
                    toBeDisposed[i].Dispose();
                }

                throw;
            }
        }

        /**
         * <summary>
         * Create the AAS package in the given <paramref name="stream"/>.
         * </summary>
         */
        public PackageReadWrite Create(Stream stream)
        {
            // These objects are to be disposed if the create operation fails.
            // Otherwise, their ownership should be transferred to the resulting object.
            // This list also includes the opened package.
            //
            // Capacity of 1 is meant for the opened package.
            List<IDisposable> toBeDisposed = new List<IDisposable>(1);

            try
            {
                var pkg = SystemPackage.Open(
                    stream, FileMode.Create, FileAccess.ReadWrite);
                toBeDisposed.Add(pkg);

                return CreatePackage(null, pkg, toBeDisposed);
            }
            catch (Exception)
            {
                for (var i = toBeDisposed.Count - 1; i >= 0; i--)
                {
                    toBeDisposed[i].Dispose();
                }

                throw;
            }
        }

        private static PackagePart? FindOriginPart(SystemPackage package)
        {
            var xs = package.GetRelationshipsByType(
                RelationType.AasxOrigin);

            return (
                from x in xs
                where x.SourceUri.ToString() == "/"
                select package.GetPart(x.TargetUri)
            ).FirstOrDefault();
        }

        /*
         * NOTE (mristin, 2021-05-07):
         * Make sure that you sync the gist of the `Open* methods.` I tried to encapsulate
         * the bits of the code in separate functions, but that resulted in very
         * unreadable and hard-to-follow code:
         *
         * * The resources need to be disposed correctly. C# lacks the mechanism for
         *   transferring the ownership easily.
         *
         * * C# does not support `new` constraints on generics with parameters.
         *   Thus we need to duplicate the implementation since we side with
         *   the type safety of the caller code rather than with the maintainer of
         *   the library.
         */

        /**
         * <summary>Open the package at <paramref name="path"/> for reading.</summary>
         * <remarks>
         * The exceptions are captured in the resulting type disjunction so that you
         * can use it for verifying a large number of files in a performant way.
         * </remarks>
         */
        public PackageOrException<PackageRead> OpenRead(string path)
        {
            // These objects are to be disposed if the open operation fails.
            // Otherwise, their ownership should be transferred to the resulting object.
            // This list also includes the opened package.
            //
            // Capacity of 2 is meant for: 1) the stream and 2) the package itself
            List<IDisposable> toBeDisposed = new List<IDisposable>(2);

            PackageOrException<PackageRead>? result;

            try
            {
                // (mristin, 2021-05-07):
                // We have to open a stream ourselves. The official NET 5 implementation
                // of Open Package Convention leaks file descriptors if the file format
                // is invalid.
                Stream stream = File.OpenRead(path);
                toBeDisposed.Add(stream);

                var pkg = SystemPackage.Open(stream, FileMode.Open, FileAccess.Read);
                toBeDisposed.Add(pkg);

                var originPart = FindOriginPart(pkg);
                if (originPart is null)
                {
                    result = new PackageOrException<PackageRead>(
                        null,
                        new InvalidDataException("No origin part found"));
                }
                else
                {
                    result = new PackageOrException<PackageRead>(
                        new PackageRead(path, originPart, toBeDisposed),
                        null);
                }
            }
            catch (Exception exception)
            {
                result = new PackageOrException<PackageRead>(null, exception);
            }

            if (result == null)
            {
                throw new InvalidOperationException("Unexpected null result");
            }


            // Dispose the toBeDisposed pre-emptively here so that they do not have to
            // linger unnecessarily in the resulting object.
            if (result.MaybeException != null)
            {
                for (var i = toBeDisposed.Count - 1; i >= 0; i--)
                {
                    toBeDisposed[i].Dispose();
                }
            }

            #region Postconditions

#if DEBUG || DEBUGSLOW

            Dbc.Ensure(
                result.MaybeException != null || result.Must().Path == path,
                "The Path property of the package must match the input path.");

#endif

            #endregion

            return result;
        }

        /**
         * <summary>Open the package in <paramref name="stream"/> for reading.</summary>
         * <remarks>
         * The exceptions are captured in the resulting type disjunction so that you
         * can use it for verifying a large number of files in a performant way.
         * </remarks>
         */
        public PackageOrException<PackageRead> OpenRead(Stream stream)
        {
            // These objects are to be disposed if the open operation fails.
            // Otherwise, their ownership should be transferred to the resulting object.
            //
            // Capacity of 1 is meant for only the package itself, we are not responsible
            // for closing the stream.
            List<IDisposable> toBeDisposed = new List<IDisposable>(1);

            PackageOrException<PackageRead>? result;

            try
            {
                var pkg = SystemPackage.Open(stream, FileMode.Open, FileAccess.Read);
                toBeDisposed.Add(pkg);

                var originPart = FindOriginPart(pkg);
                if (originPart is null)
                {
                    result = new PackageOrException<PackageRead>(
                        null,
                        new InvalidDataException("No origin part found"));
                }
                else
                {
                    result = new PackageOrException<PackageRead>(
                        new PackageRead(null, originPart, toBeDisposed),
                        null);
                }
            }
            catch (Exception exception)
            {
                result = new PackageOrException<PackageRead>(null, exception);
            }

            if (result == null)
            {
                throw new InvalidOperationException("Unexpected null result");
            }

            // Dispose the toBeDisposed pre-emptively here so that they do not have to
            // linger unnecessarily in the resulting object.
            if (result.MaybeException != null)
            {
                for (var i = toBeDisposed.Count - 1; i >= 0; i--)
                {
                    toBeDisposed[i].Dispose();
                }
            }

            #region Postconditions

#if DEBUG || DEBUGSLOW

            Dbc.Ensure(
                result.MaybeException != null || result.Must().Path == null,
                "The Path property of the package must be null " +
                "if reading from a stream.");

#endif

            #endregion

            return result;
        }

        /**
         * <summary>Open the package at <paramref name="path"/> for read/write.</summary>
         * <remarks>
         * The exceptions are captured in the resulting type disjunction so that you
         * can use it for verifying a large number of files in a performant way.
         * </remarks>
         */
        public PackageOrException<PackageReadWrite> OpenReadWrite(string path)
        {
            // These objects are to be disposed if the open operation fails.
            // Otherwise, their ownership should be transferred to the resulting object.
            // This list also includes the opened package.
            //
            // Capacity of 2 is meant for: 1) the stream and 2) the package itself
            List<IDisposable> toBeDisposed = new List<IDisposable>(2);

            PackageOrException<PackageReadWrite>? result;

            try
            {
                // (mristin, 2021-05-07):
                // We have to open a stream ourselves. The official NET 5 implementation
                // of Open Package Convention leaks file descriptors if the file format
                // is invalid.
                Stream stream =
                    new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
                toBeDisposed.Add(stream);

                var pkg =
                    SystemPackage.Open(stream, FileMode.Open, FileAccess.ReadWrite);
                toBeDisposed.Add(pkg);

                var originPart = FindOriginPart(pkg);
                if (originPart is null)
                {
                    result = new PackageOrException<PackageReadWrite>(
                        null,
                        new InvalidDataException("No origin part found"));
                }
                else
                {
                    result = new PackageOrException<PackageReadWrite>(
                        new PackageReadWrite(path, originPart, toBeDisposed),
                        null);
                }
            }
            catch (Exception exception)
            {
                result = new PackageOrException<PackageReadWrite>(null, exception);
            }

            if (result == null)
            {
                throw new InvalidOperationException("Unexpected null result");
            }

            // Dispose the toBeDisposed pre-emptively here so that they do not have to
            // linger unnecessarily in the resulting object.
            if (result.MaybeException != null)
            {
                for (var i = toBeDisposed.Count - 1; i >= 0; i--)
                {
                    toBeDisposed[i].Dispose();
                }
            }

            #region Postconditions

#if DEBUG || DEBUGSLOW

            Dbc.Ensure(
                result.MaybeException != null || result.Must().Path == path,
                "The Path property of the package must match the input path."
            );

#endif

            #endregion

            return result;
        }

        /**
         * <summary>
         * Open the package in <paramref name="stream"/> for read/write.
         * </summary>
         * <remarks>
         * The exceptions are captured in the resulting type disjunction so that you
         * can use it for verifying a large number of files in a performant way.
         * </remarks>
         */
        public PackageOrException<PackageReadWrite> OpenReadWrite(Stream stream)
        {
            // These objects are to be disposed if the open operation fails.
            // Otherwise, their ownership should be transferred to the resulting object.
            //
            // Capacity of 1 is meant for only the package itself, we are not responsible
            // for closing the stream.
            List<IDisposable> toBeDisposed = new List<IDisposable>(1);

            PackageOrException<PackageReadWrite>? result;

            try
            {
                var pkg = SystemPackage.Open(
                    stream, FileMode.Open, FileAccess.ReadWrite);
                toBeDisposed.Add(pkg);

                var originPart = FindOriginPart(pkg);
                if (originPart is null)
                {
                    result = new PackageOrException<PackageReadWrite>(
                        null,
                        new InvalidDataException("No origin part found"));
                }
                else
                {
                    result = new PackageOrException<PackageReadWrite>(
                        new PackageReadWrite(null, originPart, toBeDisposed),
                        null);
                }
            }
            catch (Exception exception)
            {
                result = new PackageOrException<PackageReadWrite>(null, exception);
            }

            if (result == null)
            {
                throw new InvalidOperationException("Unexpected null result");
            }

            // Dispose the toBeDisposed pre-emptively here so that they do not have to
            // linger unnecessarily in the resulting object.
            if (result.MaybeException != null)
            {
                for (var i = toBeDisposed.Count - 1; i >= 0; i--)
                {
                    toBeDisposed[i].Dispose();
                }
            }

            #region Postconditions

#if DEBUG || DEBUGSLOW

            Dbc.Ensure(result.MaybeException != null || result.Must().Path == null,
                "The Path property of the package must be null " +
                "if read/writing to a stream.");

#endif

            #endregion

            return result;
        }

        /**
         * <summary>Create an empty AAS structure in a new package.</summary>
         * <remarks>The caller needs to dispose the result.</remarks>
         * <remarks>
         * The ownership of the <paramref name="toBeDisposed"/> is transferred to
         * the result.
         * </remarks>
         */
        private static PackageReadWrite CreatePackage(
            string? path, SystemPackage package, List<IDisposable> toBeDisposed)
        {
            var originPart = package.CreatePart(
                new Uri("/aasx/aasx-origin", UriKind.Relative),
                System.Net.Mime.MediaTypeNames.Text.Plain,
                CompressionOption.Maximum);

            using var stream = originPart.GetStream(FileMode.Create);
            var bytes = Encoding.ASCII.GetBytes(
                "Intentionally empty.");
            stream.Write(bytes, 0, bytes.Length);

            package.CreateRelationship(
                originPart.Uri, TargetMode.Internal, RelationType.AasxOrigin);

            var result = new PackageReadWrite(path, originPart, toBeDisposed);

            #region Postconditions

#if DEBUG || DEBUGSLOW

            Dbc.Ensure(
                !result.Specs().Any(),
                "Specs must be empty in a new package.");

            Dbc.Ensure(
                !result.SupplementaryRelationships().Any(),
                "There must be no supplementary relationships " +
                "in a new package.");

            Dbc.Ensure(
                result.Thumbnail() == null,
                "There must be no thumbnail in a new package.");

#endif

            #endregion

            return result;
        }
    }

    /**
     * <summary>Read from the AAS package.</summary>
     */
    public class PackageRead : IDisposable
    {
        protected readonly SystemPackage UnderlyingPackage;
        protected readonly PackagePart OriginPart;
        private readonly List<IDisposable> _toBeDisposed;
        private bool _disposed;

        /**
         * Path associated with the package.
         * Null if no path available (<i>e.g.,</i> if the package comes from a stream).
         */
        public string? Path { get; }

        /**
         * <remarks>
         * This object takes over the ownership of <paramref name="toBeDisposed"/>.
         * The <paramref name="toBeDisposed"/> might possibly include the underlying
         * package as well, but this needs to be specified by the caller.
         * </remarks>
         */
        internal PackageRead(
            string? path,
            PackagePart originPart,
            List<IDisposable> toBeDisposed)
        {
            Path = path;
            UnderlyingPackage = originPart.Package;
            OriginPart = originPart;
            _toBeDisposed = toBeDisposed;
        }

        void IDisposable.Dispose()
        {
            if (!_disposed)
            {
                for (var i = _toBeDisposed.Count - 1; i >= 0; i--)
                {
                    _toBeDisposed[i].Dispose();
                }

                GC.SuppressFinalize(this);
            }

            _disposed = true;
        }


        /**
         * <summary>List AAS specs contained in the package.</summary>
         */
        public IEnumerable<Part> Specs()
        {
            var xs = OriginPart.GetRelationshipsByType(
                Packaging.RelationType.AasxSpec);

            foreach (var x in xs)
            {
                var specUri = x.TargetUri;
                var specPart = UnderlyingPackage.GetPart(specUri);
                yield return new Part(specUri, specPart);
            }
        }

        /**
         * <summary>List AAS specs grouped by their MIME content type.</summary>
         */
        public IDictionary<string, List<Part>> SpecsByContentType()
        {
            var result = new SortedDictionary<string, List<Part>>();
            foreach (var spec in Specs())
            {
                if (!result.ContainsKey(spec.ContentType))
                {
                    result.Add(spec.ContentType, new List<Part>());
                }

                result[spec.ContentType].Add(spec);
            }

            foreach (string contentType in result.Keys)
            {
                // Sort the list of specs by URI to make the code reproducible
                result[contentType].Sort((spec1, spec2) => string.Compare(
                    spec1.Uri.ToString(),
                    spec2.Uri.ToString(),
                    StringComparison.InvariantCulture));
            }

            #region Postconditions

#if DEBUG || DEBUGSLOW

            Dbc.Ensure(
                result.All(
                    item => item.Value.All(
                        spec => spec.ContentType == item.Key)),
                "The content type of spec must match its group."
            );

            Dbc.Ensure(
                result.All(item => item.Value.Count > 0),
                "Every entry in the result must contain non-empty specs."
            );

#endif

            #endregion

            return result;
        }

        /**
         * <summary>
         * Check that the <paramref name="part"/> is related to the origin
         * of the package as spec.
         * </summary>
         */
        // ReSharper disable once MemberCanBeProtected.Global
        public bool IsSpec(Part part)
        {
            return OriginPart
                .GetRelationshipsByType(Packaging.RelationType.AasxSpec)
                .Any(rel => rel.TargetUri == part.Uri);
        }

        /**
         * <summary>List supplementary files contained in the package.</summary>
         */
        // ReSharper disable once MemberCanBeProtected.Global
        public IEnumerable<Part> SupplementariesFor(Part spec)
        {
            var xs = spec.PackagePart.GetRelationshipsByType(
                Packaging.RelationType.AasxSupplementary);

            foreach (var x in xs)
            {
                var supplUri = x.TargetUri;

                if (!UnderlyingPackage.PartExists(supplUri))
                {
                    throw new InvalidDataException(
                        $"The relationship from spec {spec.Uri} " +
                        $"as supplementary material to {supplUri} exists, " +
                        "but the underlying part does not.");
                }

                var supplPart = UnderlyingPackage.GetPart(supplUri);
                yield return new Part(supplUri, supplPart);
            }
        }

        public class SupplementaryRelationship
        {
            // ReSharper disable once MemberCanBePrivate.Global
            // ReSharper disable once NotAccessedField.Global
            public readonly Part Spec;
            public readonly Part Supplementary;

            public SupplementaryRelationship(Part spec, Part supplementary)
            {
                Spec = spec;
                Supplementary = supplementary;
            }
        }

        /**
         * <summary>
         * Iterate over all the supplementary relationships from all the specs.
         * </summary>
         */
        public IEnumerable<SupplementaryRelationship> SupplementaryRelationships()
        {
            foreach (var spec in Specs())
            {
                foreach (var suppl in SupplementariesFor(spec))
                {
                    yield return new SupplementaryRelationship(spec, suppl);
                }
            }
        }

        /**
         * <summary>
         * Try to find the package part with the given <paramref name="uri"/>.
         * </summary>
         * <returns>Part of the AAS package or null, if it does not exist.</returns>
         */
        public Part? FindPart(Uri uri)
        {
            if (!UnderlyingPackage.PartExists(uri))
            {
                return null;
            }

            return new Part(uri, UnderlyingPackage.GetPart(uri));
        }

        /**
         * <summary>
         * Retrieve the package part with the given <paramref name="uri"/>.
         * </summary>
         * <exception cref="InvalidOperationException">
         * if the part does not exist in the AAS package
         * </exception>
         */
        public Part MustPart(Uri uri)
        {
            return new Part(uri, UnderlyingPackage.GetPart(uri));
        }

        /**
         * <summary>Retrieve the thumbnail from the AAS package.</summary>
         * <returns>The thumbnail, or null if no thumbnail in the package.</returns>
         * <exception cref="InvalidDataException">
         * If the relationship exists, but the part does not.
         * </exception>
         */
        public Part? Thumbnail()
        {
            Part? result = null;

            var xs = UnderlyingPackage.GetRelationshipsByType(
                Packaging.RelationType.Thumbnail);

            foreach (var x in xs)
            {
                if (x.SourceUri.ToString() == "/")
                {
                    if (!UnderlyingPackage.PartExists(x.TargetUri))
                    {
                        throw new InvalidDataException(
                            "The thumbnail relation from the origin exists, " +
                            $"but the target part does not: {x.TargetUri}");
                    }
                    result = new Part(
                        x.TargetUri, UnderlyingPackage.GetPart(x.TargetUri));
                    break;
                }
            }

            return result;
        }
    }

    /**
     * <summary>Read and write from/to the AAS package.</summary>
     */
    public class PackageReadWrite : PackageRead
    {
        /**
         * <remarks>
         * The ownership of the <paramref name="toBeDisposed"/> is transferred
         * to this object.
         * </remarks>
         */
        internal PackageReadWrite(
            string? path, PackagePart originPart, List<IDisposable> toBeDisposed)
            : base(path, originPart, toBeDisposed)
        {
            // Intentionally left empty.
        }

        /**
         * <summary>
         * Write the <paramref name="content"/> to the package as a package part
         * with <paramref name="contentType"/>.
         * </summary>
         * <remarks>
         * This function needs to be used to put <i>any content</i> into the package.
         *
         * You have to introduce the relations by calling, <i>e.g.</i>,
         * <see cref="RelateSupplementaryToSpec"/>.
         *
         * The caller needs to be careful not to unintentionally overwrite
         * existing parts which are already related to each other (<i>e.g.</i>,
         * do not overwrite a part as a supplementary material which has been made
         * a spec).
         * </remarks>
         */
        public Part PutPart(Uri uri, string contentType, byte[] content)
        {
            using MemoryStream ms = new MemoryStream(content, false);
            var result = PutPart(uri, contentType, ms);

            #region Postconditions

#if DEBUG || DEBUGSLOW

            var maybePart = FindPart(uri);

            Dbc.Ensure(maybePart != null,
                "The part should be included in the package.");
#endif

#if DEBUGSLOW
            Dbc.Ensure(
                maybePart == null
                || maybePart.ReadAllBytes().SequenceEqual(content),
                "Input content and re-read content must coincide on put.");
#endif

            #endregion

            return result;
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public Part PutPart(Uri uri, string contentType, Stream stream)
        {
            Part? result;

            if (UnderlyingPackage.PartExists(uri))
            {
                var part = UnderlyingPackage.GetPart(uri);
                using var target = part.GetStream();
                target.SetLength(0);
                stream.CopyTo(target);

                result = new Part(uri, part);
            }
            else
            {
                var part = UnderlyingPackage.CreatePart(uri, contentType);
                using var target = part.GetStream();
                stream.CopyTo(target);

                result = new Part(uri, part);
            }

            #region Postconditions

#if DEBUG || DEBUGSLOW

            Dbc.Ensure(FindPart(uri) != null,
                "The part should be included in the package.");
#endif

            #endregion

            return result;
        }

        /**
         * <summary>Remove the part from the package.</summary>
         * <remarks>
         * This function will not check whether you removed the relations corresponding
         * to this part. Please use <see cref="UnrelateSupplementaryFromSpec"/> to
         * that end.
         * </remarks>
         */
        public void RemovePart(Part part)
        {
            if (UnderlyingPackage.PartExists(part.Uri))
            {
                UnderlyingPackage.DeletePart(part.Uri);
            }

#if DEBUG || DEBUGSLOW

            Dbc.Ensure(FindPart(part.Uri) == null,
                "The part should not exist in the package anymore.");
#endif
        }

        /**
         * <summary>
         * Relate the <paramref name="part"/> to the package origin as a spec.
         * </summary>
         * <returns>
         * The same unchanged <paramref name="part"/>.
         *
         * This is practical if you want to chain function calls.
         * </returns>
         */
        public Part MakeSpec(Part part)
        {
            OriginPart.CreateRelationship(
                part.Uri, TargetMode.Internal, Packaging.RelationType.AasxSpec);

            #region Postconditions

#if DEBUGSLOW

            Dbc.Ensure(
                Specs().FirstOrDefault(aSpec => aSpec.Uri == part.Uri) != null,
                "Spec must be listed.");

            Dbc.Ensure(
                IsSpec(part),
                "The part fulfills the spec property.");

#endif

            #endregion

            return part;
        }

        /**
         * <summary>
         * Remove the relationship from the origin to the <paramref name="part"/>
         * as a spec.
         * </summary>
         * <remarks>The caller needs to delete the part herself by calling
         * <see cref="RemovePart"/>.</remarks>
         * <returns>
         * The same unchanged <paramref name="part"/>.
         *
         * This is practical if you want to chain function calls.
         * </returns>
         */
        public Part UnmakeSpec(Part part)
        {
            #region Preconditions

#if DEBUGSLOW

            Dbc.Require(
                IsSpec(part),
                "The part fulfills the spec property.");

#endif

            #endregion

            #region Snapshots

#if DEBUGSLOW

            var oldSpecUriSet = new HashSet<Uri>(
                Specs().Select(spec => spec.Uri));

#endif

            #endregion

            var morituri = new List<string>();

            var rels = OriginPart
                .GetRelationshipsByType(Packaging.RelationType.AasxSpec)
                .Where(aRel => aRel.TargetUri == part.Uri);

            foreach (var rel in rels)
            {
                morituri.Add(rel.Id);
            }

            foreach (var relId in morituri)
            {
                OriginPart.DeleteRelationship(relId);
            }

            #region Postconditions

#if DEBUGSLOW

            Dbc.Ensure(
                Specs().All(aSpec => aSpec.Uri != part.Uri),
                "The spec must not be listed in the Specs().");

            var specUriSet = new HashSet<Uri>(
                Specs().Select(spec => spec.Uri));

            Dbc.Ensure(
                !oldSpecUriSet.Contains(part.Uri)
                || (
                    specUriSet.Count == oldSpecUriSet.Count - 1
                    && oldSpecUriSet.Except(specUriSet).First() == part.Uri
                ),
                "No other spec has been removed.");

#endif

            #endregion

            return part;
        }

        /**
         * <summary>
         * Relate the <paramref name="supplementary"/> to the <paramref name="spec"/>
         * as a supplementary material.
         * </summary>
         * <returns>
         * The same unchanged <paramref name="supplementary"/>.
         *
         * This is practical if you want to chain function calls.
         * </returns>
         */
        // ReSharper disable once UnusedMethodReturnValue.Global
        public Part RelateSupplementaryToSpec(Part supplementary, Part spec)
        {
            #region Preconditions

#if DEBUGSLOW

            Dbc.Require(
                IsSpec(spec),
                "The part fulfills the spec property.");

#endif

            #endregion


            var rels = spec.PackagePart.GetRelationshipsByType(
                Packaging.RelationType.AasxSupplementary);

            if (rels.All(rel => rel.TargetUri != supplementary.Uri))
            {
                spec.PackagePart.CreateRelationship(
                    supplementary.Uri,
                    TargetMode.Internal,
                    Packaging.RelationType.AasxSupplementary);
            }

            #region Postconditions

#if DEBUGSLOW

            Dbc.Ensure(
                SupplementariesFor(spec)
                    .FirstOrDefault(
                        suppl => suppl.Uri == supplementary.Uri) != null,
                "The supplementary must be listed.");

#endif

            #endregion

            return supplementary;
        }

        /**
         * <summary>
         * Remove the relation as supplementary between
         * the <paramref name="supplementary"/> and the <paramref name="spec"/>.
         * </summary>
         * <remarks>
         * If the relationship has not been previously established, do nothing.
         *
         * The caller needs to delete the part(s) herself by calling
         * <see cref="RemovePart"/>.</remarks>
         * <returns>
         * The same unchanged <paramref name="supplementary"/>.
         *
         * This is practical if you want to chain function calls.
         * </returns>
         */
        // ReSharper disable once UnusedMethodReturnValue.Global
        public Part UnrelateSupplementaryFromSpec(Part supplementary, Part spec)
        {
            #region Preconditions

#if DEBUGSLOW

            Dbc.Require(
                IsSpec(spec),
                "The part fulfills the spec property.");

#endif

            #endregion

            #region Snapshots

#if DEBUGSLOW
            var oldSupplUriSet = new HashSet<Uri>(
                SupplementariesFor(spec).Select(suppl => suppl.Uri));

#endif

            #endregion

            var morituri = new List<string>();

            var rels = spec
                .PackagePart
                .GetRelationshipsByType(Packaging.RelationType.AasxSupplementary)
                .Where(
                    aRel => aRel.TargetUri == supplementary.Uri);

            foreach (var rel in rels)
            {
                morituri.Add(rel.Id);
            }

            foreach (var relId in morituri)
            {
                spec.PackagePart.DeleteRelationship(relId);
            }

            #region Postconditions

#if DEBUGSLOW

            Dbc.Ensure(
                SupplementariesFor(spec)
                    .All(suppl => suppl.Uri != supplementary.Uri),
                "The supplementary file must not be " +
                "listed in the Supplementaries().");

            var supplUriSet = new HashSet<Uri>(
                SupplementariesFor(spec).Select(suppl => suppl.Uri));

            Dbc.Ensure(
                !oldSupplUriSet.Contains(supplementary.Uri)
                || (
                    supplUriSet.Count == oldSupplUriSet.Count - 1
                    && oldSupplUriSet.Except(supplUriSet).First() == supplementary.Uri
                ),
                "No other supplementary has been removed.");

#endif

            #endregion

            return supplementary;
        }

        /**
         * <summary>
         * Establish the relation from package origin to <paramref name="part"/>
         * as a thumbnail.
         * </summary>
         * <remarks>
         * If there is a relationship to a thumbnail already, it will be unmade first.
         * </remarks>
         * <returns>
         * The same unchanged <paramref name="part"/>.
         *
         * This is practical if you want to chain function calls.
         * </returns>
         */
        // ReSharper disable once UnusedMethodReturnValue.Global
        public Part MakeThumbnail(Part part)
        {
            bool createRelation = true;

            var maybeThumbnail = Thumbnail();
            if (maybeThumbnail != null)
            {
                if (maybeThumbnail.Uri != part.Uri)
                {
                    UnmakeThumbnail();
                }
                else
                {
                    createRelation = false;
                }
            }

            if (createRelation)
            {
                UnderlyingPackage.CreateRelationship(
                    part.Uri, TargetMode.Internal, Packaging.RelationType.Thumbnail);
            }

            #region Postconditions

#if DEBUG || DEBUGSLOW
            var thumbnail = Thumbnail();

            Dbc.Ensure(
                thumbnail != null,
                "The thumbnail must be available.");

            Dbc.Ensure(
                thumbnail == null || thumbnail.Uri == part.Uri,
                "The thumbnail must point to the part.");

#endif

            #endregion

            return part;
        }

        /**
         * <summary>
         * Remove the relation from package origin to any thumbnail, if specified.
         * </summary>
         * <remarks>
         * The caller needs to delete the part herself by calling
         * <see cref="RemovePart"/>.
         * </remarks>
         */
        public void UnmakeThumbnail()
        {
            var morituri =
                UnderlyingPackage.GetRelationshipsByType(
                        Packaging.RelationType.Thumbnail)
                    .Where(rel => rel.SourceUri.ToString() == "/")
                    .ToList();

            foreach (var moriturus in morituri)
            {
                UnderlyingPackage.DeleteRelationship(moriturus.Id);
            }

            #region Postconditions

#if DEBUG || DEBUGSLOW

            Dbc.Ensure(Thumbnail() == null,
                "The thumbnail must not exist any more");
#endif

            #endregion
        }

        /**
         * <summary>Flush the changes to the underlying storage layers.</summary>
         */
        public void Flush()
        {
            UnderlyingPackage.Flush();
        }
    }
}
