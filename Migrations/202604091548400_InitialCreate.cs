namespace Votify.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "public.Eventoes",
                c => new
                    {
                        IdEvento = c.Int(nullable: false, identity: true),
                        Nombre = c.String(),
                        FechaIni = c.DateTime(nullable: false),
                        FechaFin = c.DateTime(nullable: false),
                        Descripcion = c.String(),
                        PermiteCompetidoresVotar = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.IdEvento);
            
            CreateTable(
                "public.Votacions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        FechaIni = c.DateTime(nullable: false),
                        FechaFin = c.DateTime(nullable: false),
                        Estado = c.Boolean(nullable: false),
                        evento_IdEvento = c.Int(),
                        EncargadoVotacion_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("public.Eventoes", t => t.evento_IdEvento)
                .ForeignKey("public.Rols", t => t.EncargadoVotacion_Id)
                .Index(t => t.evento_IdEvento)
                .Index(t => t.EncargadoVotacion_Id);
            
            CreateTable(
                "public.Proyectoes",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Nombre = c.String(),
                        Descripcion = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "public.Rols",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        FechaAsignacion = c.DateTime(nullable: false),
                        TipoRol = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "public.Usuarios",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Username = c.String(),
                        Password = c.String(),
                        Email = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "public.Votoes",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Valor = c.Double(nullable: false),
                        Comentario = c.String(),
                        Fecha = c.DateTime(nullable: false),
                        VotanteId = c.Int(nullable: false),
                        VotacionId = c.Int(nullable: false),
                        ProyectoId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => new { t.VotanteId, t.VotacionId, t.ProyectoId }, unique: true, name: "IX_Voto_Unique");
            
        }
        
        public override void Down()
        {
            DropForeignKey("public.Votacions", "EncargadoVotacion_Id", "public.Rols");
            DropForeignKey("public.Votacions", "evento_IdEvento", "public.Eventoes");
            DropIndex("public.Votoes", "IX_Voto_Unique");
            DropIndex("public.Votacions", new[] { "EncargadoVotacion_Id" });
            DropIndex("public.Votacions", new[] { "evento_IdEvento" });
            DropTable("public.Votoes");
            DropTable("public.Usuarios");
            DropTable("public.Rols");
            DropTable("public.Proyectoes");
            DropTable("public.Votacions");
            DropTable("public.Eventoes");
        }
    }
}
