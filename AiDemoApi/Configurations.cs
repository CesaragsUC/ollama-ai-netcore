using AiDemoApi.Controllers;
using System.Collections.Concurrent;

namespace AiDemoApi;

public interface IChatStore
{
    List<SendPromptDto> Get(string conversationId);
    void Append(string conversationId, SendPromptDto message);
}

// implementação simples em memória (reinicia ao derrubar a API)
public sealed class InMemoryChatStore : IChatStore
{
    private readonly ConcurrentDictionary<string, List<SendPromptDto>> _mem = new();

    public List<SendPromptDto> Get(string conversationId) =>
        _mem.GetOrAdd(conversationId, _ => new List<SendPromptDto>());

    public void Append(string conversationId, SendPromptDto message) =>
        Get(conversationId).Add(message);
}