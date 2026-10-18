using System.Linq;
using System.Reflection;

namespace AccessibleDemo.Models
{
	public static class DeploymentInfo
	{
		public static string Revision => Read("DeploymentRevision");
		public static string BuildTimestamp => Read("BuildTimestampUtc");

		private static string Read(string key) => typeof(DeploymentInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
			.FirstOrDefault(attribute => attribute.Key == key)?.Value ?? "unavailable";
	}
}
