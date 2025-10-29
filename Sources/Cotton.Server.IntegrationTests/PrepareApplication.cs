using NUnit.Framework;
using Microsoft.EntityFrameworkCore.Storage;
using Cotton.Server.IntegrationTests.Abstractions;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cotton.Server.IntegrationTests
{
    public class PrepareApplication : IntegrationTestBase
    {
        [Test]
        [Order(1)]
        public void ResetDatabase()
        {
            var creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            creator.Create();
            Assert.Multiple(() =>
            {
                Assert.That(creator.Exists(), Is.True, "DB must exist after Create()");
                Assert.That(creator.HasTables(), Is.False, "DB must have no user tables after Create()");
            });
        }

        [Test]
        [Order(2)]
        public void StartApplication()
        {
            Task.Run(() =>
            {
                Cotton.Server.Program.Main(["--environment", "IntegrationTests"]);
            });
        }
    }
}
