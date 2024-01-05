using System.Threading.Tasks;
using Telegram.Bot.Tests.Integ.Framework;
using Telegram.Bot.Types;
using Xunit;

namespace Telegram.Bot.Tests.Integ.Update_Messages;

[Collection(Constants.TestCollections.DeleteMessage2)]
[TestCaseOrderer(Constants.TestCaseOrderer, Constants.AssemblyName)]
public class DeleteMessageTests2(TestsFixture fixture)
{
    ITelegramBotClient BotClient => _fixture.BotClient;

    readonly TestsFixture _fixture = fixture;


    [OrderedFact("Should delete message")]
    [Trait(Constants.MethodTraitName, Constants.TelegramBotApiMethods.SendMessage)]
    [Trait(Constants.MethodTraitName, Constants.TelegramBotApiMethods.DeleteMessage)]
    public async Task Should_Delete_Message()
    {
        Message message = await BotClient.SendTextMessageAsync(
            chatId: _fixture.SupergroupChat.Id,
            text: "This message will be deleted shortly"
        );

        await Task.Delay(1_000);

        await BotClient.DeleteMessageAsync(
            chatId: message.Chat.Id,
            messageId: message.MessageId
        );
    }

    [OrderedFact("Should delete messages")]
    [Trait(Constants.MethodTraitName, Constants.TelegramBotApiMethods.SendMessage)]
    [Trait(Constants.MethodTraitName, Constants.TelegramBotApiMethods.DeleteMessages)]
    public async Task Should_Delete_Messages()
    {
        Message message1 = await BotClient.SendTextMessageAsync(
            chatId: _fixture.SupergroupChat.Id,
            text: "Message one.\nThis message will be deleted shortly"
        );

        Message message2 = await BotClient.SendTextMessageAsync(
            chatId: _fixture.SupergroupChat.Id,
            text: "Message two.\nThis message will be deleted shortly"
        );

        int[] messageIds = [message1.MessageId, message2.MessageId];

        await Task.Delay(1_000);

        await BotClient.DeleteMessagesAsync(
            chatId: _fixture.SupergroupChat.Id,
            messageIds: messageIds
        );
    }

}
