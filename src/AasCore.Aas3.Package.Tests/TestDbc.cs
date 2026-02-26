using NUnit.Framework;  // can't alias
using ArgumentException = System.ArgumentException;
using InvalidOperationException = System.InvalidOperationException;

namespace AasCore.Aas3.Package.Tests
{
    public class TestDbc
    {
        [Test]
        public void Test_that_require_doesnt_throw_if_ok()
        {
            Dbc.Require(true, "something");
        }

        [Test]
        public void Test_the_exception_if_require_fails()
        {
            Assert.Catch<ArgumentException>(() =>
            {
                Dbc.Require(false, "something");
            });
        }

        [Test]
        public void Test_that_ensure_doesnt_throw_if_ok()
        {
            Dbc.Ensure(true, "something");
        }

        [Test]
        public void Test_the_exception_if_ensure_fails()
        {
            Assert.Catch<InvalidOperationException>(() =>
            {
                Dbc.Ensure(false, "something");
            });
        }
    }
}
