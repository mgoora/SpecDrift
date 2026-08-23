using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using DriftyApi.Core.Data;
using DriftyApi.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace DriftyApi.Core.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly InMemoryClientRepository _repository;

    public ClientsController(InMemoryClientRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Client), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Client> GetClient(Guid id)
    {
        var client = _repository.GetById(id);
        if (client == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Client not found",
                detail: $"No client exists with ID '{id}'.");
        }

        return Ok(client);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Client), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<Client> CreateClient(Client client)
    {
        if (_repository.FindByLei(client.Lei) is not null)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Duplicate LEI",
                detail: $"A client with LEI '{client.Lei}' is already registered.");
        }

        _repository.Add(client);

        return CreatedAtAction(nameof(GetClient), new { id = client.Id }, client);
    }

    /// <summary>
    /// Not documented in the spec — added straight to the controller during maintenance.
    /// </summary>
    [HttpPost("{id:guid}/documents")]
    [ProducesResponseType(typeof(Document), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<Document> AddClientDocument(Guid id, Document document)
    {
        var client = _repository.GetById(id);
        if (client == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Client not found",
                detail: $"No client exists with ID '{id}'.");
        }

        client.Documents.Add(document);

        return Ok(document);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(Client), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Client> UpdateClient(Guid id, [FromBody] JsonObject patch)
    {
        var client = _repository.GetById(id);
        if (client == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Client not found",
                detail: $"No client exists with ID '{id}'.");
        }

        ApplyMergePatch(client, patch);

        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(client, new ValidationContext(client), validationResults, true))
        {
            var detail = string.Join(" ", validationResults.Select(r => r.ErrorMessage));
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation failed", detail: detail);
        }

        return Ok(client);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DeleteClient(Guid id)
    {
        var client = _repository.GetById(id);
        if (client == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Client not found",
                detail: $"No client exists with ID '{id}'.");
        }

        // Compliance asked that archived records stay queryable for the retention
        // period, so this archives the client rather than removing it.
        client.KycStatus = KycStatus.Archived;

        return NoContent();
    }

    [HttpPost("search")]
    [ProducesResponseType(typeof(ClientSearchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ClientSearchResult> SearchClients(string? companyName = null, string? lei = null,
        RiskLevel? riskLevel = null, KycStatus? kycStatus = null, int page = 1, int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(lei))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation failed",
                detail: "lei is required.");
        }

        if (page < 1)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation failed",
                detail: "page must be at least 1.");
        }

        if (pageSize is < 1 or > 100)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation failed",
                detail: "pageSize must be between 1 and 100.");
        }

        var matches = _repository.Search(companyName, lei, riskLevel, kycStatus).ToList();

        var pageItems = matches
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = new ClientSearchResult
        {
            Items = pageItems,
            TotalCount = matches.Count,
            Page = page,
            PageSize = pageSize
        };

        return Ok(result);
    }

    /// <summary>
    /// JSON Merge Patch (RFC 7386): a property present in the patch overwrites the
    /// field, including to null where the field is nullable. A property absent from
    /// the patch leaves the existing field untouched.
    /// </summary>
    private static void ApplyMergePatch(Client client, JsonObject patch)
    {
        if (patch.TryGetPropertyValue("companyName", out var companyName)) client.CompanyName = companyName!.GetValue<string>();
        if (patch.TryGetPropertyValue("lei", out var lei)) client.Lei = lei!.GetValue<string>();
        if (patch.TryGetPropertyValue("kycSegment", out var kycSegment)) client.KycSegment = Enum.Parse<KycSegment>(kycSegment!.GetValue<string>());
        if (patch.TryGetPropertyValue("kycStatus", out var kycStatus)) client.KycStatus = Enum.Parse<KycStatus>(kycStatus!.GetValue<string>());
        if (patch.TryGetPropertyValue("avoxId", out var avoxId)) client.AvoxId = avoxId?.GetValue<string>();
        if (patch.TryGetPropertyValue("riskLevel", out var riskLevel)) client.RiskLevel = Enum.Parse<RiskLevel>(riskLevel!.GetValue<string>());
        if (patch.TryGetPropertyValue("dateOfIncorporation", out var dateOfIncorporation)) client.DateOfIncorporation = DateTime.Parse(dateOfIncorporation!.GetValue<string>());
    }
}
