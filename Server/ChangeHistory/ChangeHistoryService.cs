using System;
using System.Collections.Generic;
using System.Linq;
using RealTimeTabSynchronizer.Server.DiffCalculation.Dto;
using RealTimeTabSynchronizer.Server.EntityFramework;

namespace RealTimeTabSynchronizer.Server.ChangeHistory
{
	public class ChangeHistoryService : IChangeHistoryService
	{
		private readonly TabSynchronizerDbContext mDbContext;

		public ChangeHistoryService(TabSynchronizerDbContext dbContext)
		{
			mDbContext = dbContext;
		}

		public IEnumerable<TabAction> FilterOutAlreadyProcessedChanges(Guid browserId, IEnumerable<TabAction> allChanges)
		{
			var processedIds = allChanges.Select(x => x.ActionId).ToList();

			var browserChanges = mDbContext.ProcessedChanges.Where(x => x.BrowserId == browserId);
			var alreadyProcessedChanges = browserChanges.Where(x => processedIds.Contains(x.Id)).Select(x => x.Id).ToList();

			var notProcessedChanges = allChanges.Where(x => alreadyProcessedChanges.All(y => y != x.ActionId));

			MarkChangesAsAlreadyProcessed(browserId, notProcessedChanges);

			return notProcessedChanges;
		}

		private void MarkChangesAsAlreadyProcessed(Guid browserId, IEnumerable<TabAction> processedChanges)
		{
			var changes = processedChanges.Select(x => new Change
			{
				BrowserId = browserId,
				Id = x.ActionId
			});

			mDbContext.ProcessedChanges.AddRange(changes);
		}
	}
}