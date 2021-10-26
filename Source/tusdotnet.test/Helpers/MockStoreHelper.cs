using NSubstitute;
using System;
using System.Collections.Generic;
using tusdotnet.Interfaces;

namespace tusdotnet.test.Helpers
{
    internal static class MockStoreHelper
    {
        public static ITusStore CreateWithExtensions<TExtension1>()
        {
            return CreateWithExtensions(typeof(TExtension1));
        }

        public static ITusStore CreateWithExtensions<TExtension1, TExtension2>()
        {
            return CreateWithExtensions(typeof(TExtension1), typeof(TExtension2));
        }

        public static ITusStore CreateWithExtensions<TExtension1, TExtension2, TExtension3>()
        {
            return CreateWithExtensions(typeof(TExtension1), typeof(TExtension2), typeof(TExtension3));
        }

        public static ITusStore CreateWithExtensions<TExtension1, TExtension2, TExtension3, TExtension4>()
        {
            return CreateWithExtensions(typeof(TExtension1), typeof(TExtension2), typeof(TExtension3), typeof(TExtension4));
        }

        private static ITusStore CreateWithExtensions(params Type[] types)
        {
            var allTypes = new List<Type>(types.Length + 1)
            {
                typeof(ITusStore)
            };
            allTypes.AddRange(types);

            return (ITusStore)Substitute.For(allTypes.ToArray(), new object[0]);
        }
    }
}
