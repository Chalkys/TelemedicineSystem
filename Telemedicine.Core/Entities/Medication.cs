using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TelemedicineSystem.Core.Entities
{
    [Table("medications")]
    public class Medication
    {
        [Key]
        [Column("medication_id")]
        public Guid MedicationId { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("name")]
        public string Name { get; set; }

        [MaxLength(100)]
        [Column("dosage")]
        public string? Dosage { get; set; }

        [MaxLength(100)]
        [Column("frequency")]
        public string? Frequency { get; set; }
    }
}