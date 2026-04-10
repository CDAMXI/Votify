namespace Votify.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddResetToken : DbMigration
    {
        public override void Up()
        {
            AddColumn("public.Usuarios", "ResetToken", c => c.String());
            AddColumn("public.Usuarios", "ResetTokenExpiry", c => c.DateTime());
        }
        
        public override void Down()
        {
            DropColumn("public.Usuarios", "ResetTokenExpiry");
            DropColumn("public.Usuarios", "ResetToken");
        }
    }
}
