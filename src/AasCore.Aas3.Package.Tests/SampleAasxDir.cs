using Directory = System.IO.Directory;
using Environment = System.Environment;
using File = System.IO.File;
using InvalidOperationException = System.InvalidOperationException;
using Path = System.IO.Path;

using System.Collections.Generic;  // can't alias
using System.IO;  // can't alias
using System.Linq; // can't alias

namespace AasCore.Aas3.Package.Tests
{
    /**
     * <summary>List the directory with the downloaded AASX sample files.</summary> 
     */
    internal static class SampleAasxDir
    {
        private static string DirPathFromEnvironment()
        {
            var variable = "SAMPLE_AASX_DIR";

            var result = Environment.GetEnvironmentVariable(variable);

            if (result == null)
            {
                throw new InvalidOperationException(
                    $"The environment variable {variable} has not been set. " +
                    "Did you set it manually to the directory containing sample AASXs? " +
                    "Otherwise, run the test through Test.ps1?");
            }

            return result;
        }

        public static List<string> ListPaths()
        {
            var sampleAasxDir = DirPathFromEnvironment();

            if (!Directory.Exists(sampleAasxDir))
            {
                throw new InvalidOperationException(
                    "The directory containing the sample AASXs " +
                    $"does not exist or is not a directory: {sampleAasxDir}; " +
                    "did you download the samples with DownloadSamples.ps1?");
            }

            var result = Directory.GetFiles(sampleAasxDir)
                .Where(p => Path.GetExtension(p).ToLower() == ".aasx")
                .ToList();

            result.Sort();

            return result;
        }

        public static string Path34Festo()
        {
            var sampleAasxDir = DirPathFromEnvironment();
            var pth = Path.Combine(new[] { sampleAasxDir, "34_Festo.aasx" });
            if (!File.Exists(pth))
            {
                throw new FileNotFoundException(
                    $"Expected the AAS package to exist, but it does not: {pth}");
            }

            return pth;
        }
    }

}