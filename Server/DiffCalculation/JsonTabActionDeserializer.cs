using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using RealTimeTabSynchronizer.Server.DiffCalculation.Dto;

namespace RealTimeTabSynchronizer.Server.DiffCalculation
{
	public class JsonTabActionDeserializer : ITabActionDeserializer
	{
		private readonly IDictionary<string, Type> mInputTypeNameToClassMap =
			new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
			{
				{ "CreateTab", typeof(TabCreatedDto) },
				{ "MoveTab", typeof(TabMovedDto) },
				{ "CloseTab", typeof(TabClosedDto) },
				{ "ChangeTabUrl", typeof(TabUrlChangedDto) },
			};

		private readonly JsonSerializer mSerializer;

		public JsonTabActionDeserializer()
		{
			mSerializer = JsonSerializer.Create(new JsonSerializerSettings
			{
				ContractResolver = new ContractResolver()
			});
		}

		public TabAction Deserialize(object singleChange)
		{
			var @object = (JObject)singleChange;

			var changeType = @object.Value<string>("type");
			var destinationDtoType = mInputTypeNameToClassMap[changeType];

			return (TabAction)@object.ToObject(destinationDtoType, mSerializer);
		}

		private class ContractResolver : DefaultContractResolver
		{
			protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
			{
				var property = base.CreateProperty(member, memberSerialization);
				if (property.UnderlyingName == nameof(TabAction.ActionTime))
					property.PropertyName = "dateTime";
				else if (property.UnderlyingName == nameof(TabAction.TabIndex))
					property.PropertyName = "index";
				else if (property.UnderlyingName == nameof(TabAction.ActionId))
					property.PropertyName = "changeId";

				return property;
			}
		}
	}
}