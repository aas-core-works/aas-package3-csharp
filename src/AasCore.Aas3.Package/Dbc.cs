using ArgumentException = System.ArgumentException;
using InvalidOperationException = System.InvalidOperationException;
using NotNullAttribute = System.Diagnostics.CodeAnalysis.NotNullAttribute;

namespace AasCore.Aas3.Package
{
    /**
     * Provide methods for design-by-contract.
     */
    public static class Dbc
    {
        public static void Require(bool value, string message)
        {
            if (!value)
            {
                throw new ArgumentException(message);
            }
        }

        public static void AssertIsNotNull([NotNull] object? @object)
        {
            if (@object == null)
            {
                throw new InvalidOperationException("Unexpected null");
            }
        }


        public static void Ensure(bool value, string message)
        {
            if (!value)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}