using System.Reflection;

namespace Middagsasen.Planner.Api.Core.OpenApi
{
    /// <summary>
    /// Build-time-generering av OpenAPI (Microsoft.Extensions.ApiDescription.Server) kjører appen via verktøyet
    /// <c>GetDocument.Insider</c>, som bygger og starter hosten med en tom server for å hente dokumentet.
    /// Da finnes ingen hemmeligheter eller miljøkonfigurasjon, så oppstartsvalidering av innstillinger må hoppes over.
    /// </summary>
    public static class BuildTimeDocumentGeneration
    {
        private const string ToolAssemblyName = "GetDocument.Insider";

        /// <summary>Om appen kjøres av build-time-genereringen av OpenAPI, ikke som vanlig app.</summary>
        public static bool IsRunning { get; } = Assembly.GetEntryAssembly()?.GetName().Name == ToolAssemblyName;
    }
}
