using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RealTimeTabSynchronizer.Server.Browsers
{
	public class BrowserMapping : EntityTypeConfiguration<Browser>
	{
		public override void Map(EntityTypeBuilder<Browser> builder)
		{
			builder.ToTable("Browsers");

			builder.HasKey(x => x.Id);

			builder.Property(x => x.Name)
				.HasMaxLength(1024)
				.IsRequired();
		}
	}
}