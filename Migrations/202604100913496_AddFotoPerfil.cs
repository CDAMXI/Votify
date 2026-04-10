namespace Votify.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddFotoPerfil : DbMigration
    {
        public override void Up()
        {
            AddColumn("public.Usuarios", "FotoPerfil", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("public.Usuarios", "FotoPerfil");
        }
    }
}
