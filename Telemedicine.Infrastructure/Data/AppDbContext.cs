using Microsoft.EntityFrameworkCore;
using Telemedicine.Core.Entities;
using TelemedicineSystem.Core.Entities;

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
        public DbSet<Application> Applications { get; set; }
        public DbSet<Consultation> Consultations { get; set; }
        public DbSet<MedicalBook> MedicalBooks { get; set; }
        public DbSet<Entry> Entries { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<TreatmentCourse> TreatmentCourses { get; set; }
        public DbSet<Disease> Diseases { get; set; }
        public DbSet<Medication> Medications { get; set; }
        public DbSet<Procedure> Procedures { get; set; }
        public DbSet<Analysis> Analyses { get; set; }
        public DbSet<EntryMedication> EntryMedications { get; set; }
        public DbSet<EntryProcedure> EntryProcedures { get; set; }
        public DbSet<EntryAnalysis> EntryAnalyses { get; set; }

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

            // Application → Consultation (1:1)
            modelBuilder.Entity<Application>()
                .HasOne(a => a.Consultation)
                .WithOne(c => c.Application)
                .HasForeignKey<Consultation>(c => c.ApplicationId);

            // MedicalBook → Patient (1:1)
            modelBuilder.Entity<MedicalBook>()
                .HasOne(m => m.Patient)
                .WithOne()
                .HasForeignKey<MedicalBook>(m => m.PatientId);

            // MedicalBook → Entries (1:many)
            modelBuilder.Entity<MedicalBook>()
                .HasMany(m => m.Entries)
                .WithOne(e => e.MedicalBook)
                .HasForeignKey(e => e.MedicalBookId);

            // Entry → Documents (1:many)
            modelBuilder.Entity<Entry>()
                .HasMany(e => e.Documents)
                .WithOne(d => d.Entry)
                .HasForeignKey(d => d.EntryId);

            // Consultation → Entries (1:many)
            modelBuilder.Entity<Consultation>()
                .HasMany(c => c.Entries)
                .WithOne(e => e.Consultation)
                .HasForeignKey(e => e.ConsultationId);

            // Consultation → Documents (1:many)
            modelBuilder.Entity<Consultation>()
                .HasMany(c => c.Documents)
                .WithOne(d => d.Consultation)
                .HasForeignKey(d => d.ConsultationId);

            // TreatmentCourse → Entries (1:many)
            modelBuilder.Entity<TreatmentCourse>()
                .HasMany(t => t.Entries)
                .WithOne(e => e.TreatmentCourse)
                .HasForeignKey(e => e.TreatmentCourseId);

            // Entry → EntryMedication (1:many)
            modelBuilder.Entity<EntryMedication>()
                .HasOne(em => em.Entry)
                .WithMany(e => e.EntryMedications)
                .HasForeignKey(em => em.EntryId);

            modelBuilder.Entity<EntryMedication>()
                .HasOne(em => em.Medication)
                .WithMany()
                .HasForeignKey(em => em.MedicationId);

            // Entry → EntryProcedure (1:many)
            modelBuilder.Entity<EntryProcedure>()
                .HasOne(ep => ep.Entry)
                .WithMany(e => e.EntryProcedures)
                .HasForeignKey(ep => ep.EntryId);

            modelBuilder.Entity<EntryProcedure>()
                .HasOne(ep => ep.Procedure)
                .WithMany()
                .HasForeignKey(ep => ep.ProcedureId);

            // Entry → EntryAnalysis (1:many)
            modelBuilder.Entity<EntryAnalysis>()
                .HasOne(ea => ea.Entry)
                .WithMany(e => e.EntryAnalyses)
                .HasForeignKey(ea => ea.EntryId);

            modelBuilder.Entity<EntryAnalysis>()
                .HasOne(ea => ea.Analysis)
                .WithMany()
                .HasForeignKey(ea => ea.AnalysisId);

            // Entry → Disease (many:1)
            modelBuilder.Entity<Entry>()
                .HasOne(e => e.Disease)
                .WithMany()
                .HasForeignKey(e => e.DiseaseId);
        }
    }
}