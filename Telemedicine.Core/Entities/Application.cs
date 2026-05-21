using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Telemedicine.Core.Entities;

namespace TelemedicineSystem.Core.Entities
{
    [Table("applications")]
    public class Application
    {
        [Key]
        [Column("applicationid")]
        public Guid ApplicationId { get; set; }

        [Required]
        [Column("patientid")]
        public Guid PatientId { get; set; }

        [Required]
        [Column("type")]
        public char Type { get; set; } // 'F' - бесплатная, 'P' - платная

        [Required]
        [MaxLength(200)]
        [Column("subject")]
        public string Subject { get; set; }

        [Column("consultantid")]
        public Guid? ConsultantId { get; set; }  // может быть не назначен

        [Column("consultationdate")]
        public DateTime? ConsultationDate { get; set; }  // желаемая дата

        [Column("description")]
        public string? Description { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("createdat")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("complaints")]
        public string? Complaints { get; set; }

        [Column("previous_diagnoses")]
        public string? PreviousDiagnoses { get; set; }

        [Column("current_medications")]
        public string? CurrentMedications { get; set; }

        [ForeignKey("PatientId")]
        public Patient Patient { get; set; }

        [ForeignKey("ConsultantId")]
        public Consultant Consultant { get; set; }

        public Consultation Consultation { get; set; }
    }
}