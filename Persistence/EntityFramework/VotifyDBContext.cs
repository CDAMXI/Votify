using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.Annotations;
using System.Linq;
using Npgsql;
using Votify.Entities;

namespace Votify.Persistence
{
    [DbConfigurationType(typeof(VotifyDbConfiguration))]
    public class VotifyDBContext : DbContext
    {
        // El nombre "VotifyDbConnection" debe coincidir con el del App.config o Web.config
        private const string DefaultConnection =
            "Host=aws-1-eu-west-1.pooler.supabase.com;Port=5432;Database=postgres;" +
            "Username=postgres.qoahzxuzktsrzleaxjeo;Password=yvuWJPRkV5uWleTN";

        public VotifyDBContext()
            : base(new NpgsqlConnection(DefaultConnection), contextOwnsConnection: true)
        {
            Configuration.ProxyCreationEnabled = true;
            Configuration.LazyLoadingEnabled = true;
        }

        public VotifyDBContext(string connectionString)
            : base(new NpgsqlConnection(connectionString), contextOwnsConnection: true)
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
        public DbSet<Rol> Roles { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Rol>()
                .Map<Jurado>(m => m.Requires("TipoRol").HasValue("EXPERT"))
                .Map<Competidor>(m => m.Requires("TipoRol").HasValue("COMPETITOR"))
                .Map<Organizador>(m => m.Requires("TipoRol").HasValue("ORGANIZER"))
                .Map<EncargadoVotacion>(m => m.Requires("TipoRol").HasValue("VOTING_MANAGER"))
                .Map<Publico>(m => m.Requires("TipoRol").HasValue("PUBLIC"));

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

            // Relaciones 1-a-1 explícitas
            modelBuilder.Entity<Reglas>()
                .HasRequired(r => r.evento)
                .WithOptional(e => e.reglas);

            modelBuilder.Entity<Ranking>()
                .HasRequired(r => r.votacion)
                .WithOptional(v => v.ranking);

            modelBuilder.Entity<Ranking>()
                .HasRequired(r => r.proyecto)
                .WithMany();

            modelBuilder.Entity<Categoria>()
                .HasRequired(c => c.evento)
                .WithMany(e => e.categorias);

            modelBuilder.Entity<Sugerencias>()
                .HasRequired(s => s.evento)
                .WithMany(e => e.sugerencias);

            modelBuilder.Entity<Baremo>()
                .HasRequired(b => b.votacion)
                .WithMany(v => v.criterios);

            modelBuilder.Entity<Premios>()
                .HasRequired(p => p.votacion)
                .WithMany();

            modelBuilder.Entity<Premios>()
                .HasOptional(p => p.categoria)
                .WithMany(c => c.premios);

            modelBuilder.Entity<Proyecto>()
                .HasOptional(p => p.categoria)
                .WithMany();

            modelBuilder.Entity<Proyecto>()
                .HasOptional(p => p.evento)
                .WithMany(e => e.proyectos);

            base.OnModelCreating(modelBuilder);
        }

        public void Rollback()
        {
            foreach (var entry in ChangeTracker.Entries().ToList())
            {
                switch (entry.State)
                {
                    case EntityState.Modified:
                        entry.State = EntityState.Unchanged;
                        break;
                    case EntityState.Added:
                        entry.State = EntityState.Detached;
                        break;
                    case EntityState.Deleted:
                        entry.Reload();
                        break;
                }
            }
        }

        public void RemoveAllData()
        {
            // En EF6 para Postgres, borrar todo suele requerir ejecutar SQL crudo
            this.Database.ExecuteSqlCommand("TRUNCATE TABLE \"Usuarios\" CASCADE;");
            this.Database.ExecuteSqlCommand("TRUNCATE TABLE \"Votaciones\" CASCADE;");
        }
    }
}

