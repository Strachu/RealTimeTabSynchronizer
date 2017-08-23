using RealTimeTabSynchronizer.Server.DiffCalculation.Dto;

namespace RealTimeTabSynchronizer.Server.DiffCalculation
{
	public interface ITabActionDeserializer
	{
		TabAction Deserialize(object singleChange);
	}
}