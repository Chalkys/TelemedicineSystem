using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TelemedicineSystem.Core.Entities
{
    [Table("procedures")]
    public class Procedure
    {
        [Key]
        [Column("procedure_id")]
        public Guid ProcedureId { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("name")]
        public string Name { get; set; }

        [Column("description")]
        public string? Description { get; set; }
    }
}