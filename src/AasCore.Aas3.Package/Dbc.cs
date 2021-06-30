using ArgumentException = System.ArgumentException;
using InvalidOperationException = System.InvalidOperationException;

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

        public static void Ensure(bool value, string message)
        {
            if (!value)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}