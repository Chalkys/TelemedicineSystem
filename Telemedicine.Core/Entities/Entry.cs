using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Telemedicine.Core.Entities;

namespace TelemedicineSystem.Core.Entities
{
    [Table("entries")]
    public class Entry
    {
        [Key]
        [Column("entryid")]
        public Guid EntryId { get; set; }

        [Required]
        [Column("medicalbookid")]
        public Guid MedicalBookId { get; set; }

        [Column("treatment_course_id")]
        public Guid? TreatmentCourseId { get; set; }

        [Column("consultationid")]
        public Guid? ConsultationId { get; set; }

        [Required]
        [Column("consultantid")]
        public Guid ConsultantId { get; set; }

        [Column("disease_id")]
        public Guid? DiseaseId { get; set; }

        [Column("conclusion")]
        public string? Conclusion { get; set; }

        [Column("recommendations")]
        public string? Recommendations { get; set; }

        [Column("createdat")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [Column("complaints")]
        public string? Complaints { get; set; }

        [Column("previous_diagnoses")]
        public string? PreviousDiagnoses { get; set; }

        [Column("current_medications")]
        public string? CurrentMedications { get; set; }

        // Навигационные свойства
        [ForeignKey("MedicalBookId")]
        public MedicalBook MedicalBook { get; set; }

        [ForeignKey("TreatmentCourseId")]
        public TreatmentCourse TreatmentCourse { get; set; }

        [ForeignKey("ConsultationId")]
        public Consultation? Consultation { get; set; }

        [ForeignKey("ConsultantId")]
        public Consultant Consultant { get; set; }

        [ForeignKey("DiseaseId")]
        public Disease? Disease { get; set; }

        public ICollection<Document> Documents { get; set; }
        public ICollection<EntryMedication> EntryMedications { get; set; }
        public ICollection<EntryProcedure> EntryProcedures { get; set; }
        public ICollection<EntryAnalysis> EntryAnalyses { get; set; }
    }
}