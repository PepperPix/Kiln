namespace Kiln.Cli.Tests;

using Kiln.Cli.Commands;
using Kiln.Cli.Tests.Fakes;
using Kiln.Services;
using Microsoft.Extensions.DependencyInjection;

public class ServeCommandAppTests
{
    [Test]
    public async Task ServeCommand_StartMessage_ContainsConfiguredPort()
    {
        var devServer = new FakeDevServer
        {
            RunBehavior = () => throw new OperationCanceledException(),
        };

        var (app, console) = CommandAppTesterFactory.Create(services =>
        {
            services.AddSingleton<IDevServer>(devServer);
        });

        app.Configure(config => config.AddCommand<ServeCommand>("serve"));

        var result = await app.RunAsync(["serve", "--port", "6123"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("6123");
        await Assert.That(console.Output).Contains("Server stopped");
    }
}
