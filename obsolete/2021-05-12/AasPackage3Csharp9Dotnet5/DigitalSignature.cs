using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Net.Mime;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

// ReSharper disable once CheckNamespace
namespace Aas3.OPCPackaging
{
    /**
     * <summary>Provide internal constants used for digital signatures.</summary>
     */
    static class Constants
    {
        internal static readonly Uri DefaultOriginPartName = PackUriHelper.CreatePartUri(
            new Uri("/package/services/digital-signature/origin.psdsor",
                UriKind.Relative));

        internal static readonly ContentType OriginPartContentType = new ContentType(
            "application/vnd.openxmlformats-package.digital-signature-origin");

        internal static readonly string GuidStorageFormatString = @"N";
        internal static readonly string DefaultHashAlgorithm = SignedXml.XmlDsigSHA256Url;

        internal static readonly string OriginRelationshipType =
            "http://schemas.openxmlformats.org/package/2006/relationships/digital-signature/origin";

        internal static readonly string OriginToSignatureRelationshipType =
            "http://schemas.openxmlformats.org/package/2006/relationships/digital-signature/signature";

        internal static readonly string DefaultSignaturePartNamePrefix =
            "/package/services/digital-signature/xml-signature/";

        internal static readonly string DefaultSignaturePartNameExtension =
            ".psdsxs";

        internal static readonly ContentType XmlSignaturePartType
            = new("application/" +
                  "vnd.openxmlformats-package.digital-signature-xmlsignature+xml");
    }

    /**
     * Represent a signature of the package.
     */
    public class Signature
    {
        /**
         * Part of the package corresponding to the signature
         */
        public readonly PackagePart Part;

        public Signature(PackagePart part)
        {
            Part = part;
        }
    }

    /**
     * Either found signatures or exception while reading them.
     *
     * If no signature origin was found, the list of signatures is empty.
     */
    public class SignaturesOrException
    {
        private readonly PackagePart? _origin;
        private readonly List<Signature>? _signatures;
        public readonly Exception? MaybeException;

        internal SignaturesOrException(
            PackagePart? origin, List<Signature>? signatures, Exception? exception)
        {
            #region Preconditions

            if (!(signatures is null ^ exception is null))
            {
                throw new ArgumentException(
                    $"{nameof(signatures)} is {signatures}, " +
                    $"{nameof(exception)} is {exception}; " +
                    "exclusivity expected, not both or none");
            }

            if (signatures is null && origin is not null)
            {
                throw new ArgumentException(
                    $"{nameof(signatures)} are null, " +
                    $"but {nameof(origin)} is {origin}. " +
                    $"If there was an origin, there must have been signatures " +
                    $"(even as an empty list).");
            }

            if (origin is null && signatures is not null && signatures.Count > 0)
            {
                throw new ArgumentException(
                    $"Unexpected non-empty ${nameof(signatures)} {signatures}, " +
                    $"but null {nameof(origin)}.");
            }

            #endregion

            _origin = origin;
            _signatures = signatures;
            MaybeException = exception;
        }

        /**
         * <summary>
         * Return the origin of the signatures or throw the associated exception.
         * </summary>
         * <returns>the part, or null if no origin could be found.</returns>
         */
        public PackagePart? MaybeOrigin()
        {
            if (MaybeException is not null)
            {
                throw MaybeException;
            }

            return _origin;
        }

        /**
         * <summary>
         * Return the list of signatures or throw the associated exception.
         * </summary>
         */
        public List<Signature> MustSignatures()
        {
            if (MaybeException is not null)
            {
                throw MaybeException;
            }

            #region Postconditions

#if DEBUG || DEBUGSLOW

            if (_signatures is not null && _origin is null)
            {
                throw new InvalidOperationException(
                    "Unexpected non-null signatures, but null origin.");
            }

#endif

            if (_signatures is null)
            {
                throw new InvalidOperationException(
                    $"Unexpected {nameof(_signatures)} null");
            }

            #endregion

            return _signatures;
        }
    }

    /**
     * <summary>
     * Either found a digital signature, did not find it or there was an exception.
     * </summary>
     */
    class OriginOrException
    {
        private readonly PackagePart? _origin;
        public readonly bool Found;
        public readonly Exception? MaybeException;

        public OriginOrException(
            PackagePart? origin, bool found, Exception? exception)
        {
            #region Preconditions

            if (found && exception is not null)
            {
                throw new ArgumentException(
                    $"Unexpected {nameof(found)} " +
                    $"when the {nameof(exception)} is not null.");
            }

            if (found && origin is null)
            {
                throw new ArgumentException(
                    $"Unexpected {nameof(found)} " +
                    $"when the {nameof(origin)} is null.");
            }

            if (!found && origin is not null)
            {
                throw new ArgumentException(
                    $"Unexpected not {nameof(found)} " +
                    $"when the {nameof(origin)} is not null.");
            }

            if (exception is not null && origin is not null)
            {
                throw new ArgumentException(
                    $"Unexpected non-null {nameof(origin)} " +
                    $"when the {nameof(exception)} is not null.");
            }

            #endregion

            _origin = origin;
            Found = found;
            MaybeException = exception;
        }

