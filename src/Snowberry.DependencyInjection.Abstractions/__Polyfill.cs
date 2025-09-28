using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if NETSTANDARD2_0
namespace System.Diagnostics.CodeAnalysis
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
    internal sealed class DoesNotReturnAttribute : System.Attribute { }
}
#endif