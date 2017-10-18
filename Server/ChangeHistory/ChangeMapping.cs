using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RealTimeTabSynchronizer.Server.ChangeHistory
{
	public class ChangeMapping : EntityTypeConfiguration<Change>
	{
		public override void Map(EntityTypeBuilder<Change> builder)
		{
			builder.ToTable("ProcessedChanges");

			builder.HasKey(x => new { x.BrowserId, x.Id });
		}
	}
}