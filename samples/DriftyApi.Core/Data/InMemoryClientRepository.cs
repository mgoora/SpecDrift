using System.Collections.Concurrent;
using DriftyApi.Core.Models;

namespace DriftyApi.Core.Data;

/// <summary>
/// The registry's only store: an in-memory singleton seeded at startup.
/// No database — this fixture exists to be read by Spec Drift, not to run in production.
/// </summary>
public class InMemoryClientRepository
{
    private readonly ConcurrentDictionary<Guid, Client> _clients = new();

    public InMemoryClientRepository()
    {
        foreach (var client in Seed())
        {
            _clients[client.Id] = client;
        }
    }

    public Client? GetById(Guid id)
    {
        return _clients.TryGetValue(id, out var client) ? client : null;
    }

    public Client Add(Client client)
    {
        client.Id = Guid.NewGuid();
        _clients[client.Id] = client;
        return client;
    }

    public Client? FindByLei(string lei)
    {
        return _clients.Values.FirstOrDefault(c => string.Equals(c.Lei, lei, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<Client> Search(string? companyName, string? lei, RiskLevel? riskLevel, KycStatus? kycStatus)
    {
        IEnumerable<Client> query = _clients.Values;

        if (!string.IsNullOrWhiteSpace(companyName))
        {
            query = query.Where(c => c.CompanyName.Contains(companyName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(lei))
        {
            query = query.Where(c => string.Equals(c.Lei, lei, StringComparison.OrdinalIgnoreCase));
        }

        if (riskLevel.HasValue)
        {
            query = query.Where(c => c.RiskLevel == riskLevel.Value);
        }

        if (kycStatus.HasValue)
        {
            query = query.Where(c => c.KycStatus == kycStatus.Value);
        }

        return query.OrderBy(c => c.CompanyName, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<Client> Seed()
    {
        yield return new Client
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CompanyName = "Acme Manufacturing Ltd",
            Lei = "5493001234ABCDEFGH12",
            KycSegment = KycSegment.Corporate,
            KycStatus = KycStatus.Approved,
            AvoxId = "AVX-100234",
            RiskLevel = RiskLevel.Low,
            DateOfIncorporation = new DateTime(2005, 3, 14),
            Documents = new List<Document>
            {
                new() { Name = "Acme_CertOfInc.pdf", Grouping = DocumentGrouping.Identity, Type = DocumentType.CertificateOfIncorporation },
                new() { Name = "Acme_Accounts_2023.pdf", Grouping = DocumentGrouping.Financial, Type = DocumentType.AuditedAccounts }
            }
        };

        yield return new Client
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            CompanyName = "Meridian Capital Partners",
            Lei = "5493005678IJKLMNOP34",
            KycSegment = KycSegment.Institutional,
            KycStatus = KycStatus.InProgress,
            AvoxId = null,
            RiskLevel = RiskLevel.Medium,
            DateOfIncorporation = new DateTime(2012, 7, 1),
            Documents = new List<Document>
            {
                new() { Name = "Meridian_Articles.pdf", Grouping = DocumentGrouping.Ownership, Type = DocumentType.ArticlesOfAssociation }
            }
        };

        yield return new Client
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            CompanyName = "Northwind Trading Co",
            Lei = "5493009012QRSTUVWX56",
            KycSegment = KycSegment.Retail,
            KycStatus = KycStatus.NotStarted,
            AvoxId = null,
            RiskLevel = RiskLevel.Low,
            DateOfIncorporation = new DateTime(2020, 11, 23),
            Documents = new List<Document>()
        };

        yield return new Client
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            CompanyName = "Helios Financial Group",
            Lei = "5493003456YZABCDEF78",
            KycSegment = KycSegment.FinancialInstitution,
            KycStatus = KycStatus.PendingReview,
            AvoxId = "AVX-558812",
            RiskLevel = RiskLevel.High,
            DateOfIncorporation = new DateTime(1998, 1, 9),
            Documents = new List<Document>
            {
                new() { Name = "Helios_W8BEN.pdf", Grouping = DocumentGrouping.Regulatory, Type = DocumentType.W8BEN },
                new() { Name = "Helios_Director_Passport.pdf", Grouping = DocumentGrouping.Identity, Type = DocumentType.PassportCopy },
                new() { Name = "Helios_UtilityBill.pdf", Grouping = DocumentGrouping.Identity, Type = DocumentType.UtilityBill }
            }
        };

        yield return new Client
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            CompanyName = "Vantage Holdings plc",
            Lei = "5493007890GHIJKLMN90",
            KycSegment = KycSegment.Corporate,
            KycStatus = KycStatus.Approved,
            AvoxId = "AVX-770091",
            RiskLevel = RiskLevel.Medium,
            DateOfIncorporation = new DateTime(2010, 5, 30),
            Documents = new List<Document>
            {
                new() { Name = "Vantage_CertOfInc.pdf", Grouping = DocumentGrouping.Identity, Type = DocumentType.CertificateOfIncorporation }
            }
        };

        yield return new Client
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            CompanyName = "Redline Ventures",
            Lei = "5493002468OPQRSTUV12",
            KycSegment = KycSegment.Institutional,
            KycStatus = KycStatus.Rejected,
            AvoxId = null,
            RiskLevel = RiskLevel.Prohibited,
            DateOfIncorporation = new DateTime(2015, 9, 18),
            Documents = new List<Document>
            {
                new() { Name = "Redline_Articles.pdf", Grouping = DocumentGrouping.Ownership, Type = DocumentType.ArticlesOfAssociation },
                new() { Name = "Redline_Accounts_Flagged.pdf", Grouping = DocumentGrouping.Financial, Type = DocumentType.AuditedAccounts }
            }
        };
    }
}
