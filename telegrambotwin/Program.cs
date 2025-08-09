using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Microsoft.EntityFrameworkCore;
using System.Net.WebSockets;
using System.Diagnostics;
using Telegram.Bot.Types.ReplyMarkups;
using System.Collections.Generic;
using System.Drawing.Imaging;
using telegrambotwin;

class Program
{
    static char prefix = '/';
    static string pathDownload = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Games_4pda");
    static string authorid = "7082333427";
    static Dictionary<long, string> awaitingAdminReply = new();
    private static Dictionary<long, long> selectedChats = new();
    
    static async Task Main()
    {
        var botClient = new TelegramBotClient(info.keybot);

        using var cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await botClient.GetMe();
        Console.WriteLine($"Бот запущен: @{me.Username}");
        Console.ReadLine();

        cts.Cancel();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        
        using var db = new BotDbContext();
        db.Database.EnsureCreated();
        if (update.MyChatMember != null)
        {
            var newStatus = update.MyChatMember.NewChatMember.Status;
            var oldStatus = update.MyChatMember.OldChatMember.Status;
            var botId = update.MyChatMember.NewChatMember.User.Id;


            if ((newStatus == ChatMemberStatus.Kicked || newStatus == ChatMemberStatus.Left)
                && update.MyChatMember.From.Id == botId)
            {
                var leftChatId = update.MyChatMember.Chat.Id;

                var chatToRemove = db.BotChats.FirstOrDefault(c => c.ChatId == leftChatId);
                if (chatToRemove != null)
                {
                    db.BotChats.Remove(chatToRemove);
                    await db.SaveChangesAsync();
                }

                Console.WriteLine($"Бот покинул чат {leftChatId}, удалено из БД");
            }

            return;
        }
        
            
            
        
        
        if (update.CallbackQuery is { } callbackQuery)
        {
            var callbackData = callbackQuery.Data;
            var fromId = callbackQuery.From.Id;



            var admin = db.Users.FirstOrDefault(u => u.TelegramId == fromId.ToString());

            if (admin == null || admin.Role != "admin")
            {
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "Недостаточно прав");
                return;
            }
            if (update.CallbackQuery?.Data?.StartsWith("answerchat:") == true)
            {
                var data = update.CallbackQuery.Data;
                var selectedChatId = long.Parse(data.Split(':')[1]);


                selectedChats[update.CallbackQuery.From.Id] = selectedChatId;

                await botClient.SendMessage(
                    update.CallbackQuery.Message.Chat.Id,
                    $"Теперь отправьте сообщение, которое хотите отправить в чат {selectedChatId}",
                    cancellationToken: cancellationToken
                );

                return;
            }

            if (callbackData.StartsWith("reply:"))
            {
                var targetId = callbackData.Split(':')[1];
                awaitingAdminReply[fromId] = targetId;

                await botClient.SendMessage(fromId, $"Введите ответ для пользователя (id: {targetId}):");
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "Ожидаю сообщение для ответа");
                return;
            }

