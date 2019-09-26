using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Discord.Net;
using Discord;
using Discord.WebSocket;
using System.Diagnostics;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticDataSyntax;
using DenizenBot.CommandHandlers;

namespace DenizenBot
{
    /// <summary>
    /// Discord bot for Denizen 1.x (Bukkit) help.
    /// </summary>
    public class DenizenMetaBot
    {
        /// <summary>
        /// Configuration folder path.
        /// </summary>
        public const string CONFIG_FOLDER = "./config/";

        /// <summary>
        /// Bot token file path.
        /// </summary>
        public const string TOKEN_FILE = CONFIG_FOLDER + "token.txt";

        /// <summary>
        /// Configuration file path.
        /// </summary>
        public const string CONFIG_FILE = CONFIG_FOLDER + "config.fds";

        /// <summary>
        /// Bot token, read from config data.
        /// </summary>
        public static readonly string TOKEN = File.ReadAllText(TOKEN_FILE).Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ').Replace(" ", "");

        /// <summary>
        /// The configuration file section.
        /// </summary>
        public FDSSection ConfigFile;

        /// <summary>
        /// Internal Discord API bot Client handler.
        /// </summary>
        public DiscordSocketClient Client;

        /// <summary>
        /// Bot command response handler.
        /// </summary>
        public void Respond(SocketMessage message)
        {
            string[] messageDataSplit = message.Content.Split(' ');
            StringBuilder resultBuilder = new StringBuilder(message.Content.Length);
            List<string> cmds = new List<string>();
            for (int i = 0; i < messageDataSplit.Length; i++)
            {
                if (messageDataSplit[i].Contains("<") && messageDataSplit[i].Contains(">"))
                {
                    continue;
                }
                resultBuilder.Append(messageDataSplit[i]).Append(" ");
                if (messageDataSplit[i].Length > 0)
                {
                    cmds.Add(messageDataSplit[i]);
                }
            }
            if (cmds.Count == 0)
            {
                Console.WriteLine("Empty input, ignoring: " + message.Author.Username);
                return;
            }
            string fullMessageCleaned = resultBuilder.ToString();
            Console.WriteLine("Found input from: (" + message.Author.Username + "), in channel: " + message.Channel.Name + ": " + fullMessageCleaned);
            string commandNameLowered = cmds[0].ToLowerInvariant();
            cmds.RemoveAt(0);
            if (ChatCommands.TryGetValue(commandNameLowered, out Action<string[], SocketMessage> acto))
            {
                acto.Invoke(cmds.ToArray(), message);
            }
            else
            {
                message.Channel.SendMessageAsync(embed: UserCommands.GetErrorMessageEmbed("Unknown Command", "Unknown command. Consider the __**help**__ command?")).Wait();
            }
        }

        /// <summary>
        /// All valid user commands in a map of typable command name -> command method.
        /// </summary>
        public readonly Dictionary<string, Action<string[], SocketMessage>> ChatCommands = new Dictionary<string, Action<string[], SocketMessage>>(1024);

        /// <summary>
        /// Returns whether a Discord user is a bot commander (via role check).
        /// </summary>
        public bool IsBotCommander(SocketGuildUser user)
        {
            return user.Roles.Any((role) => role.Name.ToLowerInvariant() == "botcommander");
        }
        
        /// <summary>
        /// Saves the config file.
        /// </summary>
        public void SaveConfig()
        {
            lock (ConfigSaveLock)
            {
                ConfigFile.SaveToFile(CONFIG_FILE);
            }
        }

        /// <summary>
        /// Lock object for config file saving/loading.
        /// </summary>
        public static Object ConfigSaveLock = new Object();

