using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace MIG.Utility
{
    public static class Serialization
    {
        public static string JsonSerialize(object data, bool indent = false)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ContractResolver = new CustomResolver();
            if (indent)
                settings.Formatting = Formatting.Indented;
            return JsonConvert.SerializeObject(data, settings);
        }

        class CustomResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);

                property.ShouldSerialize = instance =>
                {
                    try
                    {
                        PropertyInfo prop = (PropertyInfo)member;
                        if (prop.CanRead)
                        {
                            prop.GetValue(instance, null);
                            return true;
                        }
                    }
                    catch
                    {
                    }
                    return false;
                };

                return property;
            }
        }

    }
}

