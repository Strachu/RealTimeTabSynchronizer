using System;

namespace Server.TabData_
{
    public class TabRange
    {
        public TabRange(int fromIndexInclusive, int toIndexInclusive = Int32.MaxValue)
        {
            FromIndexInclusive = fromIndexInclusive;
            ToIndexInclusive = toIndexInclusive;
        }

        public int FromIndexInclusive { get; }
        public int ToIndexInclusive { get; }
    }
}