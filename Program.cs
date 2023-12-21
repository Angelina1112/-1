using System;
using System.Data.SqlClient;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Telegram.Bot;
using Telegram.Bot.Types;

class Program
{
    static async Task Main()
    {
        string connectionString = "Server=127.0.0.1;Database=сообщения;User ID=root;Password="; // Замените на свою строку подключения
        string token = "6595144680:AAFWKIQTc28EggPy3hH_73w1P2ez3nWgBcg"; // Вставьте свой токен бота
        string chatId = "-1001865315808"; // Вставьте ID вашего канала

        TelegramBotClient bot = new TelegramBotClient(token);

        while (true)
        {
            if (DateTime.Now.Minute == 52 && DateTime.Now.Second == 0)
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    try
                    {
                        await ProcessTelegramMessagesAsync(bot, chatId, connection);
                        await ProcessTelegramMessagesAsync1(bot, chatId, connection);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                }

                await Task.Delay(TimeSpan.FromHours(1));
            }

            await Task.Delay(1000);
        }
    }

    static long lastProcessedUpdateId = 0;

    static async Task ProcessTelegramMessagesAsync(TelegramBotClient bot, string chatId, MySqlConnection connection)
    {
        connection.Open();

        // Получаем текущее количество сообщений из базы данных
        int messageCount = GetMessageCountFromDatabase(connection);


        // Получаем обновления из группы в Telegram
        var updates = await bot.GetUpdatesAsync(offset: (int?)(lastProcessedUpdateId + 1));

        foreach (var update in updates)
        {
            if (update.Message != null && update.Message.Chat.Id.ToString() == chatId)
            {
                // Обновляем ID последнего обработанного обновления
                lastProcessedUpdateId = update.Id;
            }

            if (update.Message != null && update.Message.Chat.Id.ToString() == chatId && update.Message.From != null && !update.Message.From.IsBot)
            {
                // Если сообщение из группы и не от бота, добавляем его в базу данных
                string newMessage = $"{update.Message.From.FirstName} {update.Message.From.LastName}: {update.Message.Text}";
                DateTime sentAt = update.Message.Date.ToLocalTime();  // Используем временную метку сообщения
                InsertMessageIntoDatabase(connection, newMessage, sentAt);

                // Обновляем счетчик сообщений в базе данных
                UpdateMessageCountInDatabase(connection, messageCount + 1);
            }
        }
        connection.Close();
    }
    static async Task ProcessTelegramMessagesAsync1(TelegramBotClient bot, string chatId, MySqlConnection connection)
    {
        connection.Open();

        // Получаем текущее количество сообщений из базы данных
        int messageCount = GetMessageCountFromDatabase(connection);
        string newMessage1 = $"Количество сообщений в группе: {messageCount}";
        await bot.SendTextMessageAsync(chatId, newMessage1);
        connection.Close();
    }

    private static void InsertMessageIntoDatabase(MySqlConnection connection, string message, DateTime sentAt)
    {
        // Проверяем, есть ли такое сообщение с таким временем отправки уже в базе данных
        if (!IsMessageAlreadyStored(connection, message, sentAt))
        {
            string insertQuery = "INSERT INTO MessageLog (message, sent_at) VALUES (@message, @sent_at)";

            using (MySqlCommand insertCommand = new MySqlCommand(insertQuery, connection))
            {
                insertCommand.Parameters.AddWithValue("@message", message);
                insertCommand.Parameters.AddWithValue("@sent_at", sentAt);
                insertCommand.ExecuteNonQuery();
            }
        }
        else
        {
            Console.WriteLine($"Message already exists in the database: {message}, sent at {sentAt}");
        }
    }

    private static bool IsMessageAlreadyStored(MySqlConnection connection, string message, DateTime sentAt)
    {
        string query = "SELECT COUNT(*) FROM MessageLog WHERE message = @message AND sent_at = @sent_at";

        using (MySqlCommand command = new MySqlCommand(query, connection))
        {
            command.Parameters.AddWithValue("@message", message);
            command.Parameters.AddWithValue("@sent_at", sentAt);
            int count = Convert.ToInt32(command.ExecuteScalar());
            return count > 0;
        }
    }

    private static int GetMessageCountFromDatabase(MySqlConnection connection)
    {
        string query = "SELECT COUNT(*) FROM MessageLog WHERE sent_at";

        using (MySqlCommand command = new MySqlCommand(query, connection))
        {
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    private static void UpdateMessageCountInDatabase(MySqlConnection connection, int newCount)
    {
        string updateQuery = $"UPDATE MessageCount SET Count = {newCount}";

        using (MySqlCommand updateCommand = new MySqlCommand(updateQuery, connection))
        {
            updateCommand.ExecuteNonQuery();
        }
    }
}
