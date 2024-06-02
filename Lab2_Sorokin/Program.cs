using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI_API;
using OpenAI_API.Chat;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    private static readonly HttpClient HttpClient = new HttpClient();

    private static readonly dynamic config = JsonConvert.DeserializeObject(System.IO.File.ReadAllText(@"..\..\..\config.json"));
    private static readonly TelegramBotClient Bot = new TelegramBotClient((string)config.TelegramBotToken);
    private static readonly string OpenAIApiKey = (string)config.OpenAIApiKey;
    private static readonly string WeatherApiKey = (string)config.WeatherApiKey;

    private static string CurrentCommand = "";

    static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = { } // Получение всех типов обновлений
        };

        Bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cancellationToken);

        Console.WriteLine("Press any key to exit");
        Console.ReadKey();
        cts.Cancel();
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message && update.Message.Type == MessageType.Text)
        {
            var chatId = update.Message.Chat.Id;
            var messageText = update.Message.Text;

            if (messageText == "/start")
            {
                CurrentCommand = "";
                var replyKeyboard = new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "Weather Info", "Car Info" }
                })
                {
                    ResizeKeyboard = true
                };

                await botClient.SendTextMessageAsync(chatId, "Welcome to the Bot! Use the buttons below to navigate.", replyMarkup: replyKeyboard);
            }
            else if (messageText == "/help")
            {
                CurrentCommand = "";
                await botClient.SendTextMessageAsync(chatId, "/start - Start the bot\n/help - Show this help message\nWeather Info - Get weather information for a city\nCar Info - Get information about a car using OpenAI");
            }
            else if (messageText == "Weather Info")
            {
                CurrentCommand = "Weather";
                await botClient.SendTextMessageAsync(chatId, "Please enter the name of the city to get weather information.");
            }
            else if (messageText == "Car Info")
            {
                CurrentCommand = "Car";
                await botClient.SendTextMessageAsync(chatId, "Please enter the make and model of the car to get information about it.");
            }
            else if (CurrentCommand == "Weather")
            {
                var weatherInfo = await GetWeatherInfoAsync(messageText);
                await botClient.SendTextMessageAsync(chatId, $"Weather Info:\n{weatherInfo}");
                CurrentCommand = "";
            }
            else if (CurrentCommand == "Car")
            {
                var carInfo = await GetCarInfoAsync(messageText);
                await botClient.SendTextMessageAsync(chatId, $"Car Info:\n{carInfo}");
                CurrentCommand = "";
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Sorry, I didn't understand that command.");
            }
        }
    }

    private static async Task<string> GetWeatherInfoAsync(string cityName)
    {
        try
        {
            var response = await HttpClient.GetStringAsync($"http://api.openweathermap.org/data/2.5/weather?q={cityName}&appid={WeatherApiKey}&units=metric");
            var weatherData = JObject.Parse(response);
            var description = weatherData["weather"][0]["description"].ToString();
            var temperature = weatherData["main"]["temp"].ToString();

            return $"Current weather in {cityName}: {description}, {temperature}°C";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling Weather API: {ex.Message}");
            return "Sorry, there was an error retrieving the weather information.";
        }
    }

    private static async Task<string> GetCarInfoAsync(string carQuery)
    {
        var api = new OpenAIAPI(OpenAIApiKey);
        string prompt = $"Tell me about the car {carQuery}, including its engine, transmission, and other specifications.";

        var chatRequest = new ChatRequest
        {
            Model = "gpt-3.5-turbo",
            Messages = new[]
            {
                new ChatMessage(ChatMessageRole.System, "You are a helpful assistant."),
                new ChatMessage(ChatMessageRole.User, prompt)
            },
            MaxTokens = 300,
            Temperature = 0.7
        };

        try
        {
            var chatResponse = await api.Chat.CreateChatCompletionAsync(chatRequest);
            if (chatResponse.Choices == null || chatResponse.Choices.Count == 0)
            {
                return "Sorry, I couldn't retrieve information about that car.";
            }

            var responseText = chatResponse.Choices[0].Message.Content.Trim();
            return responseText;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling OpenAI API: {ex.Message}");
            return "Sorry, there was an error retrieving the information.";
        }
    }

    private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine(exception.Message);
        return Task.CompletedTask;
    }
}