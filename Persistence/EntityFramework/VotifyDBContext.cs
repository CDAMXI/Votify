using Votify.Entities;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;


namespace Votify.Persistence
{
    public class VotifyDBContext : DBContextVotify
    {
        public VotifyDBContext() : base("Name=VotifyDbConnection") //this is the connection string name
        {
            /*
            See DbContext.Configuration documentation
            */
            Configuration.ProxyCreationEnabled = true;
            Configuration.LazyLoadingEnabled = true;
        }

        static VotifyDBContext()
        {
            Database.SetInitializer<VotifyDBContext>(new DropCreateDatabaseIfModelChanges<VotifyDBContext>());
        }

        // DbSets for persistent classes in your case study
        // TO BE DONE IMPLEMENTED
        DbSet<Usuario> Usuario { get; set; }
        DbSet<Votacion> Votacion { get; set; }


        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            /*                        modelBuilder.Entity<Part>()
                                                .HasMany(p => p.UsedParts)
                                                .WithRequired(uP => uP.Part)
                                                .WillCascadeOnDelete(true);

                                    modelBuilder.Entity<UsedPart>()
                                        .HasRequired(p => p.Part)
                                        .WithMany(uP => uP.UsedParts)
                                        .WillCascadeOnDelete(false);
            */

        }

        // Generic method to clear all the data (except some relations if needed)
        public override void RemoveAllData()
        {
            clearSomeRelationships();

            base.RemoveAllData();
        }

        // Sometimes it is needed to clear some relationships explicitly 
        private void clearSomeRelationships()
        {
            //            SaveChanges();
        }

    }
}

