using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Annotations;
using System.Linq;
using Votify.Entities;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Infrastructure.Annotations;

namespace Votify.Persistence
{
    // Asegúrate de que DBContextVotify herede de DbContext o cámbialo a DbContext directamente
    public class VotifyDBContext : DbContext
    {
        // El nombre "VotifyDbConnection" debe coincidir con el del App.config o Web.config
        public VotifyDBContext() : base("name=VotifyDbConnection")
        {
            Configuration.ProxyCreationEnabled = true;
            Configuration.LazyLoadingEnabled = true;
        }

        static VotifyDBContext()
        {
            // Nota: DropCreateDatabaseIfModelChanges puede fallar en Supabase si no tienes 
            // permisos de superusuario para borrar la DB. Es mejor usar null o Migrations.
            Database.SetInitializer<VotifyDBContext>(null);
        }

        // Los DbSets deben ser PUBLIC para que el motor los encuentre correctamente
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Evento> Eventos { get; set; }
        public DbSet<Votacion> Votaciones { get; set; }

        public DbSet<Voto> Votos { get; set; }
        public DbSet<Proyecto> Proyectos { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // PostgreSQL usa el esquema 'public' por defecto
            modelBuilder.HasDefaultSchema("public");

            modelBuilder.Entity<Voto>().Property(v => v.VotanteId).HasColumnAnnotation("Index", new IndexAnnotation(new IndexAttribute("IX_Voto_Unique", 1) { IsUnique = true }));

            modelBuilder.Entity<Voto>()
                .Property(v => v.VotacionId)
                .HasColumnAnnotation("Index", new IndexAnnotation(
                    new IndexAttribute("IX_Voto_Unique", 2) { IsUnique = true }));

            modelBuilder.Entity<Voto>()
                .Property(v => v.ProyectoId)
                .HasColumnAnnotation("Index", new IndexAnnotation(
                    new IndexAttribute("IX_Voto_Unique", 3) { IsUnique = true }));

            base.OnModelCreating(modelBuilder);
        }

        public void RemoveAllData()
        {
            // En EF6 para Postgres, borrar todo suele requerir ejecutar SQL crudo
            this.Database.ExecuteSqlCommand("TRUNCATE TABLE \"Usuarios\" CASCADE;");
            this.Database.ExecuteSqlCommand("TRUNCATE TABLE \"Votaciones\" CASCADE;");
        }
    }
}

