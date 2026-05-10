using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Telemedicine.Core.Entities;

namespace TelemedicineSystem.Core.Entities
{
    public class MedicalBook
    {
        [Key]
        public Guid MedicalBookId { get; set; }

        [Required]
        public Guid PatientId { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.UtcNow;

        public string Description { get; set; }

        // Навигационные свойства
        [ForeignKey("PatientId")]
        public Patient Patient { get; set; }

        public ICollection<Entry> Entries { get; set; }
    }
}