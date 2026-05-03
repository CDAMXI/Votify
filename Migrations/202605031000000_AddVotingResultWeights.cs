namespace Votify.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddVotingResultWeights : DbMigration
    {
        public override void Up()
        {
            AddColumn("public.votacion", "peso_jurado", c => c.Int(nullable: false, defaultValue: 70));
            AddColumn("public.votacion", "peso_publico", c => c.Int(nullable: false, defaultValue: 30));
        }

        public override void Down()
        {
            DropColumn("public.votacion", "peso_publico");
            DropColumn("public.votacion", "peso_jurado");
        }
    }
}
