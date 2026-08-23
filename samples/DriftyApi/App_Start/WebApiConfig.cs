using System.Net.Http.Headers;
using System.Web.Http;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace DriftyApi.App_Start
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.MapHttpAttributeRoutes();

            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            json.SerializerSettings.Converters.Add(new StringEnumConverter());

            // The PATCH endpoint uses JSON Merge Patch (RFC 7386); its media type
            // isn't recognised by the JSON formatter unless added explicitly.
            json.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/merge-patch+json"));

            config.Formatters.Remove(config.Formatters.XmlFormatter);
        }
    }
}
