using System;

namespace Jenson.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class JensonTypeDiscriminatorAttribute : Attribute
    {
        public string Property { get; }
        public string DiscriminatorFunction { get; }

        public JensonTypeDiscriminatorAttribute(string property, string discriminatorFunction)
        {
            Property = property;
            DiscriminatorFunction = discriminatorFunction;
        }
    }
}
