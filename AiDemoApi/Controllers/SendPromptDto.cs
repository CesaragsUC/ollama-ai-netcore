namespace AiDemoApi.Controllers;

public sealed record SendPromptDto(string ConversationId, string Prompt, string? Assistent);