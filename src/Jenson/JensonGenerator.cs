using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Jenson
{
    [Generator]
    public class JensonGenerator : ISourceGenerator
    {
        private const string DiagnosticId = nameof(JensonGenerator);

        private static readonly DiagnosticDescriptor MissingPartialModifier =
            new DiagnosticDescriptor(
                DiagnosticId,
                "Declarations for Jenson serializable types should be marked partial",
                "Make '{0}' partial", "Jensen",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

        public void Initialize(InitializationContext context)
        {
            return;

            var messageInterval = TimeSpan.FromSeconds(5);
            var message = "Waiting for debugger. [{0}]";
            var delay = TimeSpan.FromMilliseconds(200);
            WaitForDebugger(messageInterval, message, delay).GetAwaiter().GetResult();
        }

        public void Execute(SourceGeneratorContext generatorContext)
        {
            var ct = generatorContext.CancellationToken;
            var compilation = generatorContext.Compilation;

            var jensonSerializeAttribute = compilation.GetTypeByMetadataName("Jenson.Attributes.JensonSerializeAttribute");
            var jensonPropertyAttribute = compilation.GetTypeByMetadataName("Jenson.Attributes.JensonPropertyAttribute");
            var jensonTypeDiscriminatorAttribute = compilation.GetTypeByMetadataName("Jenson.Attributes.JensonTypeDiscriminatorAttribute");
            var jsonPropertyNameAttribute = compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonPropertyNameAttribute");
            var jsonIgnoreAttribute = compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonIgnoreAttribute");

            if (jensonSerializeAttribute is null) throw new Exception("Missing JensonSerializeAttribute in compilation.");
            if (jensonPropertyAttribute is null) throw new Exception("Missing JensonPropertyAttribute in compilation.");
            if (jensonTypeDiscriminatorAttribute is null) throw new Exception("Missing JensonTypeDiscriminatorAttribute in compilation.");
            if (jsonPropertyNameAttribute is null) throw new Exception("Missing JsonPropertyName in compilation.");
            if (jsonIgnoreAttribute is null) throw new Exception("Missing JsonIgnore in compilation.");

            var context = new JensonContext(
                generatorContext,
                jensonSerializeAttribute,
                jensonPropertyAttribute,
                jensonTypeDiscriminatorAttribute,
                jsonPropertyNameAttribute,
                jsonIgnoreAttribute);

            var rootTypes = compilation.GlobalNamespace.GetAllTypes(ct)
                .Where(t => t.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, jensonSerializeAttribute)));

            ct.ThrowIfCancellationRequested();

            var sb = new StringBuilder();

            foreach (var t in rootTypes)
            {
                var typeDeclarations = t.DeclaringSyntaxReferences.Select(sr =>
                {
                    return (TypeDeclarationSyntax)sr.GetSyntax();
                });

                var isPartial = typeDeclarations.Any(td => td.Modifiers.Any(SyntaxKind.PartialKeyword));

                if (!isPartial)
                {
                    foreach (var td in typeDeclarations)
                    {
                        var diagnostic = Diagnostic.Create(MissingPartialModifier, td.GetLocation(), new object[] { t.Name });
                        generatorContext.ReportDiagnostic(diagnostic);
                    }
                }

                var w = new SourceWriter();
                GenerateTypeSerializer(context, w, t);

                generatorContext.AddSource($"{t.Name}.jenson", SourceText.From(w.ToString(), Encoding.UTF8));

                // for debugging
                sb.Append(w.ToString()); sb.AppendLine(); sb.AppendLine();
            }

            // for debugging
            File.WriteAllText("C:\\Users\\jesse\\Desktop\\converter.cs", sb.ToString());
        }

        private void GenerateTypeSerializer(JensonContext context, SourceWriter w, INamedTypeSymbol t)
        {
            AppendImports(w);

            w.Line();

            var ns = t.ContainingNamespace;
            var nsString = ns.Name;
            ns = ns.ContainingNamespace;
            while (!ns.IsGlobalNamespace)
            {
                nsString = ns.Name + "." + nsString;
                ns = ns.ContainingNamespace;
            }

            w.Line($"namespace {nsString}");
            w.Line("{");
            w.Indent();

            var typeName = t.Name;
            var declKeyword = t.TypeKind == TypeKind.Class ? (t.IsRecord() ? "record" : "class") : "struct";

            w.Line($"[JsonConverter(typeof({typeName}Converter))]");
            w.Line($"public partial {declKeyword} {typeName} {{ }}");

            var propertiesInfo = ExtractPropertyInfo(context, t, typeName);

            w.Line($"public class {typeName}Converter : JsonConverter<{typeName}>");
            w.Line("{");
            w.Indent();

            var typeDiscriminatorAttr = t.GetAttributes()
                .FirstOrDefault(attr =>
                    SymbolEqualityComparer.Default.Equals(
                        attr.AttributeClass,
                        context.JensonTypeDiscriminatorAttribute));

            if (typeDiscriminatorAttr is not null)
            {
                var discrPropName = (string) typeDiscriminatorAttr.ConstructorArguments[0].Value;
                var discrFun = (string) typeDiscriminatorAttr.ConstructorArguments[1].Value;

                var discrProp = propertiesInfo.First(p => p.Name.Equals(discrPropName));

                WriteTypeDiscriminatorConverterBody(w, typeName, discrProp, discrFun);
            }
            else
            {
                foreach (var p in propertiesInfo)
                {
                    // TODO When property naming policy is set this won't necessarily match
                    // UTF-8 json variable names. We should take JsonSerializerOptions into account.
                    // Relevant props:
                    // - PropertyNameCaseInsensitive
                    // - PropertyNamingPolicy

                    // TODO use utf8 string literals when available
                    w.Line($"private static readonly byte[] _{p.Name}Name = Encoding.UTF8.GetBytes(\"{p.JsonName ?? p.Name}\");");
                }

                w.Line();
                AppendRead(w, typeName, propertiesInfo);

                w.Line();
                AppendWrite(w, typeName, propertiesInfo);
            }

            w.Dedent();
            w.Line("}"); // class
            w.Dedent();
            w.Line("}"); // namespace

            Debug.Assert(w.Indentation == 0);
        }

        private void WriteTypeDiscriminatorConverterBody(SourceWriter w, string baseType, JensonPropertyInfo p, string discrFun)
        {
            w.Line($"public override {baseType} Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
            w.Line("{");

            w.Indent();

            w.Line("var discrReader = reader;");
            w.Line($"var discriminatorName = Encoding.UTF8.GetBytes(\"{p.Name}\");");
            w.Line();

            w.Line("if (discrReader.TokenType != JsonTokenType.StartObject) throw new JsonException(\"Expected start of object.\");");
            w.Line();

            w.Line("while (discrReader.Read())");
            w.Line("{");
            w.Indent();
            w.Line("if (discrReader.ValueTextEquals(discriminatorName))");
            w.Line("{");
            w.Indent();
            w.Line("if (!discrReader.Read()) throw new JsonException();");
            // TODO proper deserialization of value
            w.Line($"var value = JsonSerializer.Deserialize<{p.TypeName}>(ref discrReader);");
            w.Line($"var realType = {baseType}.{discrFun}(value);");
            w.Line($"return ({baseType}) JsonSerializer.Deserialize(ref reader, realType, options);");
            w.Dedent();
            w.Line("}");

            w.Line();
            w.Line("discrReader.Skip();");

            w.Dedent();
            w.Line("}");

            w.Line();

            w.Line("throw new JsonException(\"Missing type discriminator.\");");

            w.Dedent();
            w.Line("}");

            w.Line();

            w.Line($"public override void Write(Utf8JsonWriter writer, {baseType} value, JsonSerializerOptions options)");
            w.Line("{");
            w.Indent();

            w.Line("throw new NotImplementedException(\"A converter for subclasses must always be used for writing.\");");

            w.Dedent();
            w.Line("}");
        }

        private static void AppendImports(SourceWriter w)
        {
            w.Line("using System;");
            w.Line("using System.Text;");
            w.Line("using System.Text.Json;");
            w.Line("using System.Text.Json.Serialization;");
        }

        private static List<JensonPropertyInfo> ExtractPropertyInfo(JensonContext context, INamedTypeSymbol t, string typeName)
        {
            // TODO need better filtering to avoid duplicate properties (e.g. virtual)
            // Actually we need to fold property overrides into parent props so
            // users can override attributes in inherited classes.
            // E.g. JsonIgnore on A.Foo, but JsonInclude on B.Foo where B : A
            var properties = t.GetThisAndBaseTypes()
                .Reverse() // Reverse so we handle base types first (for merging)
                .SelectMany(t =>
                    t.GetMembers()
                     .Where(m => m.Kind == SymbolKind.Property && !m.IsImplicitlyDeclared)
                     .Select(p => (IPropertySymbol)p))
                     .OrderBy(p => p.Name);
            
            var propertiesInfo = GetPropertyInfoFromSymbols(context, properties);

            // Use OrderBy for stable sort
            return propertiesInfo.OrderBy(p => p.Order ?? 0).ToList();
        }

        private static IEnumerable<JensonPropertyInfo> GetPropertyInfoFromSymbols(JensonContext context, IEnumerable<IPropertySymbol> properties)
        {
            using var en = properties.GetEnumerator();

            var propsLeft = en.MoveNext();
            while (propsLeft)
            {
                var propName = en.Current.Name;
                var propInfo = CreatePropertyInfo(context, en.Current);

                propsLeft = en.MoveNext();

                while (propsLeft && propName.Equals(en.Current.Name) && en.Current.IsOverride)
                {
                    var additionalInfo = CreatePropertyInfo(context, en.Current);
                    propInfo = MergePropertyInfo(propInfo, additionalInfo);
                    propsLeft = en.MoveNext();
                }

                yield return propInfo;
            }
        }

        private static JensonPropertyInfo CreatePropertyInfo(JensonContext context, IPropertySymbol prop)
        {
            var name = prop.Name;
            string? jsonName = null;
            int? order = null;
            string? shouldSerializeFunction = null;

            var canBeNull = !prop.Type.IsValueType;

            var isReadOnly = prop.IsReadOnly;
            var shouldSerialize = !prop.IsWriteOnly
                && prop.GetMethod!.DeclaredAccessibility != Accessibility.Private
                && prop.GetMethod.DeclaredAccessibility != Accessibility.Protected;
            var shouldDeserialize = !prop.IsReadOnly
                && prop.SetMethod!.DeclaredAccessibility != Accessibility.Private
                && prop.SetMethod.DeclaredAccessibility != Accessibility.Protected;

            var typeFormat = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);
            var propType = prop.Type.ToDisplayString(typeFormat);

            foreach (var attr in prop.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, context.JsonIgnoreAttribute))
                {
                    shouldSerialize = false;
                    shouldDeserialize = false;
                }
                else if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, context.JsonPropertyNameAttribute))
                {
                    jsonName = (string)attr.ConstructorArguments[0].Value!;
                }
                else if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, context.JensonPropertyAttribute))
                {
                    attr.TryGetProperty("Order", out order);
                    attr.TryGetProperty("ShouldSerializeFunction", out shouldSerializeFunction);
                }
            }

            return new JensonPropertyInfo(
                name,
                jsonName,
                propType,
                isReadOnly,
                shouldSerialize,
                shouldDeserialize,
                canBeNull,
                order,
                shouldSerializeFunction);
        }

        private static JensonPropertyInfo MergePropertyInfo(JensonPropertyInfo baseProp, JensonPropertyInfo overwrite)
            => new JensonPropertyInfo(
                baseProp.Name,
                overwrite.JsonName ?? baseProp.Name,
                overwrite.TypeName,
                overwrite.IsReadOnly,
                overwrite.ShouldSerialize && baseProp.ShouldSerialize, // TODO handle JsonInclude
                overwrite.ShouldDeserialize && baseProp.ShouldDeserialize,
                baseProp.CanBeNull,
                overwrite.Order ?? baseProp.Order,
                overwrite.ShouldSerializeFunction ?? baseProp.ShouldSerializeFunction);

        private void AppendRead(SourceWriter w, string typeName, List<JensonPropertyInfo> propertiesInfo)
        {
            // READ
            // Relevant options:
            // - IgnoreNullValues
            // - ReadCommentHandling
            // - AllowTrailingComments

            w.Line($"public override {typeName} Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
            w.Line("{");

            w.Indent();

            w.Line("if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException(\"Expected start of object.\");");
            w.Line();

            foreach (var p in propertiesInfo.Where(p => p.ShouldDeserialize))
            {
                // variables to hold content
                w.Line($"{p.TypeName} {p.Name} = default;");
            }

            w.Line();

            w.Line("while (reader.Read())");
            w.Line("{");
            w.Indent();

            w.Line("if (reader.TokenType == JsonTokenType.EndObject)");
            w.Line("{");
            w.Indent();

            // Construct and return the read object
            w.Line($"return new {typeName}");
            w.Line("{");
            w.Indent();
            foreach (var p in propertiesInfo.Where(p => p.ShouldDeserialize))
                w.Line($"{p.Name} = {p.Name},");

            w.Dedent();
            w.Line("};");

            w.Dedent();
            w.Line("}");

            w.Line();

            w.Line("if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException($\"Expected property name, but got {reader.TokenType}.\");");
            w.Line();

            var firstBranchHandled = false;

            for (var i = 0; i < propertiesInfo.Count; i++)
            {
                var p = propertiesInfo[i];

                if (!p.ShouldDeserialize) continue;

                // Check against properties
                if (firstBranchHandled) w.Write($"else ");
                firstBranchHandled = true;

                // TODO options.PropertyNameCaseInsensitive
                // TODO options.PropertyNamingPolicy

                w.Line($"if (reader.ValueTextEquals(_{p.Name}Name))");
                w.Line("{");
                w.Indent();

                var tryReadValue = p switch
                {
                    //_ when p.IsArray => GenerateReadArray(p),
                    _ when p.IsString => $"if (!reader.Read()) throw new JsonException();\n{p.Name} = reader.GetString();",
                    _ when p.IsBoolean => GenerateReadBoolean(p),
                    _ when p.IsByte => $"if (!reader.Read() || !reader.TryGetByte(out {p.Name})) throw new JsonException();",
                    _ when p.IsDateTime => $"if (!reader.Read() || !reader.TryGetDateTime(out {p.Name})) throw new JsonException();",
                    _ when p.IsDateTimeOffset => $"if (!reader.Read() || !reader.TryGetDateTimeOffset(out {p.Name})) throw new JsonException();",
                    _ when p.IsDecimal => $"if (!reader.Read() || !reader.TryGetDecimal(out {p.Name})) throw new JsonException();",
                    _ when p.IsDouble => $"if (!reader.Read() || !reader.TryGetDouble(out {p.Name})) throw new JsonException();",
                    _ when p.IsGuid => $"if (!reader.Read() || !reader.TryGetGuid(out {p.Name})) throw new JsonException();",
                    _ when p.IsInt16 => $"if (!reader.Read() || !reader.TryGetInt16(out {p.Name})) throw new JsonException();",
                    _ when p.IsInt32 => $"if (!reader.Read() || !reader.TryGetInt32(out {p.Name})) throw new JsonException();",
                    _ when p.IsInt64 => $"if (!reader.Read() || !reader.TryGetInt64(out {p.Name})) throw new JsonException();",
                    _ when p.IsSByte => $"if (!reader.Read() || !reader.TryGetSByte(out {p.Name})) throw new JsonException();",
                    _ when p.IsSingle => $"if (!reader.Read() || !reader.TryGetSingle(out {p.Name})) throw new JsonException();",
                    _ when p.IsUInt16 => $"if (!reader.Read() || !reader.TryGetUInt16(out {p.Name})) throw new JsonException();",
                    _ when p.IsUInt32 => $"if (!reader.Read() || !reader.TryGetUInt32(out {p.Name})) throw new JsonException();",
                    _ when p.IsUInt64 => $"if (!reader.Read() || !reader.TryGetUInt64(out {p.Name})) throw new JsonException();",
                    _ => $"{p.Name} = JsonSerializer.Deserialize<{p.TypeName}>(ref reader);"
                };

                w.Line(tryReadValue);

                w.Dedent();
                w.Line("}");
            }

            // handle no matching properties
            if (firstBranchHandled)
            {
                w.Write("else");
                w.Write("{");
                w.Indent();
            }

            // TODO handle complex values (using Skip)
            w.Line("if (!reader.Read()) throw new JsonException();");

            if (firstBranchHandled)
            {
                w.Dedent();
                w.Write("}");
            }

            w.Dedent();
            w.Line("}");
            w.Line();

            w.Line("throw new JsonException(\"Missing close object token. Invalid JSON.\");");

            w.Dedent();
            w.Line("}");
        }

        private void AppendWrite(SourceWriter w, string typeName, List<JensonPropertyInfo> propertiesInfo)
        {
            // WRITE
            // Relevant options
            // - PropertyNameCaseInsensitive
            // - IgnoreNullValues
            // - IgnoreReadOnlyProperties

            w.Line($"public override void Write(Utf8JsonWriter writer, {typeName} value, JsonSerializerOptions options)");
            w.Line("{");
            w.Indent();

            w.Line("writer.WriteStartObject();");
            w.Line();

            for (var i = 0; i < propertiesInfo.Count; i++)
            {
                var p = propertiesInfo[i];

                if (!p.ShouldSerialize) continue;

                var writeValueString = p switch
                {
                    //_ when p.IsArray => GenerateWriteArray(p),
                    _ when p.IsString ||
                           p.IsBoolean || 
                           p.IsDateTime ||
                           p.IsDateTimeOffset ||
                           p.IsGuid => $"writer.WriteString(_{p.Name}Name, value.{p.Name});",
                    _ when p.IsByte ||
                           p.IsDecimal ||
                           p.IsDouble ||
                           p.IsInt16 ||
                           p.IsInt32 ||
                           p.IsInt64 ||
                           p.IsSByte ||
                           p.IsSingle ||
                           p.IsUInt16 ||
                           p.IsUInt32 ||
                           p.IsUInt64 => $"writer.WriteNumber(_{p.Name}Name, value.{p.Name});",
                    _ => $"JsonSerializer.Serialize(writer, value.{p.Name}, options);"
                };

                if (p.IsReadOnly)
                {
                    w.Line($"if (!options.IgnoreReadOnlyProperties)");
                    w.Line("{");
                    w.Indent();
                }

                if (p.ShouldSerializeFunction != null)
                {
                    w.Line($"if (value.{p.ShouldSerializeFunction}())");
                    w.Line("{");
                    w.Indent();
                }

                if (p.CanBeNull)
                {
                    w.Line($"if (!(options.IgnoreNullValues && value.{p.Name} is null))");
                    w.Line("{");
                    w.Indent();
                }

                w.Line(writeValueString);

                if (p.CanBeNull)
                {
                    w.Dedent();
                    w.Line("}");
                }

                if (p.ShouldSerializeFunction != null)
                {
                    w.Dedent();
                    w.Line("}");
                }

                if (p.IsReadOnly)
                {
                    w.Dedent();
                    w.Line("}");
                }
            }

            w.Line();
            w.Line("writer.WriteEndObject();");

            w.Dedent();
            w.Line("}");
        }

        private string GenerateReadArray(JensonPropertyInfo p)
        {
            var w = new SourceWriter();
            w.Line("if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException();");
            w.Line();
            w.Line($"{p.Name} = JsonSerializer.Deserialize<{p.TypeName}>(ref reader);");

            return w.ToString();

            w.Line("var restore = reader;");
            w.Line("var length = 0;");
            w.Line();
            w.Line("while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)");
            w.Line("{");

            w.Indent();

            w.Line("if (reader.TokenType != JsonTokenType.Comment) length++;");
            w.Line("reader.Skip();");
            w.Dedent();
            w.Line("}");

            w.Line();

            w.Line("reader = restore;");

            w.Line($"{p.Name} = new {p.TypeName.Substring(0, p.TypeName.Length - 2)}[length];");

            var itemType = p.ArrayItemType;
            if (itemType.EndsWith("[]")) throw new NotImplementedException("Nested arrays are not implemented.");

            //var attribList = new List<OpAttribute>();
            //while (reader.Read())
            //{
            //    if (reader.TokenType == JsonTokenType.EndArray)
            //    {
            //        attributes = attribList.ToArray();
            //        break;
            //    }

            return w.ToString();
        }


        private string GenerateReadBoolean(JensonPropertyInfo p)
        {
            var w = new SourceWriter();

            w.Line($"if (reader.TokenType == JsonTokenType.True) {p.Name} = true;");
            w.Line($"else if (reader.TokenType == JsonTokenType.False) {p.Name} = false;");
            w.Line("else throw new JsonException(\"Boolean property must have true or false value.\");");

            return w.ToString();
        }
        private string GenerateWriteArray(JensonPropertyInfo p)
        {
            return $"JsonSerializer.Serialize(writer, value.{p.Name}, options);";
            //var w = new SourceWriter();

            //w.Line("writer.WriteStartArray();");
            //w.Line();
            //w.Line($"foreach (var item in value.{p.Name})");
            //w.Line("{");
            //w.Indent();

            //var writeValueString = p switch
            //{
            //    _ when p.IsArray => "throw new NotImplementedException(\"array\");",
            //    _ when p.IsString ||
            //            p.IsBoolean || 
            //            p.IsDateTime ||
            //            p.IsDateTimeOffset ||
            //            p.IsGuid => $"writer.WriteString(_{p.Name}Name, value.{p.Name});",
            //    _ when p.IsByte ||
            //            p.IsDecimal ||
            //            p.IsDouble ||
            //            p.IsInt16 ||
            //            p.IsInt32 ||
            //            p.IsInt64 ||
            //            p.IsSByte ||
            //            p.IsSingle ||
            //            p.IsUInt16 ||
            //            p.IsUInt32 ||
            //            p.IsUInt64 => $"writer.WriteNumber(_{p.Name}Name, value.{p.Name});",
            //    _ => $"JsonSerializer.Serialize(writer, value.{p.Name}, options);"
            //};

            //w.Dedent();
            //w.Line("}");
            //w.Line();
            //w.Line("writer.WriteEndArray();");

            //return w.ToString();
        }
 
        private static async Task WaitForDebugger(TimeSpan messageInterval, string message, TimeSpan checkInterval)
        {
            var currentProcessId = Process.GetCurrentProcess().Id;
            File.WriteAllText("C:\\Users\\jesse\\Desktop\\pid.txt", string.Format(message, currentProcessId));
            Console.WriteLine(message, currentProcessId);
            var aggregate = TimeSpan.Zero;

            while (!Debugger.IsAttached)
            {
                await Task.Delay(checkInterval);
                aggregate += checkInterval;
                while (aggregate > messageInterval)
                {
                    aggregate -= messageInterval;
                    Console.WriteLine(message, currentProcessId);
                }
            }
        }
    }

    public static class AttributeDataExtensions
    {
        public static bool TryGetProperty<T>(this AttributeData attr, string name, [NotNullWhen(true)] out T? value)
        {
            for (var i = 0; i < attr.NamedArguments.Length; i++)
            {
                if (attr.NamedArguments[i].Key.Equals(name, StringComparison.Ordinal))
                {
                    value = (T)attr.NamedArguments[i].Value.Value!;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }

    public static class ITypeSymbolExtensions
    {
        public static IEnumerable<ITypeSymbol> GetThisAndBaseTypes(this ITypeSymbol type)
        {
            var current = type;
            while (current != null)
            {
                yield return current;
                current = current.BaseType;
            }
        }

        public static bool IsRecord(this ITypeSymbol type) => 
            type.GetMembers()
                .Any(x => x.Kind == SymbolKind.Property &&
                          x.Name == "EqualityContract" &&
                          x.IsImplicitlyDeclared);
    }
}
