using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RestSharp
{
    public class ReflectionTypeHandler: ITypeHandler
    {
        private string m_typeFieldName;

        public ReflectionTypeHandler(): this("$type")
        {
        }

        public ReflectionTypeHandler(string typeFieldName)
        {
            m_typeFieldName = typeFieldName;
        }

        public virtual Type GetType(IDictionary<string, object> data)
        {
            if (!data.ContainsKey(m_typeFieldName))
                return null;

            string typeName = Convert.ToString(data[m_typeFieldName]);
            if (string.IsNullOrEmpty(typeName))
                return null;

            return Type.GetType(typeName);
        }

        public virtual void SetType(Type type, IDictionary<string, object> data)
        {
            data[m_typeFieldName] = type.AssemblyQualifiedName;
        }
    }
}
