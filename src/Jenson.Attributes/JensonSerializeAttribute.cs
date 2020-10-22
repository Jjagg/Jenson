using System;

namespace Jenson.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class JensonSerializeAttribute : Attribute
    {
    }
}
