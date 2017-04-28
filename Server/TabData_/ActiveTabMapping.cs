using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Server.TabData_;

public class ActiveTabMapping : EntityTypeConfiguration<ActiveTab>
{
	public override void Map(EntityTypeBuilder<ActiveTab> builder)
	{
		builder.HasKey(x => x.Id);

		builder.HasOne(x => x.Tab)
			.WithOne()
			.IsRequired();
	}
}