using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Telemedicine.Core.Entities;

namespace TelemedicineSystem.Core.Entities
{
    [Table("medicalbooks")]
    public class MedicalBook
    {
        [Key]
        [Column("medicalbookid")]
        public Guid MedicalBookId { get; set; }

        [Required]
        [Column("patientid")]
        public Guid PatientId { get; set; }

        [Column("creationdate")]
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;

        [Column("description")]
        public string Description { get; set; }

        // Навигационные свойства
        [ForeignKey("PatientId")]
        public Patient Patient { get; set; }

        public ICollection<Entry> Entries { get; set; }
    }
}