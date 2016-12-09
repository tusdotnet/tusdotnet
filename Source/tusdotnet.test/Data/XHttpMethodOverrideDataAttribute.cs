using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit.Sdk;

namespace tusdotnet.test.Data
{
	/// <summary>
	/// Data attribute to provide all available methods for testing X-Http-Method-Override.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, Inherited = false)]
	internal sealed class XHttpMethodOverrideDataAttribute : DataAttribute
	{
		private static readonly string[] AllSupportedMethods = {"options", "head", "patch", "post"};
		
		public override IEnumerable<object[]> GetData(MethodInfo testMethod)
		{
			return AllSupportedMethods.Select(f => new[] {f}).ToArray();
		}
	}
}
