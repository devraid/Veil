using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Veil.Data;

public static class VeilDbContextFactory
{
    public static VeilDbContext Create()
    {
        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Veil");

        Directory.CreateDirectory(appDataDirectory);

        var databasePath = Path.Combine(appDataDirectory, "veil.db");
        var options = new DbContextOptionsBuilder<VeilDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new VeilDbContext(options);
    }
}

public sealed class VeilDesignTimeDbContextFactory : IDesignTimeDbContextFactory<VeilDbContext>
{
    public VeilDbContext CreateDbContext(string[] args)
    {
        return VeilDbContextFactory.Create();
    }
}
