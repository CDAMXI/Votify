namespace Votify.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class FixEntityKeys : DbMigration
    {
        public override void Up()
        {
            RenameColumn(table: "public.Votacions", name: "EncargadoVotacion_Id", newName: "Encargado_Id");
            RenameIndex(table: "public.Votacions", name: "IX_EncargadoVotacion_Id", newName: "IX_Encargado_Id");
            CreateTable(
                "public.Categorias",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Nombre = c.String(),
                        Descripcion = c.String(),
                        evento_IdEvento = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("public.Eventoes", t => t.evento_IdEvento, cascadeDelete: true)
                .Index(t => t.evento_IdEvento);
            
            CreateTable(
                "public.Premios",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Nombre = c.String(),
                        Descripcion = c.String(),
                        categoria_Id = c.Int(),
                        votacion_Id = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("public.Categorias", t => t.categoria_Id)
                .ForeignKey("public.Votacions", t => t.votacion_Id, cascadeDelete: true)
                .Index(t => t.categoria_Id)
                .Index(t => t.votacion_Id);
            
            CreateTable(
                "public.Baremoes",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Nombre = c.String(),
                        Descripcion = c.String(),
                        Peso = c.Double(nullable: false),
                        Tipo = c.Int(nullable: false),
                        votacion_Id = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("public.Votacions", t => t.votacion_Id, cascadeDelete: true)
                .Index(t => t.votacion_Id);
            
            CreateTable(
                "public.Rankings",
                c => new
                    {
                        Id = c.Int(nullable: false),
                        Posicion = c.Int(nullable: false),
                        PuntajeTotal = c.Double(nullable: false),
                        EsManual = c.Boolean(nullable: false),
                        proyecto_Id = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("public.Proyectoes", t => t.proyecto_Id, cascadeDelete: true)
                .ForeignKey("public.Votacions", t => t.Id)
                .Index(t => t.Id)
                .Index(t => t.proyecto_Id);
            
            CreateTable(
                "public.Reglas",
                c => new
                    {
                        Id = c.Int(nullable: false),
                        Descripcion = c.String(),
                        ConfigPuntos = c.Int(nullable: false),
                        MaxVotosPersona = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("public.Eventoes", t => t.Id)
                .Index(t => t.Id);
            
            CreateTable(
                "public.Sugerencias",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Contenido = c.String(),
                        Fecha = c.DateTime(nullable: false),
                        evento_IdEvento = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("public.Eventoes", t => t.evento_IdEvento, cascadeDelete: true)
                .Index(t => t.evento_IdEvento);
            
            AddColumn("public.Eventoes", "Usuario_Id", c => c.Int());
            AddColumn("public.Votacions", "categoria_Id", c => c.Int());
            AddColumn("public.Proyectoes", "categoria_Id", c => c.Int());
            AddColumn("public.Proyectoes", "competidor_Id", c => c.Int());
            AddColumn("public.Proyectoes", "evento_IdEvento", c => c.Int());
            AddColumn("public.Rols", "evento_IdEvento", c => c.Int());
            AddColumn("public.Rols", "usuario_Id", c => c.Int());
            AddColumn("public.Rols", "Votacion_Id", c => c.Int());
            AddColumn("public.Rols", "Votacion_Id1", c => c.Int());
            AddColumn("public.Rols", "Votacion_Id2", c => c.Int());
            CreateIndex("public.Eventoes", "Usuario_Id");
            CreateIndex("public.Votacions", "categoria_Id");
            CreateIndex("public.Rols", "evento_IdEvento");
            CreateIndex("public.Rols", "usuario_Id");
            CreateIndex("public.Rols", "Votacion_Id");
            CreateIndex("public.Rols", "Votacion_Id1");
            CreateIndex("public.Rols", "Votacion_Id2");
            CreateIndex("public.Proyectoes", "categoria_Id");
            CreateIndex("public.Proyectoes", "competidor_Id");
            CreateIndex("public.Proyectoes", "evento_IdEvento");
            AddForeignKey("public.Votacions", "categoria_Id", "public.Categorias", "Id");
            AddForeignKey("public.Eventoes", "Usuario_Id", "public.Usuarios", "Id");
            AddForeignKey("public.Rols", "evento_IdEvento", "public.Eventoes", "IdEvento");
            AddForeignKey("public.Rols", "usuario_Id", "public.Usuarios", "Id");
            AddForeignKey("public.Rols", "Votacion_Id", "public.Votacions", "Id");
            AddForeignKey("public.Rols", "Votacion_Id1", "public.Votacions", "Id");
            AddForeignKey("public.Rols", "Votacion_Id2", "public.Votacions", "Id");
            AddForeignKey("public.Proyectoes", "categoria_Id", "public.Categorias", "Id");
            AddForeignKey("public.Proyectoes", "competidor_Id", "public.Rols", "Id");
            AddForeignKey("public.Proyectoes", "evento_IdEvento", "public.Eventoes", "IdEvento");
            AddForeignKey("public.Votoes", "ProyectoId", "public.Proyectoes", "Id", cascadeDelete: true);
            AddForeignKey("public.Votoes", "VotacionId", "public.Votacions", "Id", cascadeDelete: true);
            AddForeignKey("public.Votoes", "VotanteId", "public.Rols", "Id", cascadeDelete: true);
        }
        
        public override void Down()
        {
            DropForeignKey("public.Sugerencias", "evento_IdEvento", "public.Eventoes");
            DropForeignKey("public.Reglas", "Id", "public.Eventoes");
            DropForeignKey("public.Premios", "votacion_Id", "public.Votacions");
            DropForeignKey("public.Rankings", "Id", "public.Votacions");
            DropForeignKey("public.Rankings", "proyecto_Id", "public.Proyectoes");
            DropForeignKey("public.Votoes", "VotanteId", "public.Rols");
            DropForeignKey("public.Votoes", "VotacionId", "public.Votacions");
            DropForeignKey("public.Votoes", "ProyectoId", "public.Proyectoes");
            DropForeignKey("public.Proyectoes", "evento_IdEvento", "public.Eventoes");
            DropForeignKey("public.Proyectoes", "competidor_Id", "public.Rols");
            DropForeignKey("public.Proyectoes", "categoria_Id", "public.Categorias");
            DropForeignKey("public.Rols", "Votacion_Id2", "public.Votacions");
            DropForeignKey("public.Rols", "Votacion_Id1", "public.Votacions");
            DropForeignKey("public.Baremoes", "votacion_Id", "public.Votacions");
            DropForeignKey("public.Rols", "Votacion_Id", "public.Votacions");
            DropForeignKey("public.Rols", "usuario_Id", "public.Usuarios");
            DropForeignKey("public.Rols", "evento_IdEvento", "public.Eventoes");
            DropForeignKey("public.Eventoes", "Usuario_Id", "public.Usuarios");
            DropForeignKey("public.Votacions", "categoria_Id", "public.Categorias");
            DropForeignKey("public.Premios", "categoria_Id", "public.Categorias");
            DropForeignKey("public.Categorias", "evento_IdEvento", "public.Eventoes");
            DropIndex("public.Sugerencias", new[] { "evento_IdEvento" });
            DropIndex("public.Reglas", new[] { "Id" });
            DropIndex("public.Proyectoes", new[] { "evento_IdEvento" });
            DropIndex("public.Proyectoes", new[] { "competidor_Id" });
            DropIndex("public.Proyectoes", new[] { "categoria_Id" });
            DropIndex("public.Rankings", new[] { "proyecto_Id" });
            DropIndex("public.Rankings", new[] { "Id" });
            DropIndex("public.Baremoes", new[] { "votacion_Id" });
            DropIndex("public.Rols", new[] { "Votacion_Id2" });
            DropIndex("public.Rols", new[] { "Votacion_Id1" });
            DropIndex("public.Rols", new[] { "Votacion_Id" });
            DropIndex("public.Rols", new[] { "usuario_Id" });
            DropIndex("public.Rols", new[] { "evento_IdEvento" });
            DropIndex("public.Votacions", new[] { "categoria_Id" });
            DropIndex("public.Premios", new[] { "votacion_Id" });
            DropIndex("public.Premios", new[] { "categoria_Id" });
            DropIndex("public.Categorias", new[] { "evento_IdEvento" });
            DropIndex("public.Eventoes", new[] { "Usuario_Id" });
            DropColumn("public.Rols", "Votacion_Id2");
            DropColumn("public.Rols", "Votacion_Id1");
            DropColumn("public.Rols", "Votacion_Id");
            DropColumn("public.Rols", "usuario_Id");
            DropColumn("public.Rols", "evento_IdEvento");
            DropColumn("public.Proyectoes", "evento_IdEvento");
            DropColumn("public.Proyectoes", "competidor_Id");
            DropColumn("public.Proyectoes", "categoria_Id");
            DropColumn("public.Votacions", "categoria_Id");
            DropColumn("public.Eventoes", "Usuario_Id");
            DropTable("public.Sugerencias");
            DropTable("public.Reglas");
            DropTable("public.Rankings");
            DropTable("public.Baremoes");
            DropTable("public.Premios");
            DropTable("public.Categorias");
            RenameIndex(table: "public.Votacions", name: "IX_Encargado_Id", newName: "IX_EncargadoVotacion_Id");
            RenameColumn(table: "public.Votacions", name: "Encargado_Id", newName: "EncargadoVotacion_Id");
        }
    }
}
