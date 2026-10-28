using TUnit.Core.Interfaces;

namespace NRG.Api.Tests.AuthEndpoints;

public interface ITestFixture<T> where T : AuthApiFactory, new()
{
    HttpClient Client { get; }
    T Factory { get; }
}

public class AuthApiTestFixture<T> : IAsyncInitializer, IAsyncDisposable, ITestFixture<T> where T : AuthApiFactory, new()
{
    public T Factory { get; } = new();
    public HttpClient Client { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Client = Factory.CreateClient();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        return ValueTask.CompletedTask;
    }
}
