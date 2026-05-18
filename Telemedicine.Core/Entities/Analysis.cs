using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TelemedicineSystem.Core.Entities
{
    [Table("analyses")]
    public class Analysis
    {
        [Key]
        [Column("analysis_id")]
        public Guid AnalysisId { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("name")]
        public string Name { get; set; }

        [Column("description")]
        public string? Description { get; set; }
    }
}