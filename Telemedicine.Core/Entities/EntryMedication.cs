using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TelemedicineSystem.Core.Entities
{
    [Table("entry_medications")]
    public class EntryMedication
    {
        [Key]
        [Column("entry_medication_id")]
        public Guid EntryMedicationId { get; set; }

        [Required]
        [Column("entry_id")]
        public Guid EntryId { get; set; }

        [Required]
        [Column("medication_id")]
        public Guid MedicationId { get; set; }

        [ForeignKey("EntryId")]
        public Entry Entry { get; set; }

        [ForeignKey("MedicationId")]
        public Medication Medication { get; set; }
    }
}