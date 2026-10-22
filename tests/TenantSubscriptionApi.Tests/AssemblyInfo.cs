using Xunit;

// These are integration tests sharing ONE real Postgres database/server — running
// test classes in parallel (xUnit's default) causes DDL from one class (DROP/CREATE
// SCHEMA, migrations against the shared "public" schema) to race another's setup.
// Serializing is the standard fix for integration suites against a shared database.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
