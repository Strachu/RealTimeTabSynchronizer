using System;
using NUnit.Framework;

namespace RealTimeTabSynchronizer.Server.Tests.TestTools
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class TestFixtureForAllDatabasesAttribute : TestFixtureSourceAttribute
	{
		public TestFixtureForAllDatabasesAttribute()
			: base(typeof(DbContextFactoryProvider), nameof(DbContextFactoryProvider.GetForAllSupportedDatabases))
		{
		}
	}
}