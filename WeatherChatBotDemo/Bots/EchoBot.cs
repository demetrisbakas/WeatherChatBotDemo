// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.5.0

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Builder.Dialogs;
using System.Text.RegularExpressions;
using System.Net;
using Newtonsoft.Json;

namespace WeatherChatBotDemo.Bots
{
    public class EchoBot : ActivityHandler
    {
        private LuisRecognizer Recognizer { get; } = null;
        private DialogSet _dialogs;
        private readonly EchoBotAccessors _accessors;
        private Regex rx = new Regex("\"([^\".]+)");
        private static bool acceptLocation = false, acceptTime = false, weatherIntent = false, flag1 = false;
        private static WeatherProfile user;

        public EchoBot(EchoBotAccessors accessors, LuisRecognizer luis)
        {
            // The incoming luis variable is the LUIS Recognizer we added above.
            this.Recognizer = luis ?? throw new System.ArgumentNullException(nameof(luis));

            // Set the _accessors 
            _accessors = accessors ?? throw new System.ArgumentNullException(nameof(accessors));
            // The DialogSet needs a DialogState accessor, it will call it when it has a turn context.
            _dialogs = new DialogSet(accessors.ConversationDialogState);

            // This array defines how the Waterfall will execute.
            var waterfallStepsWeather = new WaterfallStep[]
            {
                WeatherStepAsync,
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            _dialogs.Add(new WaterfallDialog("details", waterfallStepsWeather));
            _dialogs.Add(new TextPrompt("weather"));
        }


        // IMPORTANT
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                // Get the user state from the turn context.
                user = await _accessors.UserProfile.GetAsync(turnContext, () => new WeatherProfile());

                // Get the conversation state from the turn context.
                var state = await _accessors.CounterState.GetAsync(turnContext, () => new CounterState());

                // Bump the turn count for this conversation.
                state.TurnCount++;

                Newtonsoft.Json.Linq.JToken location = null, time = null;

                // Check LUIS model
                var recognizerResult = await this.Recognizer.RecognizeAsync(turnContext, cancellationToken);
                var topIntent = recognizerResult?.GetTopScoringIntent();

                // Get the Intent as a string
                string strIntent = (topIntent != null) ? topIntent.Value.intent : "";

                // Get the IntentScore as a double
                double dblIntentScore = (topIntent != null) ? topIntent.Value.score : 0.0;

                // Only proceed with LUIS if there is an Intent 
                // and the score for the Intent is greater than 95
                if (strIntent == "Weather" && (dblIntentScore > 0.95))
                {
                    weatherIntent = true;

                    if (recognizerResult.Entities.TryGetValue("Location", out location))
                        user.Location = rx.Match(location.ToString()).Value.TrimStart('"');

                    // Show Location
                    //await turnContext.SendActivityAsync(MessageFactory.Text(user.Location), cancellationToken);


                    if (recognizerResult.Entities.TryGetValue("Time", out time))
                        user.Time = rx.Match(time.ToString()).Value.TrimStart('"');

                    // Show Time
                    //await turnContext.SendActivityAsync(MessageFactory.Text(user.Time), cancellationToken);

                    if ((user.Location != null) && (user.Time != null))
                    {
                        // Api connection here <------------------------------------
                        var responseMessage = $"The weather in {user.Location} for {user.Time} is " + GetWeather(user.Location, user.Time);
                        await turnContext.SendActivityAsync(responseMessage);
                    }
                }
                else if (!flag1)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Sorry, please try again."), cancellationToken);
                }


                if ((user.Location == null) || (user.Time == null) || flag1)
                {
                    if (acceptLocation)
                    {
                        acceptLocation = false;
                        user.Location = turnContext.Activity.Text;
                    }
                    if (acceptTime)
                    {
                        acceptTime = false;
                        user.Time = turnContext.Activity.Text;
                    }

                    if (weatherIntent)
                    {
                        if (user.Location == null)
                        {
                            acceptLocation = true;
                            flag1 = true;

                            var dialogContext = await _dialogs.CreateContextAsync(turnContext, cancellationToken);
                            var results = await dialogContext.ContinueDialogAsync(cancellationToken);

                            var responseMessage = "Give me your city.";
                            await turnContext.SendActivityAsync(responseMessage);
                        }
                        else if (user.Time == null)
                        {
                            acceptTime = true;
                            flag1 = true;

                            var dialogContext = await _dialogs.CreateContextAsync(turnContext, cancellationToken);
                            var results = await dialogContext.ContinueDialogAsync(cancellationToken);

                            var responseMessage = "Give me when do you want to learn the weather.";
                            await turnContext.SendActivityAsync(responseMessage);
                        }
                        else if ((user.Location != null) && (user.Time != null) && flag1)
                        {
                            weatherIntent = false;
                            flag1 = false;

                            // Api connection here <------------------------------------
                            await turnContext.SendActivityAsync(MessageFactory.Text($"The weather in {user.Location} for {user.Time} is " + GetWeather(user.Location, user.Time)), cancellationToken);
                        }
                    }
                }

                // Set the property using the accessor.
                await _accessors.CounterState.SetAsync(turnContext, state);

                // Save the new turn count into the conversation state.
                await _accessors.ConversationState.SaveChangesAsync(turnContext);

                // Save the user profile updates into the user state.
                await _accessors.UserState.SaveChangesAsync(turnContext, false, cancellationToken);
            }
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Hello, I'm a weather bot!"), cancellationToken);

                    var dialogContext = await _dialogs.CreateContextAsync(turnContext, cancellationToken);
                    var results = await dialogContext.ContinueDialogAsync(cancellationToken);

                    await dialogContext.BeginDialogAsync("details", null, cancellationToken);
                }
            }
        }

        private static async Task<DialogTurnResult> WeatherStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Running a prompt here means the next WaterfallStep will be 
            // run when the users response is received.
            return await stepContext.PromptAsync("weather", new PromptOptions { Prompt = MessageFactory.Text("Ask me about the weather.") }, cancellationToken);
        }


        // Returns the weather and description given the city and time
        private string GetWeather(string location, string time)
        {
            const string apiKey = "";
            string url = $"http://api.openweathermap.org/data/2.5/weather?q={location}&appid={apiKey}&units=metric&cnt=6";

            var json = new WebClient().DownloadString(url);

            var jsonResult = JsonConvert.DeserializeObject<WeatherInfo.root>(json);
            WeatherInfo.root output = jsonResult;

            string result = $"{output.main.temp}�C with {output.weather[0].description}";

            return result;
        }
    }
}
