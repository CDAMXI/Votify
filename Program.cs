using System;
using System.Linq;
using System.Data.Common;
using Npgsql;
using Votify.Presentation;
using Votify.Tests;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);

DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);

if (Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, "--dbtest", StringComparison.OrdinalIgnoreCase)))
{
    var result = DBTest.Run();
    MessageBox.Show(
        result.Message,
        result.Success ? "DBTest correcto" : "DBTest con error",
        MessageBoxButtons.OK,
        result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    return;
}

if (Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, "--checkdb", StringComparison.OrdinalIgnoreCase)))
{
    var result = DBTest.CheckData();
    MessageBox.Show(
        result.Message,
        result.Success ? "Comprobacion BD" : "Comprobacion BD con error",
        MessageBoxButtons.OK,
        result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    return;
}

// Aqui ira la inicializacion real del DAL y el servicio
// IVotifyService service = new VotifyService(new EntityFrameworkDAL(new VotifyDBContext()));
