using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Represents caching information for an operation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RpcCachingAttribute : Attribute
    {
    }
}
