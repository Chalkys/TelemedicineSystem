using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata;
using Telemedicine.Core.Entities;

namespace TelemedicineSystem.Core.Entities
{
    public class Entry
    {
        [Key]
        public Guid EntryId { get; set; }

        [Required]
        public Guid MedicalBookId { get; set; }

        public Guid? ConsultationId { get; set; }

        [Required]
        public Guid ConsultantId { get; set; }

        public string Meds { get; set; }

        public string Procedures { get; set; }

        public string Recommendations { get; set; }

        public DateTime? TreatmentStart { get; set; }

        public DateTime? TreatmentEnd { get; set; }

        [MaxLength(200)]
        public string CauseOfAnEnd { get; set; }

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