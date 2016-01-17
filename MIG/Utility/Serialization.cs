using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System.Globalization;
using Newtonsoft.Json.Linq;

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
            settings.Converters.Add(new FormattedDecimalConverter(CultureInfo.InvariantCulture));
            return JsonConvert.SerializeObject(data, settings);
        }

        // Work around for "Input string was not in the correct format" when running on some mono-arm platforms
        public class FormattedDecimalConverter : JsonConverter
        {
            private CultureInfo culture;

            public FormattedDecimalConverter(CultureInfo culture)
            {
                this.culture = culture;
            }

            public override bool CanConvert(Type objectType)
            {
                return (objectType == typeof(decimal) ||
                objectType == typeof(double) ||
                objectType == typeof(float));
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteRawValue(Convert.ToString(value, culture));
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JToken token = JToken.Load(reader);
                if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
                {
                    return token.ToObject<decimal>();
                }
                if (token.Type == JTokenType.String)
                {
                    // customize this to suit your needs
                    return Decimal.Parse(token.ToString(), this.culture);
                }
                if (token.Type == JTokenType.Null && objectType == typeof(decimal?))
                {
                    return null;
                }
                throw new JsonSerializationException("Unexpected token type: " + 
                    token.Type.ToString());
            }
        }

        public class CustomResolver : DefaultContractResolver
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

