using System;
using System.Text.Json;
using Xunit;
using Jenson;
using Jenson.Attributes;

namespace Jenson.Test
{
    public class BasicTest
    {
        [Fact]
        public void Test1()
        {
            var c = new BasicClass { Name = "Jesse", Number = 13 };
            var json = JsonSerializer.Serialize(c);
            var cr = JsonSerializer.Deserialize<BasicClass>(json);
            Assert.Equal(c.Name, cr.Name);
            Assert.Equal(c.Number, cr.Number);
        }

        [Fact]
        public void Discriminator()
        {
            var a = new A();
            var b = new B();
            var jsonA = JsonSerializer.Serialize(a);
            var jsonB = JsonSerializer.Serialize(b);

            var ar = JsonSerializer.Deserialize<BaseType>(jsonA);
            var br = JsonSerializer.Deserialize<BaseType>(jsonB);

            Assert.IsType<A>(ar);
            Assert.IsType<B>(br);
        }
    }

    [JensonSerialize]
    public partial class BasicClass
    {
        public string Name { get; init; }
        public int Number { get; init;}
    }

    [JensonSerialize]
    public partial record BasicRecord
    {
        // TODO positional records
        public string Name { get; init; }
        public int Number { get; init; }
    }

    [JensonSerialize]
    public partial struct BasicStruct
    {
        public string Name { get; init; }
        public int Number { get; init;}
    }

    //[JensonTypeDiscriminator(nameof(Type), nameof(Discriminator))]
    [JensonSerialize]
    [JensonTypeDiscriminator(nameof(Type), nameof(Discriminator))]
    public abstract partial class BaseType
    {
        public abstract int Type { get; }

        public static Type Discriminator(int value)
            => value == 0 ? typeof(A) : typeof(B);
    }

    [JensonSerialize]
    public partial class A : BaseType
    {
        public override int Type => 0;
    }

    [JensonSerialize]
    public partial class B : BaseType
    {
        public override int Type => 1;
    }
}
