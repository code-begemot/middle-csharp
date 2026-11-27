namespace MiddleCsharp.IntegrationTests;

public static class PostgresCollection
{
    public const string Name = "postgres";
}

[CollectionDefinition(PostgresCollection.Name, DisableParallelization = true)]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>
{
}
