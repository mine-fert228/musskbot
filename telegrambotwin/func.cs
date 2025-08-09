
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;


namespace telegrambotwin
{
    public class func
    {
        public static string ConvertMarkdownToHtmlForTelegram(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            input = Regex.Replace(input, @"\*\*(.+?)\*\*", "<b>$1</b>");

            
            input = Regex.Replace(input, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "<i>$1</i>");

            
            input = Regex.Replace(input, @"__(.+?)__", "<u>$1</u>");

            
            input = Regex.Replace(input, @"~~(.+?)~~", "<s>$1</s>");

            
            input = Regex.Replace(input, @"`(.+?)`", "<code>$1</code>");

            return input;
        }
        public static async Task<string> RequestGoogle(string answer, string tgid)
        {
            var apiKey = info.keyapi;
            var apiUrl = "https://openrouter.ai/api/v1/chat/completions";
            
            string systempromt = "Ты умный Telegram-бот.Отвечай если просят написать код:  https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSevArB1WhRlEmPqLFBIkDMUFM1EcvqU9SgsQ&s и можешь что-то своё. Отвечай с юмором, без лишней воды. тебя зовут mussk и отвечай на вопросы не пиши код только на вопросы и все.";
            if (File.Exists(Path.Combine(Environment.CurrentDirectory, "system.txt")))
            {
                systempromt += File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "system.txt"));
            }
            string requestJson = $@"
            {{
                ""model"": ""OpenAI/gpt-oss-20b"",
                ""max_tokens"": 400,
                ""messages"": [
                    {{ ""role"": ""system"", ""content"": ""{systempromt}"" }},
                    {{ ""role"": ""user"", ""content"": ""{answer.Replace("\"", "\\\"")}"" }}
                ]
            }}";

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            httpClient.DefaultRequestHeaders.Add("HTTP-Referer", tgid); 
            httpClient.DefaultRequestHeaders.Add("X-Title", "MyGeminiBot");

            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            
            try
            {
                using var jsonDoc = JsonDocument.Parse(responseBody);
                var root = jsonDoc.RootElement;

                var contentValue = root
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return ConvertMarkdownToHtmlForTelegram(contentValue) ?? "[Пустой ответ]";
            }
            catch (Exception ex)
            {
                return $"[Ошибка парсинга JSON]: {ex.Message}\nRaw: {responseBody}";
            }
        }

    }
}