        /// <summary>
        /// Generates default command name->method pairs.
        /// </summary>
        void DefaultCommands()
        {
            AdminCommands adminCmds = new AdminCommands() { Bot = this };
            InformationCommands infoCmds = new InformationCommands() { Bot = this };
            MetaCommands metaCmds = new MetaCommands() { Bot = this };
            // ========= Informational =========
            // help
            ChatCommands["help"] = infoCmds.CMD_Help;
            ChatCommands["halp"] = infoCmds.CMD_Help;
            ChatCommands["helps"] = infoCmds.CMD_Help;
            ChatCommands["halps"] = infoCmds.CMD_Help;
            ChatCommands["hel"] = infoCmds.CMD_Help;
            ChatCommands["hal"] = infoCmds.CMD_Help;
            ChatCommands["h"] = infoCmds.CMD_Help;
            // hello
            ChatCommands["hello"] = infoCmds.CMD_Hello;
            ChatCommands["hi"] = infoCmds.CMD_Hello;
            ChatCommands["hey"] = infoCmds.CMD_Hello;
            ChatCommands["source"] = infoCmds.CMD_Hello;
            ChatCommands["src"] = infoCmds.CMD_Hello;
            ChatCommands["github"] = infoCmds.CMD_Hello;
            ChatCommands["git"] = infoCmds.CMD_Hello;
            ChatCommands["hub"] = infoCmds.CMD_Hello;
            // info
            ChatCommands["info"] = infoCmds.CMD_Info;
            ChatCommands["notice"] = infoCmds.CMD_Info;
            ChatCommands["alert"] = infoCmds.CMD_Info;
            // update
            ChatCommands["update"] = infoCmds.CMD_Update;
            ChatCommands["latest"] = infoCmds.CMD_Update;
            ChatCommands["current"] = infoCmds.CMD_Update;
            ChatCommands["build"] = infoCmds.CMD_Update;
            ChatCommands["builds"] = infoCmds.CMD_Update;
            ChatCommands["download"] = infoCmds.CMD_Update;
            ChatCommands["version"] = infoCmds.CMD_Update;
            // github
            ChatCommands["github"] = infoCmds.CMD_GitHub;
            ChatCommands["readme"] = infoCmds.CMD_GitHub;
            ChatCommands["gh"] = infoCmds.CMD_GitHub;
            ChatCommands["read"] = infoCmds.CMD_GitHub;
            ChatCommands["link"] = infoCmds.CMD_GitHub;
            // issues
            ChatCommands["issues"] = infoCmds.CMD_Issues;
            ChatCommands["issue"] = infoCmds.CMD_Issues;
            ChatCommands["error"] = infoCmds.CMD_Issues;
            ChatCommands["ghissues"] = infoCmds.CMD_Issues;
            ChatCommands["githubissues"] = infoCmds.CMD_Issues;
            // ========= Meta Docs =========
            // command
            ChatCommands["command"] = metaCmds.CMD_Command;
            ChatCommands["commands"] = metaCmds.CMD_Command;
            ChatCommands["cmd"] = metaCmds.CMD_Command;
            ChatCommands["cmds"] = metaCmds.CMD_Command;
            ChatCommands["c"] = metaCmds.CMD_Command;
            // mechanism
            ChatCommands["mechanism"] = metaCmds.CMD_Mechanism;
            ChatCommands["mechanisms"] = metaCmds.CMD_Mechanism;
            ChatCommands["mech"] = metaCmds.CMD_Mechanism;
            ChatCommands["mechs"] = metaCmds.CMD_Mechanism;
            ChatCommands["mec"] = metaCmds.CMD_Mechanism;
            ChatCommands["mecs"] = metaCmds.CMD_Mechanism;
            ChatCommands["m"] = metaCmds.CMD_Mechanism;
            // tag
            ChatCommands["tag"] = metaCmds.CMD_Tag;
            ChatCommands["tags"] = metaCmds.CMD_Tag;
            ChatCommands["t"] = metaCmds.CMD_Tag;
            // event
            ChatCommands["event"] = metaCmds.CMD_Event;
            ChatCommands["events"] = metaCmds.CMD_Event;
            ChatCommands["evt"] = metaCmds.CMD_Event;
            ChatCommands["evts"] = metaCmds.CMD_Event;
            ChatCommands["e"] = metaCmds.CMD_Event;
            // action
            ChatCommands["action"] = metaCmds.CMD_Action;
            ChatCommands["actions"] = metaCmds.CMD_Action;
            ChatCommands["act"] = metaCmds.CMD_Action;
            ChatCommands["acts"] = metaCmds.CMD_Action;
            ChatCommands["a"] = metaCmds.CMD_Action;
            // language
            ChatCommands["language"] = metaCmds.CMD_Language;
            ChatCommands["languages"] = metaCmds.CMD_Language;
            ChatCommands["lang"] = metaCmds.CMD_Language;
            ChatCommands["langs"] = metaCmds.CMD_Language;
            ChatCommands["l"] = metaCmds.CMD_Language;
            // ========= Utility =========
            // TODO: CMD_DScript
            // ========= Admin =========
            ChatCommands["restart"] = adminCmds.CMD_Restart;
            ChatCommands["reload"] = adminCmds.CMD_Reload;
        }

