using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.EntityFrameworkCore
{
	// https://github.com/aspnet/EntityFramework/issues/2805
	public abstract class EntityTypeConfiguration<TEntity>
		 where TEntity : class
	{
		public abstract void Map(EntityTypeBuilder<TEntity> builder);
	}
}