        /**
             * <summary>Return the origin or throw the associated exception.</summary> 
             */
        public PackagePart Must()
        {
            if (MaybeException is not null)
            {
                throw MaybeException;
            }

            if (!Found)
            {
                throw new InvalidOperationException(
                    "The origin part was not found.");
            }

            if (_origin is null)
            {
                throw new InvalidOperationException(
                    $"Unexpected {nameof(_origin)} null");
            }

            return _origin;
        }
    }

    internal class Signing
    {
        internal static OriginOrException FindOrigin(Package package)
        {
            OriginOrException? originFound = null;

            foreach (var r in
                package.GetRelationshipsByType(Constants.OriginRelationshipType))
            {
                if (r.TargetMode != TargetMode.Internal)
                {
                    return new OriginOrException(null, false,
                        new FileFormatException(
                            $"Unexpected non-internal relationship {r} " +
                            $"of the type {Constants.OriginRelationshipType}"));
                }

                Uri targetUri = PackUriHelper.ResolvePartUri(r.SourceUri, r.TargetUri);

                if (!package.PartExists(targetUri))
                {
                    return new OriginOrException(null, false,
                        new FileFormatException(
                            $"Signature origin could not be found " +
                            $"from relation {r.Id} pointing to {targetUri}."));
                }

                PackagePart p = package.GetPart(targetUri);

                // Inspect content type and ignore things we don't understand
                if (!string.Equals(
                    new ContentType(p.ContentType).Name,
                    Constants.OriginPartContentType.Name,
                    StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                if (originFound is not null)
                {
                    return new OriginOrException(null, false,
                        new FileFormatException(
                            $"Multiple origins: " +
                            $"one at URI {originFound.Must().Uri} and other at " +
                            $"{targetUri}."));
                }

                originFound = new OriginOrException(p, true, null);
            }

            if (originFound is null)
            {
                return new OriginOrException(null, false, null);
            }

            return originFound;
        }
        
                /**
         * <summary>List all the digital signatures contained in the package.</summary>
         */
        internal SignaturesOrException ListSignatures(Package package)
        {
            var originFoundOrErr = FindOrigin(package);
            if (originFoundOrErr.MaybeException is not null)
            {
                return new SignaturesOrException(
                    null, null, originFoundOrErr.MaybeException);
            }

            if (!originFoundOrErr.Found)
            {
                return new SignaturesOrException(
                    null, new List<Signature>(), null);
            }

            List<Signature> signatures = new();
            var origin = originFoundOrErr.Must();

            foreach (var r in
                origin.GetRelationshipsByType(
                    Constants.OriginToSignatureRelationshipType))
            {
                if (r.TargetMode != TargetMode.Internal)
                {
                    return new SignaturesOrException(
                        null,
                        null,
                        new FileFormatException(
                            $"Unexpected " +
                            $"non-internal target mode {r.TargetMode} " +
                            $"from the origin {origin.Uri} " +
                            $"in a relationship {r.Id} " +
                            $"of type {Constants.OriginToSignatureRelationshipType}")
                    );
                }

                Uri signaturePartName = PackUriHelper.ResolvePartUri(
                    origin.Uri, r.TargetUri);

                if (!package.PartExists(signaturePartName))
                {
                    return new SignaturesOrException(
                        null,
                        null,
                        new FileFormatException(
                            $"The signature part {signaturePartName} " +
                            $"could not be found in the package pointed " +
                            $"by the relationship {r.Id} " +
                            $"from {origin.Uri} to {r.TargetUri}.")
                    );
                }

                PackagePart signaturePart = package.GetPart(signaturePartName);

                // Consider only signature types that we recognize
                if (string.Equals(
                    new ContentType(signaturePart.ContentType).Name,
                    Constants.XmlSignaturePartType.Name,
                    StringComparison.InvariantCultureIgnoreCase
                ))
                {
                    signatures.Add(new Signature(signaturePart));
                }
            }

            return new SignaturesOrException(origin, signatures, null);
        }
    }

    /**
     * <summary>
     * Sign Open Package Convention packages.
     * </summary>
     * <remarks>
     * Inspired by:
     * https://referencesource.microsoft.com/#WindowsBase/Base/System/IO/Packaging/PackageDigitalSignatureManager.cs and
     * https://referencesource.microsoft.com/#WindowsBase/Base/MS/Internal/IO/Packaging/XmlDigitalSignatureProcessor.cs.
     * </remarks>
     */
    public class Signer
    {
        private readonly string _hashAlgorithm;
        private readonly string _timeFormat;

        private static readonly SortedSet<string> _supportedHashAlgorithms = new()
        {
            "SHA1"
        };

        private static readonly SortedSet<string> _supportedTimeFormats = new()
        {
            "YYYY-MM-DDThh:mm:ss.sTZD",
            "YYYY-MM-DDThh:mm:ssTZD",
            "YYYY-MM-DDThh:mmTZD",
            "YYYY-MM-DD",
            "YYYY-MM",
            "YYYY"
        };

        /**
         * <summary>Define a signer for signing OPC packages.</summary>
         * <param name="hashAlgorithm">
         * Hash algorithm to use when creating/verifying signatures
         * </param>
         * <param name="timeFormat">
         * Legal formats specified in Opc book
         * <ul>
         * <li><c>YYYY-MM-DDThh:mm:ss.sTZD</c></li>
         <li><c>YYYY-MM-DDThh:mm:ssTZD</c></li>
         <li><c>YYYY-MM-DDThh:mmTZD</c></li>
         <li><c>YYYY-MM-DD</c></li>
         <li><c>YYYY-MM</c></li>
         <li><c>YYYY</c></li>
         * </ul>
         * 
         * where:
         * Y = year, M = month integer (leading zero), D = day integer (leading zero), 
         * hh = 24hr clock hour
         * mm = minutes (leading zero)
         * ss = seconds (leading zero)
         * .s = tenths of a second
         * </param>
         */
        public Signer(string hashAlgorithm, string timeFormat)
        {
            #region Preconditions

            if (!_supportedHashAlgorithms.Contains(hashAlgorithm))
            {
                throw new ArgumentException(
                    $"Unsupported hash algorithm: {hashAlgorithm}; " +
                    $"supported algorithms: " +
                    $"{string.Join(", ", _supportedHashAlgorithms)}");
            }

            if (!_supportedTimeFormats.Contains(timeFormat))
            {
                throw new ArgumentException(
                    $"Unsupported time format: {timeFormat}; " +
                    $"supported time formats: " +
                    $"{string.Join(", ", _supportedTimeFormats)}");
            }
            
            #endregion 
            
            _hashAlgorithm = hashAlgorithm;
            _timeFormat = timeFormat;
        }

        // DONT-CHECK-IN: document
        public void Sign(
            Package package,
            IEnumerable<Uri> parts,
            X509Certificate certificate,
            IEnumerable<PackageRelationshipSelector> relationshipSelectors,
            string? signatureId = null
        )
        {
            #region Preconditions

            if (signatureId is not null && signatureId != string.Empty)
            {
                try
                {
                    XmlConvert.VerifyNCName(signatureId);
                }
                catch (XmlException xmlException)
                {
                    throw new ArgumentException(
                        $"{nameof(signatureId)} == {signatureId} " +
                        $"must be a valid XML identifier, but it is not",
                        xmlException);
                }
            }

            var firstMissingPart = parts.FirstOrDefault(
                part => !package.PartExists(part));
            if (firstMissingPart is not null)
            {
                throw new ArgumentException(
                    "The part to be signed is missing in the package: " +
                    firstMissingPart);
            }

            // Check the previous signatures. If they are valid, we will have to restore
            // them in case something goes wrong with the new signature.
            var signaturesOrErr = ListSignatures(package);
            if (signaturesOrErr.MaybeException is not null)
            {
                throw new ArgumentException(
                    "The package has invalid previous signature.",
                    signaturesOrErr.MaybeException);
            }

            #endregion

            if (signatureId is null || signatureId == string.Empty)
            {
                signatureId = "packageSignature"; // default
            }

            // Generate a new signature name
            Uri newSignaturePartName = PackUriHelper.CreatePartUri(
                new Uri(Constants.DefaultSignaturePartNamePrefix +
                        Guid.NewGuid().ToString(
                            Constants.GuidStorageFormatString, null) +
                        Constants.DefaultSignaturePartNameExtension,
                    UriKind.Relative));

            if (package.PartExists(newSignaturePartName))
            {
                throw new InvalidOperationException(
                    $"Unexpected signature part: {newSignaturePartName}");
            }

            // Add a new origin if it does not exist already
            var origin = signaturesOrErr.MaybeOrigin();
            if (origin is null)
            {
                origin = package.CreatePart(
                    Constants.DefaultOriginPartName,
                    Constants.OriginPartContentType.ToString());

                package.CreateRelationship(
                    Constants.DefaultOriginPartName,
                    TargetMode.Internal,
                    Constants.OriginRelationshipType);
            }

            // Create a new origin that will point to the new signature
            var relationshipToNewSignature = origin.CreateRelationship(
                newSignaturePartName, TargetMode.Internal,
                Constants.OriginToSignatureRelationshipType);

            // Persist the new origin and the corresponding relationship so that
            // all new signatures are using this newest relationship
            package.Flush();

            // Convert cert to version2 for more functionality
            X509Certificate2? exSigner = certificate as X509Certificate2;
            if (exSigner is null)
            {
                exSigner = new X509Certificate2(certificate.Handle);
            }


            // DONT-CHECK-IN: the signing itself needs to be re-implemented. oh gosh...
        }
    }
}