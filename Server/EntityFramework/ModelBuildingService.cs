using System;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace RealTimeTabSynchronizer.Server.EntityFramework
{
	public class ModelBuildingService : IModelBuildingService
	{
		private readonly IServiceProvider mServiceProvider;
		private readonly Assembly mAssemblyToGetConfigurationFrom;

		public ModelBuildingService(IServiceProvider serviceProvider, Assembly assemblyToGetConfigurationFrom)
		{
			mServiceProvider = serviceProvider;
			mAssemblyToGetConfigurationFrom = assemblyToGetConfigurationFrom;
		}

		public void ConfigureEntitiesMapping(ModelBuilder modelBuilder)
		{
			var configurations = mAssemblyToGetConfigurationFrom.GetTypes()
				.Select(x => x.GetTypeInfo())
				.Where(x => x.BaseType != null && x.BaseType.GetTypeInfo().IsGenericType)
				.Where(x => x.BaseType.GetTypeInfo().GetGenericTypeDefinition() == typeof(EntityTypeConfiguration<>))
				.Select(x => x.AsType());

			foreach (var configurationType in configurations)
			{
				var entityType = configurationType.GetTypeInfo().BaseType.GenericTypeArguments.First();

				var configurationInstance = ActivatorUtilities.CreateInstance(mServiceProvider, configurationType);
				var entityTypeBuilder = GetGenericEntityTypeBuilder(modelBuilder, entityType);

				InvokeMapMethod(configurationInstance, entityTypeBuilder);
			}
		}

		private object GetGenericEntityTypeBuilder(ModelBuilder modelBuilder, Type entityType)
		{
			var entityConfigurationMethod = modelBuilder.GetType()
				.GetMethod(nameof(ModelBuilder.Entity), types: new Type[0])
				.MakeGenericMethod(entityType);

			return entityConfigurationMethod.Invoke(modelBuilder, parameters: new object[0]);
		}

		private void InvokeMapMethod(object configuration, object entityTypeBuilder)
		{
			var mapMethod = configuration.GetType()
				.GetMethod(nameof(EntityTypeConfiguration<Browsers.Browser>.Map));

			mapMethod.Invoke(configuration, parameters: new[] { entityTypeBuilder });
		}
	}
}