using System.Runtime.CompilerServices;

// Lets tests construct AnthropicLlmClient against a stubbed HttpMessageHandler,
// verifying the request/response contract without ever making a real network call.
[assembly: InternalsVisibleTo("SpecDrift.Core.Tests")]
