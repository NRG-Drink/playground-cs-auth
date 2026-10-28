internal class Program
{
    private static readonly string _frontendDir = Path.Combine("..", "..", "frontend");

    private static void Main(string[] args)
    {
        Console.WriteLine("Starting NRG AppHost...");
        CheckFrontendDir();

        var builder = DistributedApplication.CreateBuilder(args);

        var backend = builder
            .AddProject<Projects.NRG_Api>("backend", "https");

        var frontend = builder
                .AddViteApp("frontend", _frontendDir, runScriptName: "dev:relay")
                .WithHttpEndpoint(5173)
                .WithReference(backend)
                .WaitFor(backend);

        builder.Build().Run();
    }

    private static void CheckFrontendDir()
    {
        var isFrontendDirExists = Directory.Exists(_frontendDir);
        if (!isFrontendDirExists)
        {
            throw new DirectoryNotFoundException($"Frontend directory not found: {_frontendDir}");
        }
    }
}