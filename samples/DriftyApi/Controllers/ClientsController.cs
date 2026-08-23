using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Web.Http.Description;
using DriftyApi.Data;
using DriftyApi.Models;
using Newtonsoft.Json.Linq;

namespace DriftyApi.Controllers
{
    [RoutePrefix("api/clients")]
    public class ClientsController : ApiController
    {
        private static readonly InMemoryClientRepository SharedRepository = new InMemoryClientRepository();

        private readonly InMemoryClientRepository _repository;

        public ClientsController() : this(SharedRepository)
        {
        }

        public ClientsController(InMemoryClientRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        [Route("{id:guid}")]
        [ResponseType(typeof(Client))]
        public IHttpActionResult GetClient(Guid id)
        {
            var client = _repository.GetById(id);
            if (client == null)
            {
                return Problem(HttpStatusCode.NotFound, "Client not found", $"No client exists with ID '{id}'.");
            }

            return Ok(client);
        }

        [HttpPost]
        [Route("{id:guid}/documents")]
        [ResponseType(typeof(Document))]
        public IHttpActionResult AddClientDocument(Guid id, Document document)
        {
            var client = _repository.GetById(id);
            if (client == null)
            {
                return Problem(HttpStatusCode.NotFound, "Client not found", $"No client exists with ID '{id}'.");
            }

            if (document == null || !ModelState.IsValid)
            {
                return Problem(HttpStatusCode.BadRequest, "Validation failed", DescribeModelErrors());
            }

            client.Documents.Add(document);

            return Ok(document);
        }

        [HttpPost]
        [Route("")]
        [ResponseType(typeof(Client))]
        public IHttpActionResult CreateClient(Client client)
        {
            if (client == null)
            {
                return Problem(HttpStatusCode.BadRequest, "Validation failed", "The request body is missing or is not valid JSON.");
            }

            if (!ModelState.IsValid)
            {
                return Problem(HttpStatusCode.BadRequest, "Validation failed", DescribeModelErrors());
            }

            if (_repository.FindByLei(client.Lei) != null)
            {
                return Problem(HttpStatusCode.Conflict, "Duplicate LEI", $"A client with LEI '{client.Lei}' is already registered.");
            }

            _repository.Add(client);

            var response = Request.CreateResponse(HttpStatusCode.Created, client);
            var locationBuilder = new UriBuilder(Request.RequestUri)
            {
                Path = Request.RequestUri.AbsolutePath.TrimEnd('/') + "/" + client.Id,
                Query = string.Empty
            };
            response.Headers.Location = locationBuilder.Uri;
            return ResponseMessage(response);
        }

        [HttpPatch]
        [Route("{id:guid}")]
        [ResponseType(typeof(Client))]
        public IHttpActionResult UpdateClient(Guid id, JObject patch)
        {
            var client = _repository.GetById(id);
            if (client == null)
            {
                return Problem(HttpStatusCode.NotFound, "Client not found", $"No client exists with ID '{id}'.");
            }

            if (patch == null)
            {
                return Problem(HttpStatusCode.BadRequest, "Validation failed", "The request body is missing or is not valid JSON.");
            }

            ApplyMergePatch(client, patch);

            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(client, new ValidationContext(client), validationResults, true))
            {
                var detail = string.Join(" ", validationResults.Select(r => r.ErrorMessage));
                return Problem(HttpStatusCode.BadRequest, "Validation failed", detail);
            }

            return Ok(client);
        }

        [HttpDelete]
        [Route("{id:guid}")]
        public IHttpActionResult DeleteClient(Guid id)
        {
            var client = _repository.GetById(id);
            if (client == null)
            {
                return Problem(HttpStatusCode.NotFound, "Client not found", $"No client exists with ID '{id}'.");
            }

            // Compliance asked that archived records stay queryable for the retention
            // period, so this archives the client rather than removing it.
            client.KycStatus = KycStatus.Archived;

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [Route("search")]
        [ResponseType(typeof(ClientSearchResult))]
        public IHttpActionResult SearchClients(string companyName = null, string lei = null,
            RiskLevel? riskLevel = null, KycStatus? kycStatus = null, int page = 1, int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(lei))
            {
                return Problem(HttpStatusCode.BadRequest, "Validation failed", "lei is required.");
            }

            if (page < 1)
            {
                return Problem(HttpStatusCode.BadRequest, "Validation failed", "page must be at least 1.");
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return Problem(HttpStatusCode.BadRequest, "Validation failed", "pageSize must be between 1 and 100.");
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
        private static void ApplyMergePatch(Client client, JObject patch)
        {
            if (patch.Property("companyName") != null) client.CompanyName = patch["companyName"].Value<string>();
            if (patch.Property("lei") != null) client.Lei = patch["lei"].Value<string>();
            if (patch.Property("kycSegment") != null) client.KycSegment = patch["kycSegment"].ToObject<KycSegment>();
            if (patch.Property("kycStatus") != null) client.KycStatus = patch["kycStatus"].ToObject<KycStatus>();
            if (patch.Property("avoxId") != null) client.AvoxId = patch["avoxId"].Type == JTokenType.Null ? null : patch["avoxId"].Value<string>();
            if (patch.Property("riskLevel") != null) client.RiskLevel = patch["riskLevel"].ToObject<RiskLevel>();
            if (patch.Property("dateOfIncorporation") != null) client.DateOfIncorporation = patch["dateOfIncorporation"].Value<DateTime>();
        }

        private string DescribeModelErrors()
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return string.Join(" ", errors);
        }

        private IHttpActionResult Problem(HttpStatusCode statusCode, string title, string detail)
        {
            var problem = new ProblemDetails
            {
                Title = title,
                Status = (int)statusCode,
                Detail = detail
            };

            var response = Request.CreateResponse(statusCode, problem);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/problem+json");
            return ResponseMessage(response);
        }
    }
}
