using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using NosCore.Configuration;

namespace NosCore.Database
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NosCoreContext>
    {
        private const string _configurationPath = @"../../../configuration";

        public NosCoreContext CreateDbContext(string[] args)
        {
            var _databaseConfiguration = new SqlConnectionConfiguration();
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(Directory.GetCurrentDirectory() + _configurationPath);
            builder.AddJsonFile("database.json", false);
            builder.Build().Bind(_databaseConfiguration);
            var optionsBuilder = new DbContextOptionsBuilder<NosCoreContext>();
            optionsBuilder.UseNpgsql(_databaseConfiguration.ConnectionString);
            return new NosCoreContext(optionsBuilder.Options);
        }
    }
}