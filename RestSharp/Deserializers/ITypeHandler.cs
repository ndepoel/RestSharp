using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RestSharp
{
    public interface ITypeHandler
    {
        Type GetType(IDictionary<string, object> data);
        void SetType(Type type, IDictionary<string, object> data);
    }
}
