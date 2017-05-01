using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RealTimeTabSynchronizer.Server.Tabs.Browsers
{
	public class BrowserTabMapping : EntityTypeConfiguration<BrowserTab>
	{
		public override void Map(EntityTypeBuilder<BrowserTab> builder)
		{
			builder.HasKey(x => x.Id);

			builder.HasAlternateKey(x => new { x.BrowserId, x.BrowserTabId });
			builder.HasOne(x => x.ServerTab)
				.WithMany()
				.HasForeignKey(x => x.ServerTabId)
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		}
	}
}