using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Discord;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using DenizenBot.UtilityProcessors;
using DiscordBotBase.CommandHandlers;
using DiscordBotBase;
using SharpDenizenTools.ScriptAnalysis;

namespace DenizenBot.CommandHandlers
{
    /// <summary>
    /// Commands to perform utility functions.
    /// </summary>
    public class UtilityCommands : UserCommands
    {
        /// <summary>
        /// Base URL for paste sites.
        /// </summary>
        public const string PASTEBIN_URL_BASE = "https://pastebin.com/",
            DENIZEN_PASTE_URL_BASE = "https://one.denizenscript.com/paste/",
            DENIZEN_HASTE_URL_BASE = "https://one.denizenscript.com/haste/";

        /// <summary>
        /// ASCII validator for a pastebin ID.
        /// </summary>
        public static AsciiMatcher PASTEBIN_CODE_VALIDATOR = new AsciiMatcher((c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));

        /// <summary>
        /// ASCII validator for a Denizen haste code.
        /// </summary>
        public static AsciiMatcher HASTE_CODE_VALIDATOR = new AsciiMatcher((c) => c >= '0' && c <= '9');

        /// <summary>
        /// The max wait time for a web-link download in a command.
        /// </summary>
        public static TimeSpan WebLinkDownloadTimeout = new TimeSpan(hours: 0, minutes: 0, seconds: 15);

        /// <summary>
        /// For a web-link command like '!logcheck', gets the data from the paste link.
        /// </summary>
        public string GetWebLinkDataForCommand(string inputUrl, IUserMessage message)
        {
            string rawUrl;
            if (inputUrl.StartsWith(PASTEBIN_URL_BASE))
            {
                string pastebinCode = inputUrl.Substring(PASTEBIN_URL_BASE.Length);
                if (!PASTEBIN_CODE_VALIDATOR.IsOnlyMatches(pastebinCode))
                {
                    SendErrorMessageReply(message, "Command Syntax Incorrect", "Pastebin URL given does not conform to expected format.");
                    return null;
                }
                rawUrl = $"{PASTEBIN_URL_BASE}raw/{pastebinCode}";
            }
            else if (inputUrl.StartsWith(DENIZEN_PASTE_URL_BASE) || inputUrl.StartsWith(DENIZEN_HASTE_URL_BASE))
            {
                string pasteCode = inputUrl.Substring(DENIZEN_HASTE_URL_BASE.Length).Before('/');
                if (!HASTE_CODE_VALIDATOR.IsOnlyMatches(pasteCode))
                {
                    SendErrorMessageReply(message, "Command Syntax Incorrect", "Denizen haste URL given does not conform to expected format.");
                    return null;
                }
                rawUrl = $"{DENIZEN_HASTE_URL_BASE}{pasteCode}.txt";
            }
            else
            {
                SendErrorMessageReply(message, "Command Syntax Incorrect", "Input argument must be a link to pastebin or <https://one.denizenscript.com/haste>.");
                return null;
            }
            try
            {
                Task<string> downloadTask = Program.ReusableWebClient.GetStringAsync(rawUrl);
                downloadTask.Wait(WebLinkDownloadTimeout);
                if (!downloadTask.IsCompleted)
                {
                    SendErrorMessageReply(message, "Error", "Download did not complete in time.");
                    return null;
                }
                return downloadTask.Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (ex is HttpRequestException)
                {
                    SendErrorMessageReply(message, "Error", $"Exception thrown while downloading raw data from link. HttpRequestException: `{EscapeUserInput(ex.Message)}`");
                }
                else
                {
                    SendErrorMessageReply(message, "Error", "Exception thrown while downloading raw data from link (see console for details).");
                }
                return null;
            }
        }

        /// <summary>
        /// Command to check for common issues in server logs.
        /// </summary>
        public void CMD_LogCheck(string[] cmds, IUserMessage message)
        {
            if (cmds.Length == 0)
            {
                SendErrorMessageReply(message, "Command Syntax Incorrect", "`!logcheck <link>`");
                return;
            }
            string data = GetWebLinkDataForCommand(cmds[0], message);
            if (data == null)
            {
                return;
            }
            LogChecker checker = new LogChecker(data);
            checker.Run();
            SendReply(message, checker.GetResult());
        }

