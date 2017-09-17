using System.Collections.Generic;
using System.Linq;
using RealTimeTabSynchronizer.Server.DiffCalculation.Dto;

namespace RealTimeTabSynchronizer.Server.DiffCalculation
{
    public class IndexCalculator : IIndexCalculator
    {
        public int? GetTabIndexAfterChanges(int currentIndex, IEnumerable<TabAction> changes)
        {
            var newIndex = currentIndex;

            foreach (var change in changes)
            {
                switch (change)
                {
                    case TabCreatedDto dto:
                        if (dto.TabIndex <= newIndex)
                        {
                            newIndex++;
                        }
                        break;

                    case TabClosedDto dto:
                        if (dto.TabIndex == newIndex)
                        {
                            return null;
                        }

                        if (dto.TabIndex < newIndex)
                        {
                            newIndex--;
                        }
                        break;

                    case TabMovedDto dto:
                        if (dto.TabIndex == newIndex)
                        {
                            newIndex = dto.NewIndex;
                        }
                        else if (dto.TabIndex > newIndex && dto.NewIndex <= newIndex)
                        {
                            newIndex++;
                        }
                        else if (dto.TabIndex < newIndex && dto.NewIndex >= newIndex)
                        {
                            newIndex--;
                        }
                        break;
                }
            }

            return newIndex;
        }

        // TODO Refactor -> very similar to GetTabIndexAfterChanges
        public int? GetTabIndexBeforeChanges(int newIndex, IEnumerable<TabAction> changes)
        {
            var oldIndex = newIndex;

            foreach (var change in changes.Reverse())
            {
                switch (change)
                {
                    case TabCreatedDto dto:
                        if (dto.TabIndex == oldIndex)
                        {
                            return null;
                        }

                        if (dto.TabIndex < oldIndex)
                        {
                            oldIndex--;
                        }
                        break;

                    case TabClosedDto dto:
                        if (dto.TabIndex <= oldIndex)
                        {
                            oldIndex++;
                        }
                        break;

                    case TabMovedDto dto:
                        if (dto.NewIndex == oldIndex)
                        {
                            oldIndex = dto.TabIndex;
                        }
                        else if (dto.TabIndex > oldIndex && dto.NewIndex <= oldIndex)
                        {
                            oldIndex--;
                        }
                        else if (dto.TabIndex < oldIndex && dto.NewIndex > oldIndex)
                        {
                            oldIndex++;
                        }
                        break;
                }
            }

            return newIndex;
        }
    }
}