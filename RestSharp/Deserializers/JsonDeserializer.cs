using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RestSharp.Extensions;

namespace RestSharp.Deserializers
{
	public class JsonDeserializer : IDeserializer
	{
		public string RootElement { get; set; }
		public string Namespace { get; set; }
		public string DateFormat { get; set; }
		public CultureInfo Culture { get; set; }

		public JsonDeserializer()
		{
			Culture = CultureInfo.InvariantCulture;
		}

		private Dictionary<Type, ITypeHandler> _typeHandlers = new Dictionary<Type, ITypeHandler>();

		public void AddTypeHandler(Type type, ITypeHandler handler)
		{
			_typeHandlers[type] = handler;
		}

		public T Deserialize<T>(IRestResponse response)
		{
			var target = Activator.CreateInstance<T>();

			if (target is IList)
			{
				var objType = target.GetType();

				if (RootElement.HasValue())
				{
					var root = FindRoot(response.Content);
					target = (T)BuildList(objType, root);
				}
				else
				{
					var data = SimpleJson.DeserializeObject(response.Content);
					target = (T)BuildList(objType, data);
				}
			}
			else if (target is IDictionary)
			{
				var root = FindRoot(response.Content);
				target = (T)BuildDictionary(target.GetType(), root);
			}
			else
			{
				var root = FindRoot(response.Content);
				Map(target, (IDictionary<string, object>)root);
			}

			return target;
		}

		private object FindRoot(string content)
		{
			var data = (IDictionary<string, object>)SimpleJson.DeserializeObject(content);
			if (RootElement.HasValue() && data.ContainsKey(RootElement))
			{
				return data[RootElement];
			}
			return data;
		}

		private void Map(object target, IDictionary<string, object> data)
		{
			var objType = target.GetType();
			var props = objType.GetProperties().Where(p => p.CanWrite).ToList();

			foreach (var prop in props)
			{
				var type = prop.PropertyType;

				var name = prop.Name;
				var actualName = name.GetNameVariants(Culture).FirstOrDefault(n => data.ContainsKey(n));
				var value = actualName != null ? data[actualName] : null;

				if (value == null) continue;

				// check for nullable and extract underlying type
				if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
				{
					type = type.GetGenericArguments()[0];
				}

				prop.SetValue(target, ConvertValue(type, value), null);
			}
		}

		private IDictionary BuildDictionary(Type type, object parent)
		{
			var dict = (IDictionary)Activator.CreateInstance(type);
			var valueType = type.GetGenericArguments()[1];

			if (parent is IDictionary<string, object>)
			{
				foreach (var child in (IDictionary<string, object>)parent)
				{
					var key = child.Key;
					var item = ConvertValue(valueType, child.Value);
					dict.Add(key, item);
				}
			}

			return dict;
		}

		private IList BuildList(Type type, object parent)
		{
			var list = (IList)Activator.CreateInstance(type);
			var listType = type.GetInterfaces().First(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IList<>));
			var itemType = listType.GetGenericArguments()[0];
   
			if (parent is IList)
			{
                var parentList = (IList)parent;
                for (int idx = 0; idx < parentList.Count; ++idx)
				{
                    var element = parentList[idx];

					if (itemType.IsPrimitive)
					{
						var value = element.ToString();
						list.Add(value.ChangeType(itemType, Culture));
					}
					else if (itemType == typeof(string))
					{
						if (element == null)
						{
							list.Add(null);
							continue;
						}

						list.Add(element.ToString());
					}
					else
					{
						if (element == null)
						{
							list.Add(null);
							continue;
						}

						var item = ConvertValue(itemType, element);
						list.Add(item);
					}
				}
			}

			return list;
		}
  
        private string Stringify(object value)
        {
            return Convert.ToString(value, Culture);
        }
        
		private object ConvertValue(Type type, object value)
		{
			if (type.IsPrimitive)
			{
				// no primitives can contain quotes so we can safely remove them
				// allows converting a json value like {"index": "1"} to an int
				var tmpVal = Stringify(value).Replace("\"", string.Empty);

				return tmpVal.ChangeType(type, Culture);
			}
			else if (type.IsEnum)
			{
				return type.FindEnumValue(Stringify(value), Culture);
			}
			else if (type == typeof(Uri))
			{
				return new Uri(Stringify(value), UriKind.RelativeOrAbsolute);
			}
			else if (type == typeof(string))
			{
				return Stringify(value);
			}
			else if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
			{
				DateTime dt;
				if (DateFormat.HasValue())
				{
					dt = DateTime.ParseExact(Stringify(value), DateFormat, Culture);
				}
				else
				{
					// try parsing instead
					dt = Stringify(value).ParseJsonDate(Culture);
				}

				if (type == typeof(DateTime))
				{
					return dt;
				}
				else if (type == typeof(DateTimeOffset))
				{
					return (DateTimeOffset)dt;
				}
			}
			else if (type == typeof(Decimal))
			{
				return Decimal.Parse(Stringify(value), Culture);
			}
			else if (type == typeof(Guid))
			{
				return string.IsNullOrEmpty(Stringify(value)) ? Guid.Empty : new Guid(Stringify(value));
			}
			else if (type == typeof(TimeSpan))
			{
				return TimeSpan.Parse(Stringify(value));
			}
			else if (type.IsGenericType)
			{
				var genericTypeDef = type.GetGenericTypeDefinition();
				if (genericTypeDef == typeof(List<>))
				{
					return BuildList(type, value);
				}
				else if (genericTypeDef == typeof(Dictionary<,>))
				{
					var keyType = type.GetGenericArguments()[0];

					// only supports Dict<string, T>()
					if (keyType == typeof(string))
					{
						return BuildDictionary(type, value);
					}
				}
				else
				{
					// nested property classes
					return CreateAndMap(type, value);
				}
			}
			else
			{
				// nested property classes
				return CreateAndMap(type, value);
			}

			return null;
		}

		private object CreateAndMap(Type type, object element)
		{
			var data = (IDictionary<string, object>)element;

			if (_typeHandlers.ContainsKey(type))
			{
				type = _typeHandlers[type].GetType(data) ?? type;
			}

			var instance = Activator.CreateInstance(type);

			Map(instance, data);

			return instance;
		}
	}
}