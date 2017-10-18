using System;
using System.Collections.Generic;
using RealTimeTabSynchronizer.Server.DiffCalculation.Dto;

namespace RealTimeTabSynchronizer.Server.ChangeHistory
{
	public interface IChangeHistoryService
	{
		IEnumerable<TabAction> FilterOutAlreadyProcessedChanges(Guid browserId, IEnumerable<TabAction> allChanges);
	}
}