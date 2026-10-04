using System.Text.Json;

namespace MafWorkflow.Worker.Core;

public static class AgentResponseParser
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
        bool inString = false;
        bool isEscaped = false;

        for (int i = startIndex; i < responseText.Length; i++)
        {
            char c = responseText[i];

            if (inString)
            {
                if (isEscaped)
                {
                    isEscaped = false;
                }
                else if (c == '\\')
                {
                    isEscaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (c == '"')
            {
                inString = true;
            }
            else if (c == '{')
            {
                braceCount++;
            }
            else if (c == '}')
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
