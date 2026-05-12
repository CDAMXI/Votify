namespace Votify.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddReclamacion : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "public.reclamacion",
                c => new
                {
                    id_reclamacion = c.Int(nullable: false, identity: true),
                    id_evento = c.Int(nullable: false),
                    id_usuario = c.Int(nullable: false),
                    descripcion = c.String(nullable: false),
                    fecha_creacion = c.DateTime(nullable: false),
                    estado = c.String(nullable: false, maxLength: 20),
                    respuesta_organizador = c.String(),
                    fecha_respuesta = c.DateTime()
                })
                .PrimaryKey(t => t.id_reclamacion)
                .ForeignKey("public.evento", t => t.id_evento, cascadeDelete: true)
                .ForeignKey("public.usuario", t => t.id_usuario)
                .Index(t => t.id_evento)
                .Index(t => t.id_usuario);
        }

        public override void Down()
        {
            DropForeignKey("public.reclamacion", "id_usuario", "public.usuario");
            DropForeignKey("public.reclamacion", "id_evento", "public.evento");
            DropIndex("public.reclamacion", new[] { "id_usuario" });
            DropIndex("public.reclamacion", new[] { "id_evento" });
            DropTable("public.reclamacion");
        }
    }
}