            if (callbackData.StartsWith("ban:"))
            {
                var targetId = callbackData.Split(':')[1];
                var user1 = db.Users.FirstOrDefault(u => u.TelegramId == targetId);
                if (user1 != null)
                {
                    user1.Role = "banned";
                    await db.SaveChangesAsync();
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "Пользователь забанен");
                }
            }
            else if (callbackData.StartsWith("role:"))
            {
                var parts = callbackData.Split(':');
                var targetId = parts[1];
                var newRole = parts[2];
                var user1 = db.Users.FirstOrDefault(u => u.TelegramId == targetId);
                if (user1 != null)
                {
                    if (newRole == "admin")
                    {
                        if (admin.TelegramId != authorid) { return; }
                    }
                    user1.Role = newRole;
                    await db.SaveChangesAsync();
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, $"Роль изменена на {newRole}");
                }
            }

            return;
        }
        
        if (update.Message is not { Text: { } messageText } )
            return;


        var chatId = update.Message.Chat.Id;
        var username = update.Message.Chat.Username ?? " ";





        var user = db.Users.FirstOrDefault(u => u.TelegramId == $"{update.Message.From.Id}");

        if (user == null)
        {
            user = new UserInfo
            {
                Id = Guid.NewGuid().ToString(),
                TelegramId = $"{update.Message.From.Id}",
                Username = username,
                Role = "user"
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }



        if (user.Role == "banned") { return; }
        if (selectedChats.TryGetValue(Convert.ToInt64(user.TelegramId), out var chatToSend))
        {
            await botClient.SendMessage(chatToSend, messageText, cancellationToken: cancellationToken);

            await botClient.SendMessage(
                chatId: chatId,
                text: "✅ Сообщение отправлено",
                cancellationToken: cancellationToken
            );

            selectedChats.Remove(Convert.ToInt64(user.TelegramId)); // Очистка
            return;
        }
        Console.WriteLine($"{user.TelegramId}:{messageText}");








        if (messageText.StartsWith($"{prefix}users"))
        {
            if (user.Role != "admin")
            {
                await botClient.SendMessage(chatId, "иди нахуй!", cancellationToken: cancellationToken);
                return;
            }

            var searchuser = messageText.Length > $"{prefix}users ".Length
                ? messageText.Substring($"{prefix}users ".Length).Trim()
                : null;

            if (!string.IsNullOrEmpty(searchuser))
            {
                var loadingMsg = await botClient.SendMessage(chatId, $"поиск[{searchuser}]...", cancellationToken: cancellationToken);

                var users = db.Users
                    .Where(u =>
                        EF.Functions.Like(u.Id, $"%{searchuser}%") ||
                        EF.Functions.Like(u.TelegramId, $"%{searchuser}%") ||
                        EF.Functions.Like(u.Role, $"%{searchuser}%"))
                    .ToList();

                await botClient.DeleteMessage(chatId, loadingMsg.MessageId, cancellationToken);

                if (users.Any())
                {
                    foreach (var u in users)
                    {

                        await botClient.SendMessage(
                            chatId: chatId,
                            text: $"айди(бот): {u.Id}, айди(тг): {u.TelegramId}, имя: @{u.Username}, роль: {u.Role}",
                            replyMarkup: new InlineKeyboardMarkup(new[]
                            {
                                new []
                                {
                                    InlineKeyboardButton.WithCallbackData("Забанить", $"ban:{u.TelegramId}"),
                                    InlineKeyboardButton.WithCallbackData("Сделать админом", $"role:{u.TelegramId}:admin"),
                                    InlineKeyboardButton.WithCallbackData("Сделать юзером", $"role:{u.TelegramId}:user")
                                }
                            }),
                            cancellationToken: cancellationToken
                        );

                    }

                }
                else
                {
                    await botClient.SendMessage(chatId, "404 (гомяк не найден)", cancellationToken: cancellationToken);
                }
            }
            else
            {
                await botClient.SendMessage(chatId, "404 (гомяк не найден)", cancellationToken: cancellationToken);
            }

            return;
        }


        if (messageText.StartsWith($"{prefix}support"))
        {
            await botClient.SendMessage(chatId, "ожидайте ответ", cancellationToken: cancellationToken);

            var admins = db.Users.Where(u => u.Role == "admin").ToList();
            var supportText = messageText.Substring($"{prefix}support".Length).Trim();

            foreach (var admin in admins)
            {
                var msg = user.Username == " "
                    ? $"юзер {user.TelegramId} (роль: {user.Role}): {supportText}"
                    : $"юзер @{user.Username} (роль: {user.Role}): {supportText}";

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Ответить", $"reply:{user.TelegramId}"),
                        InlineKeyboardButton.WithCallbackData("Забанить", $"ban:{user.TelegramId}")
                    }
                });

                await botClient.SendMessage(
                    chatId: admin.TelegramId,
                    text: msg,
                    replyMarkup: inlineKeyboard,
                    cancellationToken: cancellationToken
                );
            }


            return;
        }


        if (messageText.StartsWith($"{prefix}reply"))
        {
            if (user.Role != "admin")
            {
                await botClient.SendMessage(chatId, "иди нахуй!", cancellationToken: cancellationToken);
                return;
            }
            if (messageText.Length > $"{prefix}reply".Length)
            {
                var parts = messageText.Split(' ');
                if (parts.Length < 3)
                {
                    await botClient.SendMessage(chatId, $"{prefix}reply <id> <сообщение>", cancellationToken: cancellationToken);
                    return;
                }

                var targetId = parts[1];
                var replyText = messageText.Substring($"{prefix}reply {targetId}".Length).Trim();
                bool tryorcatch = false;
                try
                {
                    await botClient.SendMessage(targetId, $"вам пришло сообщение от админа\n[{user.Id}]]\n[{replyText}]", cancellationToken: cancellationToken);


                }
                catch
                {
                    await botClient.SendMessage(chatId, "Ошибка отправки сообщения", cancellationToken: cancellationToken);
                    tryorcatch = true;
                }
                if (tryorcatch == false)
                {
                    await botClient.SendMessage(chatId, "отправленно!", cancellationToken: cancellationToken);
                }
                return;
            }

            var chats = db.BotChats.ToList();

            if (!chats.Any())
            {
                await botClient.SendMessage(chatId, "Нет доступных чатов", cancellationToken: cancellationToken);
                return;
            }

            // Создаём Inline кнопки по чатам
            var buttons = chats.Select(c => new[]
            {
                InlineKeyboardButton.WithCallbackData($"{c.Title} ({c.ChatId})", $"answerchat:{c.ChatId}")
            }).ToArray();

            await botClient.SendMessage(
                chatId,
                "Выбери чат, чтобы ответить:",
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken
            );

            return;
        }
        if (messageText.StartsWith($"{prefix}answer"))
        {
            if (user.TelegramId != authorid)
            {
                await botClient.SendMessage(chatId, "иди нахуй!", cancellationToken: cancellationToken);
                return;
            }
            if (messageText.Length > $"{prefix}answer".Length)
            {
                var parts = messageText.Split(' ');
                if (parts.Length < 3)
                {
                    await botClient.SendMessage(chatId, $"{prefix}answer <id> <сообщение>", cancellationToken: cancellationToken);
                    return;
                }

                var targetId = parts[1];
                var replyText = messageText.Substring($"{prefix}answer {targetId}".Length).Trim();
                bool tryorcatch = false;
                try
                {
                    await botClient.SendMessage(targetId, $"{replyText}", cancellationToken: cancellationToken);


                }
                catch
                {
                    await botClient.SendMessage(chatId, "Ошибка отправки сообщения", cancellationToken: cancellationToken);
                    tryorcatch = true;
                }
                if (tryorcatch == false)
                {
                    await botClient.SendMessage(chatId, "отправленно!", cancellationToken: cancellationToken);
                }
                return;
            }

            var chats = db.BotChats.ToList();

            if (!chats.Any())
            {
                await botClient.SendMessage(chatId, "Нет доступных чатов", cancellationToken: cancellationToken);
                return;
            }

            // Создаём Inline кнопки по чатам
            var buttons = chats.Select(c => new[]
            {
                InlineKeyboardButton.WithCallbackData($"{c.Title} ({c.ChatId})", $"answerchat:{c.ChatId}")
            }).ToArray();

            await botClient.SendMessage(
                chatId,
                "Выбери чат, чтобы ответить:",
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken
            );

            return;
        }


        if (messageText.StartsWith($"{prefix}get"))
        {
            if (user.TelegramId != authorid)
            {
                await botClient.SendMessage(chatId, "иди нахуй!", cancellationToken: cancellationToken);
                return;
            }
            user.Role = messageText.Split(' ').Last();
            await db.SaveChangesAsync();
            await botClient.SendMessage(chatId, $"выданна роль {messageText.Split(' ').Last()}", cancellationToken: cancellationToken);
            return;
        }

        if (messageText.StartsWith($"{prefix}help"))
        {
            if (user.TelegramId == authorid) 
            {
                await botClient.SendMessage(chatId, $"{prefix}get [роль] : выдать себе роль", cancellationToken: cancellationToken);
                await botClient.SendMessage(chatId, $"{prefix}answer [айди] [сообщение]: написать человеку(анонимно)", cancellationToken: cancellationToken);
            }
            if(user.Role == "admin")
            {
                await botClient.SendMessage(chatId, $"{prefix}users [айди,роль,имя] : найти юзера", cancellationToken: cancellationToken);
                await botClient.SendMessage(chatId, $"{prefix}reply [айди] [сообщение] : написать человеку", cancellationToken: cancellationToken);
            }
            await botClient.SendMessage(chatId, $"{prefix}help : выводит это сообщение", cancellationToken: cancellationToken);
            await botClient.SendMessage(chatId, $"{prefix}support : написать админам", cancellationToken: cancellationToken);
            await botClient.SendMessage(chatId, $"[сообщение] : ответ на любой вопрос от ии", cancellationToken: cancellationToken);
            return;
        }

        if (awaitingAdminReply.TryGetValue(chatId, out var targetUserId))
        {
            try
            {
                await botClient.SendMessage(long.Parse(targetUserId), $"✉️ Сообщение от админа: {messageText}", cancellationToken: cancellationToken);
                await botClient.SendMessage(chatId, "✅ Ответ отправлен", cancellationToken: cancellationToken);
            }
            catch
            {
                await botClient.SendMessage(chatId, "❌ Ошибка отправки", cancellationToken: cancellationToken);
            }

            awaitingAdminReply.Remove(chatId);
            return;
        }

        if (messageText.StartsWith(prefix))
        {
            await botClient.SendMessage(chatId, $"Команда не найдена (попробуйте '{prefix}help')", cancellationToken: cancellationToken);
            return;
        }

        string reply = await func.RequestGoogle(messageText, user.TelegramId);
        
        await botClient.SendMessage(
            chatId: update.Message.Chat.Id,
            text: reply,
            replyParameters: update.Message.MessageId, 
            cancellationToken: cancellationToken
            , parseMode: ParseMode.Html
        );
    }



    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}
