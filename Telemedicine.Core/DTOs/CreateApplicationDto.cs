using System;
using System.ComponentModel.DataAnnotations;

namespace TelemedicineSystem.Core.DTOs
{
    public class CreateApplicationDto
    {
        [Required]
        public char Type { get; set; } // 'F' или 'P'

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; }

        public Guid? ConsultantId { get; set; }  // выбранный консультант

        public DateTime? ConsultationDate { get; set; }  // желаемая дата и время

        public string Description { get; set; }  // дополнительное описание (из окна подтверждения)
    }
}