        /// <summary>
        /// Shuts the bot down entirely.
        /// </summary>
        public void Shutdown()
        {
            Client.StopAsync().Wait();
            Client.Dispose();
            StoppedEvent.Set();
        }

        /// <summary>
        /// Returns whether meta commands are allowed in a given channel.
        /// </summary>
        /// <param name="channel">The channel to check.</param>
        /// <returns>True if allowed, false otherwise.</returns>
        public bool MetaCommandsAllowed(ISocketMessageChannel channel)
        {
            return ChannelToDetails.TryGetValue(channel.Id, out ChannelDetails details) && details.Docs;
        }

        /// <summary>
        /// Signaled when the bot is stopped.
        /// </summary>
        public ManualResetEvent StoppedEvent = new ManualResetEvent(false);

        /// <summary>
        /// Monitor object to help restart the bot as needed.
        /// </summary>
        public ConnectionMonitor BotMonitor;

        /// <summary>
        /// Channels the bot will reply in.
        /// </summary>
        public HashSet<ulong> ValidChannels = new HashSet<ulong>(32);

        /// <summary>
        /// Informational replies available, as a map of name to full text.
        /// </summary>
        public Dictionary<string, string> InformationalData = new Dictionary<string, string>(512);

        /// <summary>
        /// Informational replies names available, only including the primary names.
        /// </summary>
        public List<string> InformationalDataNames = new List<string>(128);

        /// <summary>
        /// A mapping from channel IDs to project names for the update command.
        /// </summary>
        public Dictionary<ulong, ChannelDetails> ChannelToDetails = new Dictionary<ulong, ChannelDetails>(512);

        /// <summary>
        /// A map of project names to the project's details.
        /// </summary>
        public Dictionary<string, ProjectDetails> ProjectToDetails = new Dictionary<string, ProjectDetails>(512);

