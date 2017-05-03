using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RealTimeTabSynchronizer.Server.Acknowledgments
{
	public class PendingRequestMapping : EntityTypeConfiguration<PendingRequest>
	{
		public override void Map(EntityTypeBuilder<PendingRequest> builder)
		{
			builder.HasKey(x => x.Id);

			builder.Property(x => x.SerializedData).IsRequired();
		}
	}
}