using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TelemedicineSystem.Core.Entities
{
    [Table("entry_procedures")]
    public class EntryProcedure
    {
        [Key]
        [Column("entry_procedure_id")]
        public Guid EntryProcedureId { get; set; }

        [Required]
        [Column("entry_id")]
        public Guid EntryId { get; set; }

        [Required]
        [Column("procedure_id")]
        public Guid ProcedureId { get; set; }

        [ForeignKey("EntryId")]
        public Entry Entry { get; set; }

        [ForeignKey("ProcedureId")]
        public Procedure Procedure { get; set; }
    }
}