        /// <summary>
        /// Command to check the updatedness of a version string.
        /// </summary>
        public void CMD_VersionCheck(string[] cmds, IUserMessage message)
        {
            if (cmds.Length == 0)
            {
                SendErrorMessageReply(message, "Command Syntax Incorrect", "`!versioncheck <version text>`");
                return;
            }
            string combined = string.Join(" ", cmds).Trim();
            if (combined.ToLowerFast().StartsWith("loading "))
            {
                combined = combined.Substring("loading ".Length).Trim();
            }
            if (combined.IsEmpty())
            {
                SendErrorMessageReply(message, "Bad Input", "Input text doesn't look like a version string (blank input?).");
                return;
            }
            string projectName = BuildNumberTracker.SplitToNameAndVersion(combined, out string versionText).Replace(":", "").Trim();
            versionText = versionText.Trim();
            if (projectName.IsEmpty() || versionText.IsEmpty())
            {
                SendErrorMessageReply(message, "Bad Input", "Input text doesn't look like a version string (single word input?).");
                return;
            }
            string nameLower = projectName.ToLowerFast();
            if (nameLower == "paper" || nameLower == "spigot" || nameLower == "craftbukkit")
            {
                string output = LogChecker.ServerVersionStatusOutput(combined, out bool isGood);
                if (string.IsNullOrWhiteSpace(output))
                {
                    SendErrorMessageReply(message, "Bad Input", $"Input text looks like a {nameLower} version, but doesn't fit the expected {nameLower} server version format. Should start with '{nameLower} version git-{nameLower}-...'");
                    return;
                }
                if (isGood)
                {
                    SendGenericPositiveMessageReply(message, "Running Current Build", $"That version is the current {nameLower} build for an acceptable server version.");
                }
                else
                {
                    SendGenericNegativeMessageReply(message, "Build Outdated", $"{output}.");
                }
                return;
            }
            if (BuildNumberTracker.TryGetBuildFor(projectName, versionText, out BuildNumberTracker.BuildNumber build, out int buildNum))
            {
                if (build.IsCurrent(buildNum, out int behindBy))
                {
                    SendGenericPositiveMessageReply(message, "Running Current Build", $"That version is the current {build.Name} build.");
                }
                else
                {
                    SendGenericNegativeMessageReply(message, "Build Outdated", $"That version is an outdated {build.Name} build.\nThe current {build.Name} build is {build.Value}.\nYou are behind by {behindBy} builds.");
                }
                return;
            }
            SendErrorMessageReply(message, "Bad Input", $"Input project name (`{EscapeUserInput(projectName)}`) doesn't look like any tracked project (or the version text is formatted incorrectly).");
            return;
        }


        /// <summary>
        /// Gets the result Discord embed for the script check.
        /// </summary>
        /// <returns>The embed to send.</returns>
        public Embed GetResult(ScriptChecker checker)
        {
            int totalWarns = checker.Errors.Count + checker.Warnings.Count + checker.MinorWarnings.Count;
            EmbedBuilder embed = new EmbedBuilder().WithTitle("Script Check Results").WithThumbnailUrl((totalWarns > 0) ? Constants.WARNING_ICON : Constants.INFO_ICON);
            int linesMissing = 0;
            int shortened = 0;
            void embedList(List<ScriptChecker.ScriptWarning> list, string title)
            {
                if (list.Count > 0)
                {
                    HashSet<string> usedKeys = new HashSet<string>();
                    StringBuilder thisListResult = new StringBuilder(list.Count * 200);
                    foreach (ScriptChecker.ScriptWarning entry in list)
                    {
                        if (usedKeys.Contains(entry.WarningUniqueKey))
                        {
                            continue;
                        }
                        usedKeys.Add(entry.WarningUniqueKey);
                        StringBuilder lines = new StringBuilder(50);
                        if (entry.Line != -1)
                        {
                            lines.Append(entry.Line + 1);
                        }
                        foreach (ScriptChecker.ScriptWarning subEntry in list.SkipWhile(s => s != entry).Skip(1).Where(s => s.WarningUniqueKey == entry.WarningUniqueKey))
                        {
                            shortened++;
                            if (lines.Length < 40)
                            {
                                lines.Append(", ").Append(subEntry.Line + 1);
                                if (lines.Length >= 40)
                                {
                                    lines.Append(", ...");
                                }
                            }
                        }
                        string message = $"On line {lines}: {entry.CustomMessageForm}";
                        if (thisListResult.Length + message.Length < 1000 && embed.Length + thisListResult.Length + message.Length < 1800)
                        {
                            thisListResult.Append($"{message}\n");
                        }
                        else
                        {
                            linesMissing++;
                        }
                    }
                    if (thisListResult.Length > 0)
                    {
                        embed.AddField(title, thisListResult.ToString());
                    }
                    Console.WriteLine($"Script Checker {title}: {string.Join('\n', list.Select(s => $"{s.Line + 1}: {s.CustomMessageForm}"))}");
                }
            }
            embedList(checker.Errors, "Encountered Critical Errors");
            embedList(checker.Warnings, "Script Warnings");
            embedList(checker.MinorWarnings, "Minor Warnings");
            embedList(checker.Infos, "Other Script Information");
            if (linesMissing > 0)
            {
                embed.AddField("Missing Lines", $"There are {linesMissing} lines not able to fit in this result. Fix the listed errors to see the rest.");
            }
            if (shortened > 0)
            {
                embed.AddField("Shortened Lines", $"There are {shortened} lines that were merged into other lines.");
            }
            foreach (string debug in checker.Debugs)
            {
                Console.WriteLine($"Script checker debug: {debug}");
            }
            return embed.Build();
        }

        /// <summary>
        /// Command to check for common issues in script pastes.
        /// </summary>
        public void CMD_ScriptCheck(string[] cmds, IUserMessage message)
        {
            if (cmds.Length == 0)
            {
                SendErrorMessageReply(message, "Command Syntax Incorrect", "`!script <link>`");
                return;
            }
            string data = GetWebLinkDataForCommand(cmds[0], message);
            if (data == null)
            {
                return;
            }
            ScriptChecker checker = new ScriptChecker(data);
            checker.Run();
            SendReply(message, GetResult(checker));
        }
    }
}
