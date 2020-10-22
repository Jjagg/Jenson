using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Jenson
{
    public static class INamespaceSymbolExtension
    {
        public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceOrTypeSymbol symbol, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<INamedTypeSymbol> result = Enumerable.Empty<INamedTypeSymbol>();

            if (symbol is INamespaceSymbol ns)
            {
                result = result.Concat(ns.GetMembers().SelectMany(subNs => GetAllTypes(subNs, cancellationToken)));
            }
            else if (symbol is INamedTypeSymbol ts)
            {
                result = Enumerable.Append(result, ts);
                result = result.Concat(ts.GetTypeMembers().SelectMany(subType => GetAllTypes(subType, cancellationToken)));
            }

            return result;
        }
    }
}
