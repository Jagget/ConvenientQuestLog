using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    public class ConvenientQuestLogWindow : DaggerfallQuestJournalWindow
    {
        public static bool selectedQuestDisplayed = false;
        public static bool travelOptionsModEnabled = false;
        public static bool travelOptionsCautiousTravel = false;
        public static bool travelOptionsStopAtInnsTravel = false;
        public static readonly int defaultMessageCheckValue = -1;

        private readonly TravelTimeCalculator travelTimeCalculator = new TravelTimeCalculator();
        private Message selectedQuestMessage;
        private List<Message> groupedQuestMessages;
        private int currentMessageCheck = ConvenientQuestLogWindow.defaultMessageCheckValue;

        private bool useDurationTokenSetting = false;
        private bool useDetailedQuestDurationSetting = false;

        Dictionary<string, string> stringTable = null;

        string untitledQuest = "Untitled Quest";
        string activeQuestToolTipText = "Click on a quest mesage to go back to Active Quests.";
        string journalToolTipText = "Click on a quest for quest information. Click on a location to travel there.";
        string cancelMainStory = "You cannot cancel main story quests.";
        string cancelOneTime = "You cannot cancel one time quests.";
        string areYouSure = "Are you sure you want to cancel {0}?";
        string currentLocation = "Current location";
        string travelOptionsDuration = "{0} hours {1} mins travel";
        string travelDuration = "{0} days travel";
        string durationSearchToken = "_ day";
        string day = "day";
        string days = "days";
        string questTimeLimitFormat = "{0} ({1} {2} left)";
        string questTimeLimitExtendedFormat = "{0} ({1} {2} {3:00}:{4:00} left)";

        public ConvenientQuestLogWindow(IUserInterfaceManager uiManager) : base(uiManager)
        {
            LoadText();
        }

        protected override void Setup()
        {
            base.Setup();
            useDurationTokenSetting = RegisterConvenientQuestLogWindow.mod.GetSettings().GetBool("General", "QuestsShouldContainDurationToken");
            useDetailedQuestDurationSetting = RegisterConvenientQuestLogWindow.mod.GetSettings().GetBool("General", "DetailedQuestDuration");
            Mod TOMod = ModManager.Instance.GetMod("TravelOptions");
            if (TOMod == null)
                return;
            ConvenientQuestLogWindow.travelOptionsModEnabled = TOMod.Enabled;
            ModSettings settings = TOMod.GetSettings();
            ConvenientQuestLogWindow.travelOptionsCautiousTravel = settings.GetBool("CautiousTravel", "PlayerControlledCautiousTravel");
            ConvenientQuestLogWindow.travelOptionsStopAtInnsTravel = settings.GetBool("StopAtInnsTravel", "PlayerControlledInnsTravel");
        }

        public override void Update()
        {
            base.Update();
            if (DisplayMode != DaggerfallQuestJournalWindow.JournalDisplay.ActiveQuests || currentMessageCheck == currentMessageIndex)
                return;
            currentMessageCheck = currentMessageIndex;
            questLogLabel.Clear();
            if (ConvenientQuestLogWindow.selectedQuestDisplayed)
                SetTextForSelectedQuest(selectedQuestMessage);
            else
                SetTextActiveQuests();
        }

        public override void OnPush()
        {
            base.OnPush();
            currentMessageCheck = ConvenientQuestLogWindow.defaultMessageCheckValue;
        }

        public override void OnPop()
        {
            base.OnPop();
            currentMessageCheck = ConvenientQuestLogWindow.defaultMessageCheckValue;
        }

        protected override void HandleClick(Vector2 position, bool remove = false)
        {
            if (DisplayMode != DaggerfallQuestJournalWindow.JournalDisplay.ActiveQuests)
            {
                base.HandleClick(position, remove);
            }
            else
            {
                if (entryLineMap == null)
                    return;
                int index = (int)position.y / questLogLabel.LineHeight;
                if (index < entryLineMap.Count)
                    selectedEntry = entryLineMap[index];
                else
                    selectedEntry = entryLineMap[entryLineMap.Count - 1];
                Debug.Log($"Line is: {index} entry: {selectedEntry}");

                if (ConvenientQuestLogWindow.selectedQuestDisplayed)
                {
                    currentMessageIndex = 0;
                    SetTextActiveQuests();
                }
                else
                {
                    if (index + 1 >= entryLineMap.Count)
                        return;
                    if (index == 0 || entryLineMap[index - 1] != selectedEntry)
                    {
                        currentMessageIndex = 0;
                        selectedQuestMessage = groupedQuestMessages[selectedEntry];
                        if (remove)
                        {
                            if (selectedQuestMessage.ParentQuest.QuestName.StartsWith("S0000"))
                            {
                                DaggerfallMessageBox messageBox = new DaggerfallMessageBox(uiManager, uiManager.TopWindow);
                                messageBox.SetText(cancelMainStory);
                                messageBox.ClickAnywhereToClose = true;
                                messageBox.AllowCancel = false;
                                messageBox.ParentPanel.BackgroundColor = Color.clear;
                                messageBox.Show();
                            }
                            else if (selectedQuestMessage.ParentQuest.OneTime)
                            {
                                DaggerfallMessageBox messageBox = new DaggerfallMessageBox(uiManager, uiManager.TopWindow);
                                messageBox.SetText(cancelOneTime);
                                messageBox.ClickAnywhereToClose = true;
                                messageBox.AllowCancel = false;
                                messageBox.ParentPanel.BackgroundColor = Color.clear;
                                messageBox.Show();
                            }
                            else
                            {
                                DaggerfallMessageBox messageBox = new DaggerfallMessageBox(uiManager, DaggerfallMessageBox.CommonMessageBoxButtons.YesNo, string.Format(areYouSure, selectedQuestMessage.ParentQuest.DisplayName), this);
                                messageBox.ClickAnywhereToClose = true;
                                messageBox.AllowCancel = false;
                                messageBox.ParentPanel.BackgroundColor = Color.clear;
                                messageBox.OnButtonClick += CancelQuest_OnButtonClick;
                                messageBox.Show();
                            }
                        }
                        else
                            SetTextForSelectedQuest(selectedQuestMessage);
                    }
                    else
                    {
                        if (entryLineMap[index - 1] != selectedEntry || entryLineMap[index + 1] != selectedEntry)
                            return;
                        HandleQuestClicks(groupedQuestMessages[selectedEntry]);
                    }
                }
            }
        }

        protected override void DialogButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            base.DialogButton_OnMouseClick(sender, position);
            currentMessageCheck = ConvenientQuestLogWindow.defaultMessageCheckValue;
        }

        private string GetTravelTime(Place place)
        {
            DaggerfallUnity.Instance.ContentReader.GetLocation(place.SiteDetails.regionName, place.SiteDetails.locationName, out DFLocation dfLocation);

            if (dfLocation.LocationIndex == GameManager.Instance.PlayerGPS.CurrentLocation.LocationIndex)
                return currentLocation;

            DFPosition mapPixel = MapsFile.LongitudeLatitudeToMapPixel(dfLocation.MapTableData.Longitude, dfLocation.MapTableData.Latitude);

            if (ConvenientQuestLogWindow.travelOptionsModEnabled && ConvenientQuestLogWindow.travelOptionsCautiousTravel && ConvenientQuestLogWindow.travelOptionsStopAtInnsTravel)
            {
                TransportManager transportManager = GameManager.Instance.TransportManager;
                bool useHorse = transportManager.TransportMode == TransportModes.Horse;
                bool useCart = transportManager.TransportMode == TransportModes.Cart;
                int num = (int)(GameManager.Instance.GuildManager.FastTravel(travelTimeCalculator.CalculateTravelTime(mapPixel, !ConvenientQuestLogWindow.travelOptionsCautiousTravel, !ConvenientQuestLogWindow.travelOptionsStopAtInnsTravel, false, useHorse, useCart)) / ((ConvenientQuestLogWindow.travelOptionsCautiousTravel ? 0.800000011920929 : 1.0) * 2.0));
                return string.Format(travelOptionsDuration, num / 60, num % 60);
            }
            bool hasHorse = GameManager.Instance.TransportManager.HasHorse();
            bool hasCart = GameManager.Instance.TransportManager.HasCart();
            int minutesToTravel = GameManager.Instance.GuildManager.FastTravel(travelTimeCalculator.CalculateTravelTime(mapPixel, true, true, false, hasHorse, hasCart));
            int daysToTravel = minutesToTravel / 1440;
            if (minutesToTravel % 1440 > 0)
                ++daysToTravel;
            return string.Format(travelDuration, daysToTravel);
        }

        protected virtual void SetTextActiveQuests()
        {
            ConvenientQuestLogWindow.selectedQuestDisplayed = false;
            if (questMessages == null)
                return;
            messageCount = questMessages.Count;
            questLogLabel.TextScale = 1.1f;
            titleLabel.Text = TextManager.Instance.GetLocalizedText("activeQuests", TextCollections.Internal, false);
            titleLabel.ToolTip = defaultToolTip;
            titleLabel.ToolTipText = journalToolTipText;
            int num = 0;
            entryLineMap = new List<int>(20);
            List<TextFile.Token> allActiveQuests = new List<TextFile.Token>();
            groupedQuestMessages = new List<Message>();
            foreach (Quest quest in questMessages.Select(x => x.ParentQuest).Distinct())
            {
                Quest parent = quest;
                Quest.LogEntry lastLogEntry = parent.GetLogMessages().OrderBy(x => x.stepID).Last();
                groupedQuestMessages.Add(questMessages.Single(x => x.ParentQuest == parent && x.ID == lastLogEntry.messageID));
            }
            for (int i = currentMessageIndex; i < groupedQuestMessages.Count && num < 20; i++)
            {
                string title = FormatQuestTitle(groupedQuestMessages[i].ParentQuest.DisplayName);

                bool haveDurationTokens = false;

                if (useDurationTokenSetting)
                {
                    haveDurationTokens = questMessages.Where(x => x.ParentQuest == groupedQuestMessages[i].ParentQuest).Any(y => y.Variants.Any(z => z.tokens.Any(a => a.text.Contains(durationSearchToken)))); ;
                }

                if (!useDurationTokenSetting || (useDurationTokenSetting && haveDurationTokens))
                {
                    List<Clock> clocks = new List<Clock>();
                    foreach (Clock clockResource in groupedQuestMessages[i].ParentQuest.GetAllResources(typeof(Clock)).Cast<Clock>())
                    {
                        if (clockResource.Enabled)
                            clocks.Add(clockResource);
                    }
                    if (clocks.Any())
                    {
                        Clock clock = clocks.OrderBy(x => x.RemainingTimeInSeconds).First();
                        if (useDetailedQuestDurationSetting)
                        {
                            string timeLeft = clock.GetTimeString(clock.RemainingTimeInSeconds);

                            int daysLeft = Convert.ToInt32(timeLeft.Split('.')[0]);
                            int hours = Convert.ToInt32(timeLeft.Split('.')[1].Split(':')[0]);
                            int minutes = Convert.ToInt32(timeLeft.Split('.')[1].Split(':')[1]);

                            string daysString = daysLeft == 1 ? day : days;

                            title = string.Format(questTimeLimitExtendedFormat, title, daysLeft, daysString, hours, minutes);
                        }
                        else
                        {
                            string daysLeft = clock.GetDaysString(clock.RemainingTimeInSeconds);
                            string daysString = daysLeft == "1" ? day : days;
                            title = string.Format(questTimeLimitFormat, title, daysLeft, daysString);
                        }
                    }
                }
                List<TextFile.Token> oneQuestTokens = new List<TextFile.Token>() { new TextFile.Token(TextFile.Formatting.Text, title) };
                Place mentionedInMessage = GetLastPlaceMentionedInMessage(groupedQuestMessages[i]);
                string location = string.Empty;
                if (!string.IsNullOrWhiteSpace(mentionedInMessage?.SiteDetails.locationName))
                {
                    location = TextManager.Instance.GetLocalizedLocationName(mentionedInMessage.SiteDetails.mapId, mentionedInMessage.SiteDetails.locationName) + " (" + GetTravelTime(mentionedInMessage) + ")";
                    oneQuestTokens.Add(TextFile.NewLineToken);
                    oneQuestTokens.Add(new TextFile.Token(TextFile.Formatting.TextHighlight, location));
                }
                oneQuestTokens.Add(new TextFile.Token(TextFile.Formatting.Nothing, string.Empty));
                for (int index = 0; index < oneQuestTokens.Count && num < 20; ++index)
                {
                    TextFile.Token token = oneQuestTokens[index];
                    if (token.formatting == TextFile.Formatting.Text || token.formatting == TextFile.Formatting.TextHighlight)
                    {
                        ++num;
                        entryLineMap.Add(i);
                    }
                    else
                        token.formatting = TextFile.Formatting.JustifyLeft;
                    allActiveQuests.Add(token);
                }
                allActiveQuests.Add(TextFile.NewLineToken);
                ++num;
                entryLineMap.Add(i);
            }
            questLogLabel.SetText(allActiveQuests.ToArray());
        }

        private void SetTextForSelectedQuest(Message message)
        {
            ConvenientQuestLogWindow.selectedQuestDisplayed = true;
            if (questMessages == null)
                return;
            messageCount = questMessages.Count;
            questLogLabel.TextScale = 1.1f;
            List<Message> list = questMessages.Where(x => x.ParentQuest.UID == message.ParentQuest.UID).ToList();
            titleLabel.Text = FormatQuestTitle(list.First().ParentQuest.DisplayName);
            titleLabel.ToolTip = defaultToolTip;
            titleLabel.ToolTipText = activeQuestToolTipText;
            int num = 0;
            entryLineMap = new List<int>(20);
            List<TextFile.Token> tokenList = new List<TextFile.Token>();
            for (int currentMessageIndex = this.currentMessageIndex; currentMessageIndex < list.Count && num < 20; ++currentMessageIndex)
            {
                TextFile.Token[] textTokens = list[currentMessageIndex].GetTextTokens(-1, true);
                for (int index = 0; index < textTokens.Length && num < 20; ++index)
                {
                    TextFile.Token token = textTokens[index];
                    if (token.formatting == TextFile.Formatting.Text)
                    {
                        ++num;
                        entryLineMap.Add(0);
                    }
                    else
                        token.formatting = TextFile.Formatting.JustifyLeft;
                    tokenList.Add(token);
                }
                tokenList.Add(TextFile.NewLineToken);
                ++num;
                entryLineMap.Add(0);
            }
            questLogLabel.SetText(tokenList.ToArray());
        }

        private string FormatQuestTitle(string questTitle)
        {
            if (questTitle == "Main Quest Backbone")
                questTitle = questTitle.Replace(" Backbone", string.Empty);
            return !string.IsNullOrWhiteSpace(questTitle) ? questTitle : untitledQuest;
        }

        private void CancelQuest_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
                QuestMachine.Instance.TombstoneQuest(selectedQuestMessage.ParentQuest);

            sender.CloseWindow();
            DisplayMode = DaggerfallQuestJournalWindow.JournalDisplay.ActiveQuests;
            uiManager.PushWindow(this);
        }

        private void LoadText()
        {
            const string csvFilename = "ConvenientQuestLog.csv";

            if (stringTable != null)
                return;

            stringTable = StringTableCSVParser.LoadDictionary(csvFilename);

            if (stringTable.ContainsKey("untitledQuest"))
                untitledQuest = stringTable["untitledQuest"];

            if (stringTable.ContainsKey("activeQuestToolTipText"))
                activeQuestToolTipText = stringTable["activeQuestToolTipText"];

            if (stringTable.ContainsKey("journalToolTipText"))
                journalToolTipText = stringTable["journalToolTipText"];

            if (stringTable.ContainsKey("cancelMainStory"))
                cancelMainStory = stringTable["cancelMainStory"];

            if (stringTable.ContainsKey("cancelOneTime"))
                cancelOneTime = stringTable["cancelOneTime"];

            if (stringTable.ContainsKey("areYouSure"))
                areYouSure = stringTable["areYouSure"];

            if (stringTable.ContainsKey("currentLocation"))
                currentLocation = stringTable["currentLocation"];

            if (stringTable.ContainsKey("travelOptionsDuration"))
                travelOptionsDuration = stringTable["travelOptionsDuration"];

            if (stringTable.ContainsKey("travelDuration"))
                travelDuration = stringTable["travelDuration"];

            if (stringTable.ContainsKey("durationSearchToken"))
                durationSearchToken = stringTable["durationSearchToken"];

            if (stringTable.ContainsKey("day"))
                day = stringTable["day"];

            if (stringTable.ContainsKey("days"))
                days = stringTable["days"];

            if (stringTable.ContainsKey("questTimeLimitFormat"))
                questTimeLimitFormat = stringTable["questTimeLimitFormat"];

            if (stringTable.ContainsKey("questTimeLimitExtendedFormat"))
                questTimeLimitExtendedFormat = stringTable["questTimeLimitExtendedFormat"];
        }
    }
}
