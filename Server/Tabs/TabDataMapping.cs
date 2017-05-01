using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RealTimeTabSynchronizer.Server.TabData_
{
	public class TabDataMapping : EntityTypeConfiguration<TabData>
	{
		public override void Map(EntityTypeBuilder<TabData> builder)
		{
			builder.ToTable("TabData");

			builder.HasKey(x => x.Id);

			builder.HasAlternateKey(x => x.Index);
			builder.Property(x => x.Index).HasField("m" + nameof(TabData.Index));
			builder.Property(x => x.Url)
				.HasField("m" + nameof(TabData.Url))
				.HasMaxLength(8192) // http://stackoverflow.com/questions/417142/what-is-the-maximum-length-of-a-url-in-different-browsers
				.IsRequired();

			builder.Ignore(x => x.IsOpen);
		}
	}
}