using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace RealTimeTabSynchronizer.Server.EntityFramework
{
	public interface IModelBuildingService
	{
		void ConfigureEntitiesMapping(ModelBuilder modelBuilder);
	}
}