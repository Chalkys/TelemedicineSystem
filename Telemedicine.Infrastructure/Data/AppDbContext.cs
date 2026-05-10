using Microsoft.EntityFrameworkCore;
using Telemedicine.Core.Entities;

namespace Telemedicine.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Consultant> Consultants { get; set; }
        public DbSet<Operator> Operators { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Настройка связи User → Patient (1:1)
            modelBuilder.Entity<Patient>()
                .HasOne(p => p.User)
                .WithOne(u => u.Patient)
                .HasForeignKey<Patient>(p => p.UserId);

            // Настройка связи User → Consultant (1:1)
            modelBuilder.Entity<Consultant>()
                .HasOne(c => c.User)
                .WithOne(u => u.Consultant)
                .HasForeignKey<Consultant>(c => c.UserId);

            // Настройка связи User → Operator (1:1)
            modelBuilder.Entity<Operator>()
                .HasOne(o => o.User)
                .WithOne(u => u.Operator)
                .HasForeignKey<Operator>(o => o.UserId);
        }
    }
}