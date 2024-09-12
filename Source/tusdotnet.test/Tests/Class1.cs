#if NET6_0_OR_GREATER
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace tusdotnet.test.Tests
{
    public class Class1
    {
        [Fact]
        public void VerifyCalculation()
        {
            for (int i = 0; i < 1000; i++)
            {
                var data = new byte[10485760]; // 10 MB
                Random.Shared.NextBytes(data);

                var oldWay = CalculateSha1Old(new MemoryStream(data, false));
                var newWay = CalculateSh1FakeReading(data);

                oldWay.SequenceEqual(newWay).ShouldBeTrue();
            }
        }

        private static byte[] CalculateSh1FakeReading(byte[] data)
        {
            using var sha1 = SHA1.Create();
            var chunks = data.Chunk(4096);
            foreach (var item in chunks)
            {
                sha1.TransformBlock(item, 0, item.Length, null, 0);
            }

            sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            return sha1.Hash;
        }

        public static byte[] CalculateSha1Old(Stream fileStream)
        {
            byte[] fileHash;
            using (var sha1 = SHA1.Create())
            {
                fileHash = sha1.ComputeHash(fileStream);
            }

            return fileHash;
        }
    }
}

#endif