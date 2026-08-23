using System.ComponentModel.DataAnnotations;

namespace DriftyApi.Core.Models;

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
    [MinLength(1)]
    public string Name { get; set; } = string.Empty;

    public DocumentGrouping Grouping { get; set; }

    public DocumentType Type { get; set; }
}
