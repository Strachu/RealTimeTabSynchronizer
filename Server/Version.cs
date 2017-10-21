
using System;

namespace RealTimeTabSynchronizer.Server
{
	public static class Version
	{
		public const string CurrentVersion = "0.1.0";

		public static string CopyrightNotice =>
			$"RealTimeTabSynchronizer {Version.CurrentVersion}" + Environment.NewLine +
			$"Copyright © 2017 Patryk Strach";
	}
}
