using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RestSharp
{
    public class DefaultTypeHandler: ITypeHandler
    {
        protected string TypeFieldName { get; private set; }
        protected Dictionary<string, Type> TypeMap { get; private set; }

        public DefaultTypeHandler(): this("$type", new Dictionary<string,Type>())
        {
        }

        public DefaultTypeHandler(string typeFieldName, Dictionary<string, Type> typeMap)
        {
            TypeFieldName = typeFieldName;
            TypeMap = typeMap;
        }

        public virtual Type GetType(IDictionary<string, object> data)
        {
            if (!data.ContainsKey(TypeFieldName))
                return null;

            string typeName = Convert.ToString(data[TypeFieldName]);
            if (string.IsNullOrEmpty(typeName))
                return null;

            if (!TypeMap.ContainsKey(typeName))
                return null;

            return TypeMap[typeName];
        }

        public virtual void SetType(Type type, IDictionary<string, object> data)
        {
            string typeName = TypeMap.Where(tm => tm.Value == type).Select(tm => tm.Key).FirstOrDefault();
            if (typeName == null)
                return;

            data[TypeFieldName] = typeName;
        }
    }
}
