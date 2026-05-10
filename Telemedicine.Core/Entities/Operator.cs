using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Telemedicine.Core.Entities
{
    [Table("operators")]
    public class Operator
    {
        [Key]
        [Column("operatorid")]
        public Guid OperatorId { get; set; }  // Свой первичный ключ

        [ForeignKey("User")]
        [Column("userid")]
        public Guid UserId { get; set; }  // Внешний ключ к users

        [Column("fullname")]
        public string FullName { get; set; }

        [Column("address")]
        public string Address { get; set; }

        [Column("contactindo")]
        public string ContactInfo { get; set; }

        [Column("connectiontosystem")]
        public string ConnectionToSystem { get; set; }

        public virtual User User { get; set; }
    }
}