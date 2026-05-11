using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata;
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

        [Column("consultationid")]
        public Guid? ConsultationId { get; set; }

        [Required]
        [Column("consultantid")]
        public Guid ConsultantId { get; set; }

        [Column("meds")]
        public string Meds { get; set; }

        [Column("conclusion")]
        public string Conclusion { get; set; }

        [Column("procedures")]
        public string Procedures { get; set; }

        [Column("recommendations")]
        public string Recommendations { get; set; }

        [Column("treatmentstart")]
        public DateTime? TreatmentStart { get; set; }

        [Column("treatmentend")]
        public DateTime? TreatmentEnd { get; set; }

        [MaxLength(200)]
        [Column("causeofanend")]
        public string CauseOfAnEnd { get; set; }

        [Column("createdat")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства
        [ForeignKey("MedicalBookId")]
        public MedicalBook MedicalBook { get; set; }

        [ForeignKey("ConsultationId")]
        public Consultation Consultation { get; set; }

        [ForeignKey("ConsultantId")]
        public Consultant Consultant { get; set; }

        public ICollection<Document> Documents { get; set; }
    }
}