        /// <summary>
        /// Fills fields with data from the config file.
        /// </summary>
        public void PopulateFromConfig()
        {
            ValidChannels.Clear();
            Constants.DOCS_URL_BASE = ConfigFile.GetString("url_base");
            foreach (string channel in ConfigFile.GetStringList("valid_channels"))
            {
                ValidChannels.Add(ulong.Parse(channel.Trim()));
            }
            FDSSection infoSection = ConfigFile.GetSection("info_replies");
            foreach (string key in infoSection.GetRootKeys())
            {
                string infoValue = infoSection.GetString(key);
                string[] keysSplit = key.SplitFast(',');
                InformationalDataNames.Add(keysSplit[0]);
                foreach (string name in keysSplit)
                {
                    InformationalData[name.Trim()] = infoValue;
                }
            }
            FDSSection projectDetailsSection = ConfigFile.GetSection("project_details");
            foreach (string key in projectDetailsSection.GetRootKeys())
            {
                FDSSection detailsSection = projectDetailsSection.GetSection(key);
                ProjectDetails detail = new ProjectDetails();
                detail.Name = key;
                detail.Icon = detailsSection.GetString("icon", "");
                detail.GitHub = detailsSection.GetString("github", "");
                detail.UpdateMessage = detailsSection.GetString("update", "");
                ProjectToDetails.Add(key.ToLowerFast(), detail);
            }
            FDSSection channelDetailsSection = ConfigFile.GetSection("channel_details");
            foreach (string key in channelDetailsSection.GetRootKeys())
            {
                FDSSection detailsSection = channelDetailsSection.GetSection(key);
                ChannelDetails detail = new ChannelDetails();
                List<ProjectDetails> projects = new List<ProjectDetails>();
                foreach (string projName in detailsSection.GetString("updates", "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    projects.Add(ProjectToDetails[projName]);
                }
                detail.Updates = projects.ToArray();
                detail.Docs = detailsSection.GetBool("docs", false).Value;
                ChannelToDetails.Add(ulong.Parse(key), detail);
            }
        }

        /// <summary>
        /// Initializes the bot object, connects, and runs the active loop.
        /// </summary>
        public void InitAndRun(string[] args)
        {
            Console.WriteLine("Preparing...");
            BotMonitor = new ConnectionMonitor(this);
            if (File.Exists(CONFIG_FILE))
            {
                lock (ConfigSaveLock)
                {
                    ConfigFile = FDSUtility.ReadFile(CONFIG_FILE);
                }
            }
            PopulateFromConfig();
            DefaultCommands();
            Console.WriteLine("Loading Discord...");
            DiscordSocketConfig config = new DiscordSocketConfig();
            config.MessageCacheSize = 256;
            //config.LogLevel = LogSeverity.Debug;
            Client = new DiscordSocketClient(config);
            /*Client.Log += (m) =>
            {
                Console.WriteLine(m.Severity + ": " + m.Source + ": " + m.Exception + ": "  + m.Message);
                return Task.CompletedTask;
            };*/
            Client.Ready += () =>
            {
                if (BotMonitor.ShouldStopAllLogic())
                {
                    return Task.CompletedTask;
                }
                BotMonitor.ConnectedCurrently = true;
                Client.SetGameAsync("Type !help").Wait();
                if (BotMonitor.ConnectedOnce)
                {
                    return Task.CompletedTask;
                }
                Console.WriteLine($"Args: {args.Length}");
                if (args.Length > 0 && ulong.TryParse(args[0], out ulong argument1))
                {
                    ISocketMessageChannel channelToNotify = Client.GetChannel(argument1) as ISocketMessageChannel;
                    Console.WriteLine($"Restarted as per request in channel: {channelToNotify.Name}");
                    channelToNotify.SendMessageAsync(embed: UserCommands.GetGenericPositiveMessageEmbed("Restarted", "Connected and ready!")).Wait();
                }
                BotMonitor.ConnectedOnce = true;
                return Task.CompletedTask;
            };
            Client.MessageReceived += (message) =>
            {
                if (BotMonitor.ShouldStopAllLogic())
                {
                    return Task.CompletedTask;
                }
                if (message.Author.Id == Client.CurrentUser.Id)
                {
                    return Task.CompletedTask;
                }
                BotMonitor.LoopsSilent = 0;
                if (message.Author.IsBot || message.Author.IsWebhook)
                {
                    return Task.CompletedTask;
                }
                if (message.Channel.Name.StartsWith("@") || !(message.Channel is SocketGuildChannel sgc))
                {
                    Console.WriteLine($"Refused message from ({message.Author.Username}): (Invalid Channel: {message.Channel.Name}): {message.Content}");
                    return Task.CompletedTask;
                }
                if (ValidChannels.Count != 0 && !ValidChannels.Contains(message.Channel.Id))
                {
                    Console.WriteLine($"Refused message from ({message.Author.Username}): (Non-whitelisted Channel: {message.Channel.Name}): {message.Content}");
                    return Task.CompletedTask;
                }
                bool mentionedMe = message.MentionedUsers.Any((su) => su.Id == Client.CurrentUser.Id);
                Console.WriteLine($"Parsing message from ({message.Author.Username}), in channel: {message.Channel.Name}: {message.Content}");
                if (mentionedMe)
                {
                    try
                    {
                        Respond(message);
                    }
                    catch (Exception ex)
                    {
                        if (ex is ThreadAbortException)
                        {
                            throw;
                        }
                        Console.WriteLine($"Error handling command: {ex.ToString()}");
                    }
                }
                return Task.CompletedTask;
            };
            Console.WriteLine("Starting monitor...");
            BotMonitor.StartMonitorLoop();
            Console.WriteLine("Logging in to Discord...");
            Client.LoginAsync(TokenType.Bot, TOKEN).Wait();
            Console.WriteLine("Connecting to Discord...");
            Client.StartAsync().Wait();
            Console.WriteLine("Running Discord!");
            StoppedEvent.WaitOne();
        }
    }
}
