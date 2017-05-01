using System;

namespace RealTimeTabSynchronizer.Server.TabData_
{
	public class TabData
	{
		public TabData()
		{
			LastModificationTime = DateTime.UtcNow;
		}

		private int? mIndex;
		private string mUrl;

		public int Id { get; set; }
		public DateTime LastModificationTime { get; set; }

		public int? Index
		{
			get { return mIndex; }
			set
			{
				mIndex = value;
				LastModificationTime = DateTime.UtcNow;
			}
		}

		public string Url
		{
			get { return mUrl; }
			set
			{
				mUrl = value;
				LastModificationTime = DateTime.UtcNow;
			}
		}

		public bool IsOpen
		{
			get
			{
				return Index != null;
			}
			set
			{
				if (value)
				{
					throw new InvalidOperationException("Tab opening should be done by setting Index property.");
				}

				Index = null;
			}
		}

		public override bool Equals(object obj)
		{
			var otherTabData = obj as TabData;
			if (otherTabData == null)
			{
				return false;
			}

			return Id == otherTabData.Id;
		}

		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}
	}
}