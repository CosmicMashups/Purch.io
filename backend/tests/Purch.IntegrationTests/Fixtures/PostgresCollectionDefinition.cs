namespace Purch.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "Postgres collection";
}
