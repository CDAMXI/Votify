using System.Data.Entity;
using System.Data.Entity.Infrastructure.Annotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Npgsql;
using Votify.Entities;

namespace Votify.Persistence
{
    [DbConfigurationType(typeof(VotifyDbConfiguration))]
    public class VotifyDBContext : DbContext
    {
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
            Database.SetInitializer<VotifyDBContext>(null);
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Evento> Eventos { get; set; }
        public DbSet<Votacion> Votaciones { get; set; }
        public DbSet<Voto> Votos { get; set; }
        public DbSet<Proyecto> Proyectos { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<Competidor> Competidores { get; set; }
        public DbSet<Publico> Publicos { get; set; }
        public DbSet<EncargadoVotacion> Encargados { get; set; }
        public DbSet<Jurado> Jurados { get; set; }
        public DbSet<Organizador> Organizadores { get; set; }
        public DbSet<Dashboard> Dashboards { get; set; }
        public DbSet<HojaRuta> HojasRuta { get; set; }
        public DbSet<Reclamacion> Reclamaciones { get; set; }
        public DbSet<Notificacion> Notificaciones { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("public");

            IgnoreEntitiesNotPresentInCurrentSchema(modelBuilder);
            ConfigureUsuario(modelBuilder);
            ConfigureEvento(modelBuilder);
            ConfigureRoles(modelBuilder);
            ConfigureVotacion(modelBuilder);
            ConfigureProyecto(modelBuilder);
            ConfigureVoto(modelBuilder);
            ConfigureDashboard(modelBuilder);
            ConfigureHojaRuta(modelBuilder);
            ConfigureReclamacion(modelBuilder);
            ConfigureNotificacion(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }

        private static void IgnoreEntitiesNotPresentInCurrentSchema(DbModelBuilder modelBuilder)
        {
            modelBuilder.Ignore<Baremo>();
            modelBuilder.Ignore<Categoria>();
            modelBuilder.Ignore<Certificado>();
            modelBuilder.Ignore<Premios>();
            modelBuilder.Ignore<Ranking>();
            modelBuilder.Ignore<Reglas>();
            modelBuilder.Ignore<Sugerencias>();
        }

        private static void ConfigureUsuario(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Usuario>().ToTable("usuario");
            modelBuilder.Entity<Usuario>().HasKey(u => u.Id);
            modelBuilder.Entity<Usuario>().Property(u => u.Id).HasColumnName("id_usuario");
            modelBuilder.Entity<Usuario>().Property(u => u.Username).HasColumnName("nombre").IsRequired();
            modelBuilder.Entity<Usuario>().Property(u => u.Email).HasColumnName("email").IsRequired();
            modelBuilder.Entity<Usuario>().Property(u => u.Password).HasColumnName("password_hash").IsRequired();
            modelBuilder.Entity<Usuario>().Property(u => u.FotoPerfil).HasColumnName("foto_perfil");
            modelBuilder.Entity<Usuario>().Property(u => u.ResetToken).HasColumnName("reset_token");
            modelBuilder.Entity<Usuario>().Property(u => u.ResetTokenExpiry).HasColumnName("reset_token_expiry");
        }

        private static void ConfigureEvento(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Evento>().ToTable("evento");
            modelBuilder.Entity<Evento>().HasKey(e => e.IdEvento);
            modelBuilder.Entity<Evento>().Property(e => e.IdEvento).HasColumnName("id_evento");
            modelBuilder.Entity<Evento>().Property(e => e.OrganizadorId).HasColumnName("id_organizador");
            modelBuilder.Entity<Evento>().Property(e => e.Nombre).HasColumnName("nombre").IsRequired();
            modelBuilder.Entity<Evento>().Property(e => e.Descripcion).HasColumnName("descripcion");
            modelBuilder.Entity<Evento>().Property(e => e.FechaIni).HasColumnName("fecha_inicio");
            modelBuilder.Entity<Evento>().Property(e => e.FechaFin).HasColumnName("fecha_fin");
            modelBuilder.Entity<Evento>().Property(e => e.PermiteCompetidoresVotar).HasColumnName("permite_competidores_votar");
            modelBuilder.Entity<Evento>().Property(e => e.codigoJurado).HasColumnName("codigo_jurado");
            modelBuilder.Entity<Evento>().Property(e => e.codigoEncargado).HasColumnName("codigo_encargado");

            modelBuilder.Entity<Evento>()
                .HasRequired(e => e.organizador)
                .WithMany(u => u.eventos)
                .HasForeignKey(e => e.OrganizadorId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Evento>().Ignore(e => e.sugerencias);
            modelBuilder.Entity<Evento>().Ignore(e => e.categorias);
            modelBuilder.Entity<Evento>().Ignore(e => e.reglas);
        }

        private static void ConfigureRoles(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Rol>().ToTable("rol_evento");
            modelBuilder.Entity<Rol>().HasKey(r => r.Id);
            modelBuilder.Entity<Rol>().Property(r => r.Id).HasColumnName("id_rol_evento");
            modelBuilder.Entity<Rol>().Property(r => r.UsuarioId).HasColumnName("id_usuario");
            modelBuilder.Entity<Rol>().Property(r => r.EventoId).HasColumnName("id_evento");
            modelBuilder.Entity<Rol>().Property(r => r.TipoRol).HasColumnName("tipo_rol").IsRequired();
            modelBuilder.Entity<Rol>().Property(r => r.FechaAsignacion).HasColumnName("fecha_asignacion");

            modelBuilder.Entity<Rol>()
                .HasRequired(r => r.usuario)
                .WithMany(u => u.roles)
                .HasForeignKey(r => r.UsuarioId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Rol>()
                .HasRequired(r => r.evento)
                .WithMany(e => e.roles)
                .HasForeignKey(r => r.EventoId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Competidor>().ToTable("competidor");
            modelBuilder.Entity<Competidor>().Property(r => r.Id).HasColumnName("id_competidor");

            modelBuilder.Entity<Publico>().ToTable("publico");
            modelBuilder.Entity<Publico>().Property(r => r.Id).HasColumnName("id_publico");

            modelBuilder.Entity<EncargadoVotacion>().ToTable("encargado");
            modelBuilder.Entity<EncargadoVotacion>().Property(r => r.Id).HasColumnName("id_encargado");

            modelBuilder.Entity<Jurado>().ToTable("jurado");
            modelBuilder.Entity<Jurado>().Property(r => r.Id).HasColumnName("id_jurado");

            modelBuilder.Entity<Organizador>().ToTable("organizador");
            modelBuilder.Entity<Organizador>().Property(r => r.Id).HasColumnName("id_organizador");
        }

        private static void ConfigureVotacion(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Votacion>().ToTable("votacion");
            modelBuilder.Entity<Votacion>().HasKey(v => v.Id);
            modelBuilder.Entity<Votacion>().Property(v => v.Id).HasColumnName("id_votacion");
            modelBuilder.Entity<Votacion>().Property(v => v.Titulo).HasColumnName("titulo").IsRequired();
            modelBuilder.Entity<Votacion>().Property(v => v.Descripcion).HasColumnName("descripcion");
            modelBuilder.Entity<Votacion>().Property(v => v.FechaIni).HasColumnName("fecha_inicio");
            modelBuilder.Entity<Votacion>().Property(v => v.FechaFin).HasColumnName("fecha_fin");
            modelBuilder.Entity<Votacion>().Property(v => v.NombreEstado).HasColumnName("estado").HasMaxLength(20);
            modelBuilder.Entity<Votacion>().Property(v => v.PesoJurado).HasColumnName("peso_jurado");
            modelBuilder.Entity<Votacion>().Property(v => v.PesoPublico).HasColumnName("peso_publico");

            modelBuilder.Entity<Votacion>().Property(v => v.EventoId).HasColumnName("id_evento");
            modelBuilder.Entity<Votacion>().Property(v => v.EncargadoId).HasColumnName("id_encargado");

            modelBuilder.Entity<Votacion>()
                .HasRequired(v => v.evento)
                .WithMany(e => e.votaciones)
                .HasForeignKey(v => v.EventoId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Votacion>()
                .HasRequired(v => v.Encargado)
                .WithMany(e => e.votaciones)
                .HasForeignKey(v => v.EncargadoId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Votacion>().Ignore(v => v.competidores);
            modelBuilder.Entity<Votacion>().Ignore(v => v.jurados);
            modelBuilder.Entity<Votacion>().Ignore(v => v.publicos);
            modelBuilder.Entity<Votacion>().Ignore(v => v.criterios);
            modelBuilder.Entity<Votacion>().Ignore(v => v.categoria);
            modelBuilder.Entity<Votacion>().Ignore(v => v.ranking);
        }

        private static void ConfigureProyecto(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Proyecto>().ToTable("proyecto");
            modelBuilder.Entity<Proyecto>().HasKey(p => p.Id);
            modelBuilder.Entity<Proyecto>().Property(p => p.Id).HasColumnName("id_proyecto");
            modelBuilder.Entity<Proyecto>().Property(p => p.Nombre).HasColumnName("nombre").IsRequired();
            modelBuilder.Entity<Proyecto>().Property(p => p.Descripcion).HasColumnName("descripcion");
            modelBuilder.Entity<Proyecto>().Property(p => p.ParticipantesAdicionales).HasColumnName("participantes_adicionales");
            modelBuilder.Entity<Proyecto>().Property(p => p.FotoProyecto).HasColumnName("foto_proyecto");
            modelBuilder.Entity<Proyecto>().Property(p => p.EventoId).HasColumnName("id_evento");
            modelBuilder.Entity<Proyecto>().Property(p => p.CompetidorId).HasColumnName("id_competidor");

            modelBuilder.Entity<Proyecto>()
                .HasRequired(p => p.evento)
                .WithMany(e => e.proyectos)
                .HasForeignKey(p => p.EventoId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Proyecto>()
                .HasRequired(p => p.competidor)
                .WithMany()
                .HasForeignKey(p => p.CompetidorId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Proyecto>().Ignore(p => p.Materiales);
            modelBuilder.Entity<Proyecto>().Ignore(p => p.categoria);
        }

        private static void ConfigureVoto(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Voto>().ToTable("voto");
            modelBuilder.Entity<Voto>().HasKey(v => v.Id);
            modelBuilder.Entity<Voto>().Property(v => v.Id).HasColumnName("id_voto");
            modelBuilder.Entity<Voto>().Property(v => v.VotacionId).HasColumnName("id_votacion");
            modelBuilder.Entity<Voto>().Property(v => v.ProyectoId).HasColumnName("id_proyecto");
            modelBuilder.Entity<Voto>().Property(v => v.VotanteId).HasColumnName("id_votante");
            modelBuilder.Entity<Voto>().Property(v => v.Valor).HasColumnName("valor");
            modelBuilder.Entity<Voto>().Property(v => v.Comentario).HasColumnName("comentario");
            modelBuilder.Entity<Voto>().Property(v => v.Fecha).HasColumnName("fecha");

            modelBuilder.Entity<Voto>().Property(v => v.VotacionId)
                .HasColumnAnnotation("Index", new IndexAnnotation(new IndexAttribute("uq_voto_votacion_proyecto_votante", 1) { IsUnique = true }));
            modelBuilder.Entity<Voto>().Property(v => v.ProyectoId)
                .HasColumnAnnotation("Index", new IndexAnnotation(new IndexAttribute("uq_voto_votacion_proyecto_votante", 2) { IsUnique = true }));
            modelBuilder.Entity<Voto>().Property(v => v.VotanteId)
                .HasColumnAnnotation("Index", new IndexAnnotation(new IndexAttribute("uq_voto_votacion_proyecto_votante", 3) { IsUnique = true }));

            modelBuilder.Entity<Voto>()
                .HasRequired(v => v.votacion)
                .WithMany(v => v.votos)
                .HasForeignKey(v => v.VotacionId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Voto>()
                .HasRequired(v => v.proyecto)
                .WithMany(p => p.votos)
                .HasForeignKey(v => v.ProyectoId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Voto>()
                .HasRequired(v => v.votante)
                .WithMany()
                .HasForeignKey(v => v.VotanteId)
                .WillCascadeOnDelete(false);
        }

        private static void ConfigureDashboard(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Dashboard>().ToTable("dashboard");
            modelBuilder.Entity<Dashboard>().HasKey(d => d.Id);
            modelBuilder.Entity<Dashboard>().Property(d => d.Id).HasColumnName("id_dashboard");
            modelBuilder.Entity<Dashboard>().Property(d => d.PuntuacionGlobal).HasColumnName("puntuacion_global");
            modelBuilder.Entity<Dashboard>().Property(d => d.Descripcion).HasColumnName("descripcion");
            modelBuilder.Entity<Dashboard>().Ignore(d => d.PuntuacionPorDimencion);

            modelBuilder.Entity<Dashboard>()
                .HasRequired(d => d.competidor)
                .WithMany()
                .Map(m => m.MapKey("id_competidor"))
                .WillCascadeOnDelete(true);
        }

        private static void ConfigureHojaRuta(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HojaRuta>().ToTable("hoja_ruta");
            modelBuilder.Entity<HojaRuta>().HasKey(h => h.Id);
            modelBuilder.Entity<HojaRuta>().Property(h => h.Id).HasColumnName("id_hoja_ruta");
            modelBuilder.Entity<HojaRuta>().Property(h => h.Descripcion).HasColumnName("descripcion").IsRequired();
            modelBuilder.Entity<HojaRuta>().Property(h => h.FechaGeneracion).HasColumnName("fecha_generacion");

            modelBuilder.Entity<HojaRuta>()
                .HasRequired(h => h.competidor)
                .WithMany()
                .Map(m => m.MapKey("id_competidor"))
                .WillCascadeOnDelete(true);
        }

        private static void ConfigureReclamacion(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Reclamacion>().ToTable("reclamacion");
            modelBuilder.Entity<Reclamacion>().HasKey(r => r.Id);
            modelBuilder.Entity<Reclamacion>().Property(r => r.Id).HasColumnName("id_reclamacion");
            modelBuilder.Entity<Reclamacion>().Property(r => r.EventoId).HasColumnName("id_evento");
            modelBuilder.Entity<Reclamacion>().Property(r => r.UsuarioId).HasColumnName("id_usuario");
            modelBuilder.Entity<Reclamacion>().Property(r => r.Descripcion).HasColumnName("descripcion").IsRequired();
            modelBuilder.Entity<Reclamacion>().Property(r => r.FechaCreacion).HasColumnName("fecha_creacion");
            modelBuilder.Entity<Reclamacion>().Property(r => r.Estado).HasColumnName("estado").IsRequired();
            modelBuilder.Entity<Reclamacion>().Property(r => r.RespuestaOrganizador).HasColumnName("respuesta_organizador");
            modelBuilder.Entity<Reclamacion>().Property(r => r.FechaRespuesta).HasColumnName("fecha_respuesta");

            modelBuilder.Entity<Reclamacion>()
                .HasRequired(r => r.evento)
                .WithMany()
                .HasForeignKey(r => r.EventoId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Reclamacion>()
                .HasRequired(r => r.usuario)
                .WithMany()
                .HasForeignKey(r => r.UsuarioId)
                .WillCascadeOnDelete(false);
        }

        private static void ConfigureNotificacion(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Notificacion>().ToTable("notificacion");
            modelBuilder.Entity<Notificacion>().HasKey(n => n.Id);
            modelBuilder.Entity<Notificacion>().Property(n => n.Id).HasColumnName("id");
            modelBuilder.Entity<Notificacion>().Property(n => n.RemitenteId).HasColumnName("id_remitente");
            modelBuilder.Entity<Notificacion>().Property(n => n.DestinatarioId).HasColumnName("id_destinatario");
            modelBuilder.Entity<Notificacion>().Property(n => n.Asunto).HasColumnName("asunto").IsRequired();
            modelBuilder.Entity<Notificacion>().Property(n => n.Mensaje).HasColumnName("mensaje").IsRequired();
            modelBuilder.Entity<Notificacion>().Property(n => n.FechaCreacion).HasColumnName("fecha_creacion");
            modelBuilder.Entity<Notificacion>().Property(n => n.Leida).HasColumnName("leida");

            modelBuilder.Entity<Notificacion>()
                .HasRequired(n => n.remitente)
                .WithMany(u => u.notificacionesEnviadas)
                .HasForeignKey(n => n.RemitenteId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Notificacion>()
                .HasRequired(n => n.destinatario)
                .WithMany(u => u.notificacionesRecibidas)
                .HasForeignKey(n => n.DestinatarioId)
                .WillCascadeOnDelete(false);
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
            Database.ExecuteSqlCommand("TRUNCATE TABLE public.usuario CASCADE;");
            Database.ExecuteSqlCommand("TRUNCATE TABLE public.evento CASCADE;");
        }

        public void EnsureAdministrativeSettingsSchema()
        {
            Database.ExecuteSqlCommand(
                "ALTER TABLE public.votacion ADD COLUMN IF NOT EXISTS peso_jurado integer NOT NULL DEFAULT 70;");
            Database.ExecuteSqlCommand(
                "ALTER TABLE public.votacion ADD COLUMN IF NOT EXISTS peso_publico integer NOT NULL DEFAULT 30;");
            Database.ExecuteSqlCommand(
                "ALTER TABLE public.proyecto ADD COLUMN IF NOT EXISTS foto_proyecto text;");
            Database.ExecuteSqlCommand(@"
                CREATE TABLE IF NOT EXISTS public.reclamacion (
                    id_reclamacion serial PRIMARY KEY,
                    id_evento integer NOT NULL REFERENCES public.evento(id_evento) ON DELETE CASCADE,
                    id_usuario integer NOT NULL REFERENCES public.usuario(id_usuario),
                    descripcion text NOT NULL,
                    fecha_creacion timestamp without time zone NOT NULL DEFAULT now(),
                    estado varchar(20) NOT NULL DEFAULT 'PENDIENTE',
                    respuesta_organizador text NULL,
                    fecha_respuesta timestamp without time zone NULL
                );");
            Database.ExecuteSqlCommand(
                "CREATE INDEX IF NOT EXISTS ix_reclamacion_evento ON public.reclamacion(id_evento);");
            Database.ExecuteSqlCommand(
                "CREATE INDEX IF NOT EXISTS ix_reclamacion_usuario ON public.reclamacion(id_usuario);");
        }
    }
}
