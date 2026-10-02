using System.Text.Json;

namespace SupportWorkflow;

internal static class AgentResponseParser
{
    public static bool TryDeserializeAgentResponse<T>(string responseText, out T? result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        try
        {
            var json = ExtractFirstJsonObject(responseText);
            result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return result is not null;
        }
        catch
        {
            return false;
        }
    }

    public static string ExtractFirstJsonObject(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException("Response text is empty.");
        }

        int startIndex = responseText.IndexOf('{');
        if (startIndex == -1)
        {
            throw new InvalidOperationException("No JSON object found in response text.");
        }

        int braceCount = 0;
        int endIndex = -1;

        for (int i = startIndex; i < responseText.Length; i++)
        {
            if (responseText[i] == '{')
            {
                braceCount++;
            }
            else if (responseText[i] == '}')
            {
                braceCount--;
                if (braceCount == 0)
                {
                    endIndex = i;
                    break;
                }
            }
        }

        if (endIndex == -1)
        {
            throw new InvalidOperationException("No matching closing brace found in response text.");
        }

        return responseText[startIndex..(endIndex + 1)];
    }
}
