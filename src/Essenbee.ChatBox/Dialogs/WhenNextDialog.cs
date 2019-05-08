﻿using Essenbee.ChatBox.Cards;
using Essenbee.ChatBox.Core.Interfaces;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Essenbee.ChatBox.Dialogs
{
    public class WhenNextDialog : ComponentDialog
    {
        public IStatePropertyAccessor<UserSelections> UserSelectionsState;
        private readonly IChannelClient _client;

        public WhenNextDialog(string dialogId, IStatePropertyAccessor<UserSelections> userSelectionsState,
            IChannelClient client) : base(dialogId)
        {
            UserSelectionsState = userSelectionsState;
            _client = client;

            var whenNextSteps = new WaterfallStep[]
            {
                GetUsersTimezoneStepAsync,
                GetStreamerNameStepAsync,
                GetStreamerInfoStepAsync,
            };

            AddDialog(new WaterfallDialog("whenNextIntent", whenNextSteps));
            AddDialog(new SetTimezoneDialog("setTimezoneIntent", UserSelectionsState));
            AddDialog(new TextPrompt("streamer-name"));
        }

        private async Task<DialogTurnResult> GetUsersTimezoneStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userSelections = await UserSelectionsState.GetAsync(stepContext.Context, () => new UserSelections(), cancellationToken);

            if (!string.IsNullOrWhiteSpace(userSelections.TimeZone))
            {
                return await stepContext.NextAsync();
            }

            return await stepContext.BeginDialogAsync("setTimezoneIntent", cancellationToken);
        }

        private async Task<DialogTurnResult> GetStreamerNameStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken) => await stepContext.PromptAsync("streamer-name", new PromptOptions
        {
            Prompt = MessageFactory.Text("Please enter the name of the streamer you are interested in")
        },
                cancellationToken);

        private async Task<DialogTurnResult> GetStreamerInfoStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userSelections = await UserSelectionsState.GetAsync(stepContext.Context, () => new UserSelections(), cancellationToken);
            userSelections.StreamerName = (string)stepContext.Result;

            await stepContext.Context.SendActivityAsync(ActivityTypes.Typing);

            var streamName = userSelections.StreamerName.ToLower()
                .Replace(" ", string.Empty);

            try
            {
                var channel = await _client.GetChannelByName(streamName, userSelections.TimeZone);

                if (channel != null)
                {
                    var reply = stepContext.Context.Activity.CreateReply();
                    reply.Attachments = new List<Attachment> { ChannelDataCard.Create(channel) };
                    await stepContext.Context.SendActivityAsync(reply, cancellationToken);
                }
                else
                {
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text($"I'm sorry, but I could not find {userSelections.StreamerName} in the Dev Streams database"));
                }
            }
            catch (Exception)
            {
                await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text($"I'm sorry, but I am having problems talking to the Dev Streams database."));
            }

            return await stepContext.EndDialogAsync(cancellationToken);
        }
    }
}
