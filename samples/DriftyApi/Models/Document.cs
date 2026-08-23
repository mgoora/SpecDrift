using System.ComponentModel.DataAnnotations;

namespace DriftyApi.Models
{
    public enum DocumentGrouping
    {
        Identity,
        Ownership,
        Financial,
        Regulatory
    }

    public enum DocumentType
    {
        CertificateOfIncorporation,
        ArticlesOfAssociation,
        AuditedAccounts,
        W8BEN,
        PassportCopy,
        UtilityBill
    }

    public class Document
    {
        [Required]
        [StringLength(int.MaxValue, MinimumLength = 1)]
        public string Name { get; set; }

        public DocumentGrouping Grouping { get; set; }

        public DocumentType Type { get; set; }
    }
}
