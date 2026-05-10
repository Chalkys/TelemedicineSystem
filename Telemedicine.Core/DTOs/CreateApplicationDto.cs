using System.ComponentModel.DataAnnotations;

namespace TelemedicineSystem.Core.DTOs
{
    public class CreateApplicationDto
    {
        [Required]
        [MaxLength(1)]
        public char Type { get; set; } // 'F' или 'P'

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; }
    }
}