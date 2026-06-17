namespace Kiln.Cli.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

public sealed class TypeResolver(ServiceProvider provider) : ITypeResolver, IDisposable
{
    public object? Resolve(Type? type) =>
        type is null ? null : provider.GetService(type);

    public void Dispose() => provider.Dispose();
}
