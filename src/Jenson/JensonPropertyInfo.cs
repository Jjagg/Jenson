using System;

namespace Jenson
{
    public struct JensonPropertyInfo
    {
        public string Name { get; }
        public string? JsonName { get; }
        public string TypeName { get; }
        public bool IsReadOnly { get; }
        public bool ShouldSerialize { get; }
        public bool ShouldDeserialize { get; }
        public bool CanBeNull { get; }
        public string? ShouldSerializeFunction { get; }
        public int? Order { get; }

        public bool ExplicitJsonName => JsonName != null;

        public bool IsArray => TypeName.EndsWith("[]");

        public string ArrayItemType => IsArray ? TypeName.Substring(0, TypeName.Length - 2) : throw new InvalidOperationException("Property type is not array.");
        public bool IsString => TypeName.Equals("System.String", StringComparison.Ordinal);
        public bool IsBoolean => TypeName.Equals("System.Boolean", StringComparison.Ordinal);
        public bool IsByte => TypeName.Equals("System.Byte", StringComparison.Ordinal);
        public bool IsInt16 => TypeName.Equals("System.Int16", StringComparison.Ordinal);
        public bool IsInt32 => TypeName.Equals("System.Int32", StringComparison.Ordinal);
        public bool IsInt64 => TypeName.Equals("System.Int64", StringComparison.Ordinal);
        public bool IsSByte => TypeName.Equals("System.SByte", StringComparison.Ordinal);
        public bool IsUInt16 => TypeName.Equals("System.UInt16", StringComparison.Ordinal);
        public bool IsUInt32 => TypeName.Equals("System.UInt32", StringComparison.Ordinal);
        public bool IsUInt64 => TypeName.Equals("System.UInt64", StringComparison.Ordinal);
        public bool IsGuid => TypeName.Equals("System.Guid", StringComparison.Ordinal);
        public bool IsSingle => TypeName.Equals("System.Single", StringComparison.Ordinal);
        public bool IsDouble => TypeName.Equals("System.Double", StringComparison.Ordinal);
        public bool IsDecimal => TypeName.Equals("System.Decimal", StringComparison.Ordinal);
        public bool IsDateTime => TypeName.Equals("System.DateTime", StringComparison.Ordinal);
        public bool IsDateTimeOffset => TypeName.Equals("System.DateTimeOffset", StringComparison.Ordinal);

        public JensonPropertyInfo(string name, string? jsonName, string typeName, bool isReadOnly, bool shouldSerialize, bool shouldDeserialize, bool canBeNull, int? order, string? shouldSerializeFunction)
        {
            Name = name;
            JsonName = jsonName;
            TypeName = typeName;
            IsReadOnly = isReadOnly;
            ShouldSerialize = shouldSerialize;
            ShouldDeserialize = shouldDeserialize;
            CanBeNull = canBeNull;
            Order = order;
            ShouldSerializeFunction = shouldSerializeFunction;
        }
    }
}
