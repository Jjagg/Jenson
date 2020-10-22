using System;

namespace Jenson.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class JensonPropertyAttribute : Attribute
    {
        public int Order { get; set; }
        public string ShouldSerializeFunction { get; set; }
    }
}
