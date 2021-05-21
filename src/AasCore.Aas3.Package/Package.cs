using ArgumentException = System.ArgumentException;
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
                if (!(package is null ^ exception is null))
                {
                    throw new ArgumentException(
                        $"{nameof(package)} is {package}, " +
                        $"{nameof(exception)} is {exception}; " +
                        "exclusivity expected, not both or none");
                }

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

            if (_package is null)
            {
                throw new InvalidOperationException(
                    $"Unexpected {nameof(_package)} null");
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
        private readonly PackagePart _part;

        public Part(Uri uri, PackagePart part)
        {
            Uri = uri;
            _part = part;
        }

        /**
         * MIME type
         */
        public string ContentType => _part.ContentType;

        /**
         * <returns>
         * Open a read stream.
         * 
         * The caller is responsible for disposing it.
         * </returns>
         */
        public Stream Stream()
        {
            return _part.GetStream(FileMode.Open, FileAccess.Read);
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
                "http://www.admin-shell.io/aasx/relationships/aasx-origin";

            internal const string AasxSpec =
                "http://www.admin-shell.io/aasx/relationships/aas-spec";

            internal const string AasxSupplementary =
                "http://www.admin-shell.io/aasx/relationships/aas-suppl";

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

            if (result is null)
            {
                throw new InvalidOperationException("unexpected");
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

            if (result.MaybeException is null && result.Must().Path != path)
            {
                throw new InvalidOperationException(
                    "The Path property of the package must match the input path.");
            }

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

            if (result is null)
            {
                throw new InvalidOperationException("unexpected");
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

            if (result.MaybeException is null && result.Must().Path != null)
            {
                throw new InvalidOperationException(
                    "The Path property of the package must be null " +
                    "if reading from a stream.");
            }

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
                Stream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
                toBeDisposed.Add(stream);

                var pkg = SystemPackage.Open(stream, FileMode.Open, FileAccess.ReadWrite);
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

            if (result is null)
            {
                throw new InvalidOperationException("unexpected");
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

            if (result.MaybeException is null && result.Must().Path != path)
            {
                throw new InvalidOperationException(
                    "The Path property of the package must match the input path.");
            }

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

            if (result is null)
            {
                throw new InvalidOperationException("unexpected");
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

            if (result.MaybeException is null && result.Must().Path != null)
            {
                throw new InvalidOperationException(
                    "The Path property of the package must be null " +
                    "if read/writing to a stream.");
            }

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

            if (result.Specs().Count() != 0)
            {
                throw new InvalidOperationException(
                    "Specs must be empty in a new package.");
            }

            if (result.Supplementaries().Count() != 0)
            {
                throw new InvalidOperationException(
                    "Supplementaries must be empty in a new package.");
            }

            if (result.Thumbnail() != null)
            {
                throw new InvalidOperationException(
                    "There must be no thumbnail in a new package.");
            }

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
            if (!result.All(
                item => item.Value.All(
                    spec => spec.ContentType == item.Key)))
            {
                throw new InvalidOperationException(
                    "The content type of spec must match its group.");
            }

            if (!result.All(item => item.Value.Count > 0))
            {
                throw new InvalidOperationException(
                    "Every entry in the result must contain non-empty specs.");
            }
#endif

            #endregion

            return result;
        }

        /**
         * <summary>List supplementary files contained in the package.</summary>
         */
        public IEnumerable<Part> Supplementaries()
        {
            var xs = OriginPart.GetRelationshipsByType(
                Packaging.RelationType.AasxSupplementary);

            foreach (var x in xs)
            {
                var supplUri = x.TargetUri;
                var supplPart = UnderlyingPackage.GetPart(supplUri);
                yield return new Part(supplUri, supplPart);
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
         * Write the <paramref name="content"/> of the spec to <paramref name="uri"/>.
         * </summary>
         * <remarks>
         * If the spec already exists at <paramref name="uri"/>, it will be overwritten.
         * </remarks>
         * <exception cref="InvalidDataException">
         * if the part exists, but the relationship has not been set appropriately.
         * </exception>
         */
        public void PutSpec(Uri uri, string contentType, byte[] content)
        {
            using MemoryStream ms = new MemoryStream(content, false);
            PutSpec(uri, contentType, ms);

            #region Postconditions

#if DEBUGSLOW

            var spec = Specs().FirstOrDefault(aSpec => aSpec.Uri == uri);
            if (spec is null)
            {
                throw new InvalidOperationException("Spec must be listed.");
            }

            if (!spec.ReadAllBytes().SequenceEqual(content))
            {
                throw new InvalidOperationException(
                    "Input content and re-read content must coincide on put.");
            }

#endif

            #endregion
        }

        /**
         * <summary>
         * Write the content of the <paramref name="stream"/> to the spec at
         * <paramref name="uri"/>.
         * </summary>
         * <remarks>
         * If the spec already exists at <paramref name="uri"/>, it will be overwritten.
         * </remarks>
         * <exception cref="InvalidDataException">
         * if the part exists, but the relationship has not been set appropriately.
         * </exception>
         */
        // ReSharper disable once MemberCanBePrivate.Global
        public void PutSpec(Uri uri, string contentType, Stream stream)
        {
            #region Snapshots

#if DEBUG || DEBUGSLOW
            var oldSpecCount = Specs().Count();
            var oldPartExists = UnderlyingPackage.PartExists(uri);
#endif

            #endregion

            if (UnderlyingPackage.PartExists(uri))
            {
                var rels = OriginPart.GetRelationshipsByType(
                    Packaging.RelationType.AasxSpec);

                bool found = rels.Any(rel => rel.TargetUri == uri);
                if (!found)
                {
                    throw new InvalidDataException(
                        $"The spec exists at the URI {uri}, " +
                        "but there was no relationship " +
                        $"of the type {Packaging.RelationType.AasxSpec} targeting it." +
                        (Path != null ? $" Path to the package: {Path}" : "")
                    );
                }

                var part = UnderlyingPackage.GetPart(uri);
                using var target = part.GetStream();
                target.SetLength(0);
                stream.CopyTo(target);
            }
            else
            {
                var part = UnderlyingPackage.CreatePart(uri, contentType);
                using var target = part.GetStream();
                stream.CopyTo(target);
                OriginPart.CreateRelationship(uri, TargetMode.Internal,
                    Packaging.RelationType.AasxSpec);
            }

            #region Postconditions

#if DEBUG || DEBUGSLOW
            var spec = Specs().FirstOrDefault(aSpec => aSpec.Uri == uri);
            if (spec is null)
            {
                throw new InvalidOperationException(
                    $"Spec must exist in Specs: {uri}");
            }

            if (spec.ContentType != contentType)
            {
                throw new InvalidOperationException(
                    $"Spec content type must coincide: {uri} {contentType}");
            }

            if (oldPartExists && Specs().Count() != oldSpecCount)
            {
                throw new InvalidOperationException(
                    "Spec count must remain constant on overwriting a spec.");
            }

            if (!oldPartExists && Specs().Count() != oldSpecCount + 1)
            {
                throw new InvalidOperationException(
                    "Spec count must increment 1 when creating a spec.");
            }

            if (FindPart(uri) is null)
            {
                throw new InvalidOperationException(
                    $"The part must exist on put: {uri}");
            }
#endif

            #endregion
        }

        /**
         * <summary>
         * Write the <paramref name="content"/> of the supplementary file
         * to <paramref name="uri"/>.
         * </summary>
         * <remarks>If the supplementary file already exists at <paramref name="uri"/>,
         * it will be overwritten.</remarks>
         * <exception cref="InvalidDataException">
         * if the part exists, but the relationship has not been set appropriately.
         * </exception>
         */
        public void PutSupplementary(Uri uri, string contentType, byte[] content)
        {
            using MemoryStream ms = new MemoryStream(content, false);
            PutSupplementary(uri, contentType, ms);

            #region Postconditions

#if DEBUGSLOW

            var suppl = Supplementaries().FirstOrDefault(
                aSuppl => aSuppl.Uri == uri);

            if (suppl is null)
            {
                throw new InvalidOperationException(
                    "The supplementary must be listed.");
            }

            if (!suppl.ReadAllBytes().SequenceEqual(content))
            {
                throw new InvalidOperationException(
                    "Input content and re-read content must coincide on put.");
            }

#endif

            #endregion
        }

        /**
         * <summary>
         * Write the content of the <paramref name="stream"/> to the given supplementary
         * file at <paramref name="uri"/>.
         * </summary>
         * <remarks>If the supplementary file already exists at <paramref name="uri"/>,
         * it will be overwritten.</remarks>
         * <exception cref="InvalidDataException">
         * if the part exists, but the relationship has not been set appropriately.
         * </exception>
         */
        // ReSharper disable once MemberCanBePrivate.Global
        public void PutSupplementary(Uri uri, string contentType, Stream stream)
        {
            #region Snapshots

#if DEBUG || DEBUGSLOW
            var oldSupplementaryCount = Supplementaries().Count();
            var oldPartExists = UnderlyingPackage.PartExists(uri);
#endif

            #endregion

            if (UnderlyingPackage.PartExists(uri))
            {
                var rels = OriginPart.GetRelationshipsByType(
                    Packaging.RelationType.AasxSupplementary);

                bool found = rels.Any(rel => rel.TargetUri == uri);
                if (!found)
                {
                    throw new InvalidDataException(
                        $"The supplementary exists at the URI {uri}, " +
                        "but there was no relationship " +
                        $"of the type {Packaging.RelationType.AasxSupplementary} " +
                        "targeting it." +
                        (Path != null ? $" Path to the package: {Path}" : "")
                    );
                }

                var part = UnderlyingPackage.GetPart(uri);
                using var target = part.GetStream();
                target.SetLength(0);
                stream.CopyTo(target);
            }
            else
            {
                var part = UnderlyingPackage.CreatePart(uri, contentType);
                using var target = part.GetStream();
                stream.CopyTo(target);
                OriginPart.CreateRelationship(uri, TargetMode.Internal,
                    Packaging.RelationType.AasxSupplementary);
            }

            #region Postconditions

#if DEBUG || DEBUGSLOW
            var suppl = Supplementaries().FirstOrDefault(
                aSuppl => aSuppl.Uri == uri);

            if (suppl is null)
            {
                throw new InvalidOperationException(
                    $"The supplementary must be listed in Supplementaries: {uri}");
            }

            if (suppl.ContentType != contentType)
            {
                throw new InvalidOperationException(
                    $"The content type must coincide: {uri} {contentType}");
            }

            if (oldPartExists && Supplementaries().Count() != oldSupplementaryCount)
            {
                throw new InvalidOperationException(
                    "The supplementary count must remain constant " +
                    "on overwriting.");
            }

            if (!oldPartExists && Supplementaries().Count() != oldSupplementaryCount + 1)
            {
                throw new InvalidOperationException(
                    "Supplementary count must increment 1 on " +
                    "creating a supplementary.");
            }

            if (FindPart(uri) is null)
            {
                throw new InvalidOperationException(
                    $"The part must exist on put: {uri}");
            }
#endif

            #endregion
        }

        /**
         * <summary>
         * Set the thumbnail to the <paramref name="content"/> at <paramref name="uri"/>.
         *
         * If there is already a thumbnail at a different URI,
         * the <paramref name="deleteExisting"/> decides whether the existing part
         * will be deleted (or we update only the relationship).
         * </summary>
         * <remarks>If the thumbnail already exists at <paramref name="uri"/>,
         * it will be overwritten.</remarks>
         * <exception cref="InvalidDataException">
         * if the part exists at <paramref name="uri"/>,
         * but the relationship has not been set appropriately.
         * </exception>
         */
        public void PutThumbnail(
            Uri uri, string contentType, byte[] content, bool deleteExisting)
        {
            using MemoryStream ms = new MemoryStream(content, false);
            PutThumbnail(uri, contentType, ms, deleteExisting);

            #region Postconditions

#if DEBUGSLOW

            var thumb = Thumbnail();

            if (thumb is null)
            {
                throw new InvalidOperationException(
                    "The thumbnail must be available.");
            }

            if (!thumb.ReadAllBytes().SequenceEqual(content))
            {
                throw new InvalidOperationException(
                    "Input content and re-read content must coincide on put.");
            }

#endif

            #endregion
        }

        /**
         * <summary>
         * Set the thumbnail to the content of the <paramref name="stream"/>
         * at <paramref name="uri"/>.
         *
         * If there is already a thumbnail at a different URI,
         * the <paramref name="deleteExisting"/> decides whether the existing part
         * will be deleted (or we update only the relationship).
         * </summary>
         * <remarks>If the thumbnail already exists at <paramref name="uri"/>,
         * it will be overwritten.</remarks>
         * <exception cref="InvalidDataException">
         * if the part exists at <paramref name="uri"/>,
         * but the relationship has not been set appropriately.
         * </exception>
         */
        // ReSharper disable once MemberCanBePrivate.Global
        public void PutThumbnail(Uri uri, string contentType, Stream stream,
            bool deleteExisting = true)
        {
            #region Snapshots

#if DEBUG || DEBUGSLOW

            var oldThumbnailUri = Thumbnail()?.Uri;

#endif

            #endregion

            if (UnderlyingPackage.PartExists(uri))
            {
                var rels = UnderlyingPackage.GetRelationshipsByType(
                    Packaging.RelationType.Thumbnail);

                if (rels.All(rel => rel.TargetUri != uri))
                {
                    throw new InvalidDataException(
                        $"The thumbnail exists at the URI {uri}, " +
                        "but there was no relationship " +
                        $"of the type {Packaging.RelationType.Thumbnail} " +
                        "targeting it." +
                        (Path != null ? $" Path to the package: {Path}" : "")
                    );
                }

                var part = UnderlyingPackage.GetPart(uri);
                using var target = part.GetStream();
                stream.CopyTo(target);
            }
            else
            {
                var morituri =
                    UnderlyingPackage.GetRelationshipsByType(
                            Packaging.RelationType.Thumbnail)
                        .Where(rel => rel.SourceUri.ToString() == "/")
                        .ToList();

                foreach (var moriturus in morituri)
                {
                    UnderlyingPackage.DeleteRelationship(moriturus.Id);
                    if (deleteExisting)
                    {
                        UnderlyingPackage.DeletePart(moriturus.TargetUri);
                    }
                }

                var part = UnderlyingPackage.CreatePart(uri, contentType);
                using var target = part.GetStream();
                stream.CopyTo(target);
                UnderlyingPackage.CreateRelationship(
                    uri, TargetMode.Internal,
                    Packaging.RelationType.Thumbnail);
            }

            #region Postconditions

#if DEBUG || DEBUGSLOW
            var thumbnail = Thumbnail();

            if (thumbnail is null)
            {
                throw new InvalidOperationException(
                    $"The thumbnail must be available: {uri}");
            }

            if (thumbnail.ContentType != contentType)
            {
                throw new InvalidOperationException(
                    $"The content type must coincide: {uri} {contentType}");
            }

            if (FindPart(uri) is null)
            {
                throw new InvalidOperationException(
                    $"The part must exist on put: {uri}");
            }

            if (oldThumbnailUri != uri
                && deleteExisting
                && oldThumbnailUri != null
                && UnderlyingPackage.PartExists(oldThumbnailUri))
            {
                throw new InvalidOperationException(
                    $"The previous thumbnail at {oldThumbnailUri} must be " +
                    $"deleted when replaced with a new thumbnail at {uri}.");
            }

            if (oldThumbnailUri != uri
                && !deleteExisting
                && oldThumbnailUri != null
                && !UnderlyingPackage.PartExists(oldThumbnailUri))
            {
                throw new InvalidOperationException(
                    $"The previous thumbnail at {oldThumbnailUri} must be " +
                    $"kept when replaced with a new thumbnail at {uri} " +
                    $"and {nameof(deleteExisting)} is set.");
            }
#endif

            #endregion
        }

        /**
         * <summary>Remove the spec by the given <paramref name="uri"/>.</summary>
         * <remarks>
         * If the part corresponding to <paramref name="uri"/> does not exist,
         * do nothing.
         * </remarks>
         * <exception cref="InvalidDataException">
         * if the part exists at <paramref name="uri"/>,
         * but the relationship has not been set appropriately.
         * </exception>
         */
        public void RemoveSpec(Uri uri)
        {
            if (UnderlyingPackage.PartExists(uri))
            {
                var rel = OriginPart
                    .GetRelationshipsByType(Packaging.RelationType.AasxSpec)
                    .FirstOrDefault(aRel => aRel.TargetUri == uri);

                if (rel is null)
                {
                    throw new InvalidDataException(
                            $"The part exists at the URI {uri}, " +
                            "but there was no relationship " +
                            $"of the type {Packaging.RelationType.AasxSpec} " +
                            "targeting it." +
                            (Path != null ? $" Path to the package: {Path}" : "")
                        );
                }

                OriginPart.DeleteRelationship(rel.Id);
                UnderlyingPackage.DeletePart(uri);

                #region Postconditions

#if DEBUG || DEBUGSLOW
                if (UnderlyingPackage.PartExists(uri))
                {
                    throw new InvalidOperationException(
                        $"The part must have been deleted: {uri}");
                }
#endif

#if DEBUGSLOW
                if (Specs().Any(aSpec => aSpec.Uri == uri))
                {
                    throw new InvalidOperationException(
                        $"The spec must not be listed in the Specs: {uri}");
                }

#endif

                #endregion
            }
        }

        /**
         * <summary>
         * Remove the supplementary file by the given <paramref name="uri"/>.
         * </summary>
         * <remarks>
         * If the part corresponding to <paramref name="uri"/> does not exist,
         * do nothing.
         * </remarks>
         * <exception cref="InvalidDataException">
         * if the part exists at <paramref name="uri"/>,
         * but the relationship has not been set appropriately.
         * </exception>
         */
        public void RemoveSupplementary(Uri uri)
        {
            if (UnderlyingPackage.PartExists(uri))
            {
                var rel = OriginPart
                    .GetRelationshipsByType(Packaging.RelationType.AasxSupplementary)
                    .FirstOrDefault(aRel => aRel.TargetUri == uri);

                if (rel is null)
                {
                    throw new InvalidDataException(
                        $"The part exists at the URI {uri}, " +
                        "but there was no relationship " +
                        $"of the type {Packaging.RelationType.AasxSupplementary} " +
                        "targeting it." +
                        (Path != null ? $" Path to the package: {Path}" : "")
                    );
                }

                OriginPart.DeleteRelationship(rel.Id);
                UnderlyingPackage.DeletePart(uri);

                #region Postconditions

#if DEBUG || DEBUGSLOW
                if (UnderlyingPackage.PartExists(uri))
                {
                    throw new InvalidOperationException(
                        $"The part must have been deleted: {uri}");
                }
#endif

#if DEBUGSLOW
                if (Supplementaries().Any(suppl => suppl.Uri == uri))
                {
                    throw new InvalidOperationException(
                        "The supplementary file must not be " +
                        $"listed in the Supplementaries: {uri}");
                }

#endif

                #endregion
            }
        }

        /**
         * <summary>Remove the thumbnail.</summary>
         * <remarks>If the thumbnail does not exist, do nothing.</remarks>
         */
        public void RemoveThumbnail()
        {
            var thumb = Thumbnail();
            if (thumb != null)
            {
                var morituri =
                    UnderlyingPackage.GetRelationshipsByType(
                            Packaging.RelationType.Thumbnail)
                        .Where(rel => rel.SourceUri.ToString() == "/")
                        .ToList();

                foreach (var moriturus in morituri)
                {
                    UnderlyingPackage.DeleteRelationship(moriturus.Id);
                    UnderlyingPackage.DeletePart(moriturus.TargetUri);
                }
            }

            #region Postconditions

#if DEBUG || DEBUGSLOW
            if (Thumbnail() != null)
            {
                throw new InvalidOperationException(
                    "The thumbnail must not exist any more");
            }
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