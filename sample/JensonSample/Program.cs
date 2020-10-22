using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jenson.Attributes;

namespace JensonSample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var empl = new Employee
            {
                FirstName = "Jesse",
                LastName = "Gielen"
            };

            var json = JsonSerializer.Serialize(empl);
            var emplr = JsonSerializer.Deserialize<Employee>(json);

            Debug.Assert(emplr.FirstName.Equals(empl.FirstName));

            var a = new A();
            var b = new B();
            var jsonA = JsonSerializer.Serialize(a);
            var jsonB = JsonSerializer.Serialize(b);

            var ar = JsonSerializer.Deserialize<BaseType>(jsonA);
            var br = JsonSerializer.Deserialize<BaseType>(jsonB);

            Debug.Assert(ar is A);
            Debug.Assert(br is B);
        }
    }

    [JensonSerialize]
    public partial class Employee
    {
        public string FirstName { get; init; }
        public string LastName { get; init; }
    }

    public abstract partial class OrgBase
    {
        [JensonProperty(Order = -1)]
        public int Id { get; init; }
    }

    [JensonSerialize]
    public partial class Organization : OrgBase
    {
        [JsonPropertyName("companyName")]
        public string Name { get; init; }
        [JsonIgnore]
        public bool Ignored { get; init; }
        public int Numbers { get; init; }
        [JensonProperty(ShouldSerializeFunction = nameof(ShouldWriteMoreNumbers))]
        public Int32 MoreNumbers { get; init; }
        public bool ShouldWriteMoreNumbers() => false;
        public Employee[] Employees { get; init; }
    }

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
