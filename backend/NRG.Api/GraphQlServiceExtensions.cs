using HotChocolate.Execution.Configuration;

namespace NRG.Api;

public static class IRequestExecutorBuilderExtensions
{
    public static IRequestExecutorBuilder AddGraphqlstuff(this IRequestExecutorBuilder builder)
    {
        return builder;
    }
}
