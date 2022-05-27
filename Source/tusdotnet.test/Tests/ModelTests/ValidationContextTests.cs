using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using NSubstitute;
using Shouldly;
using tusdotnet.Adapters;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using Xunit;

namespace tusdotnet.test.Tests.ModelTests
{
    public class ValidationContextTests
    {
        private ValidationContextForTest SUT { get; } = ValidationContextForTest.Create(GetContext());

        [Fact]
        public void Message_Is_Concatenated_If_FailRequest_Is_Called_Multiple_Times_With_Message()
        {
            SUT.FailRequest("a");
            SUT.FailRequest("b");
            SUT.FailRequest("c");

            AssertStatusAndMessage(HttpStatusCode.BadRequest, "abc");
        }

        [Fact]
        public void StatusCode_Is_Replaced_If_FailRequest_Is_Called_Multiple_Times_With_StatusCode()
        {
            SUT.FailRequest(HttpStatusCode.Conflict);
            AssertStatusAndMessage(HttpStatusCode.Conflict, null);

            SUT.FailRequest(HttpStatusCode.Ambiguous);
            AssertStatusAndMessage(HttpStatusCode.Ambiguous, null);

            SUT.FailRequest(HttpStatusCode.Accepted);
            AssertStatusAndMessage(HttpStatusCode.Accepted, null);
        }

        [Fact]
        public void StatusCode_And_Message_Are_Replaced_If_FailRequest_Is_Called_Multiple_Times_With_StatusCode_And_Message()
        {
            SUT.FailRequest(HttpStatusCode.Conflict, "a");
            AssertStatusAndMessage(HttpStatusCode.Conflict, "a");

            SUT.FailRequest(HttpStatusCode.Ambiguous, "b");
            AssertStatusAndMessage(HttpStatusCode.Ambiguous, "b");

            SUT.FailRequest(HttpStatusCode.Accepted, "c");
            AssertStatusAndMessage(HttpStatusCode.Accepted, "c");
        }

        [Fact]
        public void Message_Is_Removed_If_FailRequest_Is_First_Called_With_StatusCode_And_Message_And_Then_By_Only_StatusCode()
        {
            SUT.FailRequest(HttpStatusCode.Conflict, "a");
            AssertStatusAndMessage(HttpStatusCode.Conflict, "a");

            SUT.FailRequest(HttpStatusCode.Ambiguous);
            AssertStatusAndMessage(HttpStatusCode.Ambiguous, null);
        }

        [Fact]
        public void Message_Is_Removed_If_FailRequest_Is_First_Called_With_Message_And_Then_By_Only_StatusCode()
        {
            SUT.FailRequest("a");
            SUT.FailRequest("b");
            SUT.FailRequest("c");
            AssertStatusAndMessage(HttpStatusCode.BadRequest, "abc");

            SUT.FailRequest(HttpStatusCode.Ambiguous);
            AssertStatusAndMessage(HttpStatusCode.Ambiguous, null);
        }

        private void AssertStatusAndMessage(HttpStatusCode expectedStatusCode, string expectedMessage)
        {
            SUT.StatusCode.ShouldBe(expectedStatusCode);
            SUT.ErrorMessage.ShouldBe(expectedMessage);
        }

        private static ContextAdapter GetContext()
        {
            return new ContextAdapter("/files")
            {
                CancellationToken = CancellationToken.None,
                Configuration = new DefaultTusConfiguration
                {
                    Store = Substitute.For<ITusStore>(),
                    UrlPath = "/files",
                },
                Request = new RequestAdapter()
                {
                    Body = new MemoryStream(),
                    Headers = new RequestHeaders(),
                    Method = "post",
                    RequestUri = new Uri("https://localhost/files", UriKind.Absolute)
                }
            };
        }

        private class ValidationContextForTest : ValidationContext<ValidationContextForTest>
        {
        }
    }
}
