using System.Text.Json.Serialization;
using DriftyApi.Core.Data;
using Microsoft.AspNetCore.Mvc.Formatters;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<InMemoryClientRepository>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// The PATCH endpoint uses JSON Merge Patch (RFC 7386); its media type isn't
// recognised by the JSON input formatter unless added explicitly.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
{
    var jsonInputFormatter = options.InputFormatters.OfType<SystemTextJsonInputFormatter>().First();
    jsonInputFormatter.SupportedMediaTypes.Add("application/merge-patch+json");
});

var app = builder.Build();

app.MapControllers();

app.Run();
