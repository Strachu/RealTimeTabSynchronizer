using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TabDataMapping : EntityTypeConfiguration<TabData>
{
	public override void Map(EntityTypeBuilder<TabData> builder)
	{
		builder.ToTable("Tabs");

		builder.HasKey(x => x.Index);
		builder.Property(x => x.Index).ValueGeneratedNever();
		
		builder.Property(x => x.Url)
			.HasMaxLength(8192) // http://stackoverflow.com/questions/417142/what-is-the-maximum-length-of-a-url-in-different-browsers
			.IsRequired();
	}
}