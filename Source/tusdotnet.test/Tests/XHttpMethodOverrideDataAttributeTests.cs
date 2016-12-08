using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using tusdotnet.test.Data;
using Xunit;

namespace tusdotnet.test.Tests
{
	
	public class XHttpMethodOverrideDataAttributeTests
	{
		[Fact]
		public void Supports_All_Methods_Supported_By_TusMiddleware()
		{
			var allSupportedMethods = new[] {"options", "head", "patch", "post"};
			var attr = new XHttpMethodOverrideDataAttribute().GetData(null).Select(f => f[0].ToString()).ToList();

			allSupportedMethods.Except(attr).Any().ShouldBeFalse();
			attr.Except(allSupportedMethods).Any().ShouldBeFalse();
		}
	}
}
