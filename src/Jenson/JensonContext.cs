using Microsoft.CodeAnalysis;

namespace Jenson
{
    public class JensonContext
    {
        public SourceGeneratorContext GeneratorContext { get; }
        public INamedTypeSymbol JensonSerializeAttribute { get; }
        public INamedTypeSymbol JensonPropertyAttribute { get; }
        public INamedTypeSymbol JensonTypeDiscriminatorAttribute { get; }
        public INamedTypeSymbol JsonPropertyNameAttribute { get; }
        public INamedTypeSymbol JsonIgnoreAttribute { get; }

        public JensonContext(
            SourceGeneratorContext generatorContext,
            INamedTypeSymbol jensonSerializeAttribute,
            INamedTypeSymbol jensonPropertyAttribute,
            INamedTypeSymbol jensonTypeDiscriminatorAttribute,
            INamedTypeSymbol jsonPropertyNameAttribute,
            INamedTypeSymbol jsonIgnoreAttribute)
        {
            GeneratorContext = generatorContext;
            JensonSerializeAttribute = jensonSerializeAttribute;
            JensonPropertyAttribute = jensonPropertyAttribute;
            JensonTypeDiscriminatorAttribute = jensonTypeDiscriminatorAttribute;
            JsonPropertyNameAttribute = jsonPropertyNameAttribute;
            JsonIgnoreAttribute = jsonIgnoreAttribute;
        }
    }
}
