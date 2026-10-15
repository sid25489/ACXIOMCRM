using AcxiomCRM.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AcxiomCRM.Tests.TestHelpers
{
    public static class TestDbContextFactory
    {
        /// <summary>
        /// Creates an isolated InMemory DbContext per test.
        /// Transactions are ignored (suppressed) because InMemory provider does not support them.
        /// </summary>
        public static ApplicationDbContext CreateInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }
    }
}
