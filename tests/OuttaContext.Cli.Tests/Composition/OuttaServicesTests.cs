using Microsoft.Extensions.DependencyInjection;
using OuttaContext.Cli.Composition;
using OuttaContext.Core.Paths;
using OuttaContext.TestKit.Builders;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace OuttaContext.Cli.Tests.Composition;

public sealed class OuttaServicesTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    /// <summary>Catches a missing registration before a user does: every command can be created.</summary>
    [Fact]
    public void Create_EveryCommand_CanBeResolved()
    {
        // Arrange
        IServiceCollection services = OuttaServices.Create(new TestConsole(), new AppPaths(_temp.Path));
        Type[] commands = typeof(OuttaServices).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true } && typeof(ICommand).IsAssignableFrom(type))
            .ToArray();
        foreach (Type command in commands)
        {
            services.AddTransient(command);
        }

        // Act
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        // Assert
        commands.Length.ShouldBeGreaterThan(20);
        foreach (Type command in commands)
        {
            provider.GetService(command).ShouldNotBeNull(command.Name);
        }
    }
}
