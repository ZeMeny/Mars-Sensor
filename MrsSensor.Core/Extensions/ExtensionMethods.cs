using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;
using SensorStandard;

namespace MrsSensor.Core.Extensions
{
	internal static class ExtensionMethods
	{
		internal static T Copy<T>(this T source) where T : class
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			var type = source.GetType();
			var ctor = type.GetConstructor(new Type[] { });

			var destination = (T)ctor?.Invoke(new object[] { });
			foreach (var property in type.GetProperties())
			{
				var propType = property.PropertyType;
				if (propType.IsPrimitive || propType == typeof(string) || propType.IsEnum || !propType.IsValueType)
				{
					// for enum do not use copy, as its return 'object' instead of enum
					property.SetValue(destination, property.GetValue(source));
				}
				else if (propType.IsArray)
				{
					var oldArr = (Array) property.GetValue(source);
					if (oldArr == null)
					{
						continue;
					}

					var newArr = (Array) property.GetValue(destination);
					if (newArr == null)
					{
						newArr = Array.CreateInstance(
							propType.GetElementType() ?? throw new InvalidOperationException("array element type is null"),
							oldArr.Length);
					}

					Array.Copy(oldArr, newArr, oldArr.Length);
					property.SetValue(destination, newArr);
				}
				// prefer clone
				else if (propType.GetInterfaces().Contains(typeof(ICloneable)))
				{
					property.SetValue(destination, ((ICloneable) property.GetValue(source)).Clone());
				}
				// keep copying until its a primitive type
				else
				{
					var value = property.GetValue(source)?.Copy().CastToReflected(property.PropertyType);
					property.SetValue(destination, value);
				}
			}

			return destination;
		}

		private static T CastTo<T>(this object o) => (T)o;

		private static dynamic CastToReflected(this object o, Type type)
		{
			var methodInfo = typeof(ExtensionMethods).GetMethod(nameof(CastTo), BindingFlags.Static | BindingFlags.Public);
			var genericArguments = new[] { type };
			var genericMethodInfo = methodInfo?.MakeGenericMethod(genericArguments);
			return genericMethodInfo?.Invoke(null, new[] { o });
		}

		public static MrsMessageTypes? GetXmlType(string xml)
		{
			XDocument doc = XDocument.Parse(xml);
			string rootName = doc.Root?.Name.LocalName;
			if (Enum.TryParse(typeof(MrsMessageTypes), rootName, out var type))
			{
				return (MrsMessageTypes?)type;
			}

			return null;
		}

		public static T XmlConvert<T>(string xml)
		{
			XmlSerializer serializer = new XmlSerializer(typeof(T));
			StringReader reader = new StringReader(xml);
			return (T)serializer.Deserialize(reader);
		}
	}
}
