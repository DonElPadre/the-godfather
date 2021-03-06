﻿#region USING_DIRECTIVES
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Humanizer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using TheGodfather.Common;
using TheGodfather.Common.Attributes;
using TheGodfather.Database;
using TheGodfather.Exceptions;
using TheGodfather.Extensions;
using TheGodfather.Modules.Administration.Extensions;
using TheGodfather.Modules.Owner.Common;
#endregion

namespace TheGodfather.Modules.Owner
{
    [Group("owner"), Module(ModuleType.Owner), Hidden]
    [Description("Owner-only bot administration commands.")]
    [Aliases("admin", "o")]
    [Cooldown(3, 5, CooldownBucketType.Global)]
    public partial class OwnerModule : TheGodfatherModule
    {

        public OwnerModule(SharedData shared, DatabaseContextBuilder db)
            : base(shared, db)
        {
            this.ModuleColor = DiscordColor.NotQuiteBlack;
        }


        #region COMMAND_ANNOUNCE
        [Command("announce"), NotBlocked, UsesInteractivity]
        [Description("Send a message to all guilds the bot is in.")]
        [Aliases("a", "ann")]
        [UsageExampleArgs("SPAM SPAM")]
        [RequireOwner]
        public async Task AnnounceAsync(CommandContext ctx,
                                       [RemainingText, Description("Message to send.")] string message)
        {
            if (!await ctx.WaitForBoolReplyAsync($"Are you sure you want to announce the message:\n\n{Formatter.BlockCode(FormatterExtensions.StripMarkdown(message))}"))
                return;

            var emb = new DiscordEmbedBuilder {
                Title = "An announcement from my owner",
                Description = message,
                Color = DiscordColor.Red
            };

            var eb = new StringBuilder();
            foreach (TheGodfatherShard shard in TheGodfather.ActiveShards) {
                foreach (DiscordGuild guild in shard.Client.Guilds.Values) {
                    try {
                        await guild.GetDefaultChannel().SendMessageAsync(embed: emb.Build());
                    } catch {
                        eb.AppendLine($"Warning: Failed to send a message to {guild.ToString()}");
                    }
                }
            }

            if (eb.Length > 0)
                await this.InformFailureAsync(ctx, $"Action finished with following errors:\n\n{eb.ToString()}");
            else
                await this.InformAsync(ctx, StaticDiscordEmoji.Information, "Sent the message to all guilds!", important: false);
        }
        #endregion

        #region COMMAND_BOTAVATAR
        [Command("botavatar"), NotBlocked]
        [Description("Set bot avatar.")]
        [Aliases("setbotavatar", "setavatar")]
        [UsageExampleArgs("http://someimage.png")]
        [RequireOwner]
        public async Task SetBotAvatarAsync(CommandContext ctx,
                                           [Description("URL.")] Uri url)
        {
            if (!await this.IsValidImageUriAsync(url))
                throw new CommandFailedException("URL must point to an image and use http or https protocols.");

            try {
                using (Stream stream = await _http.GetStreamAsync(url))
                using (var ms = new MemoryStream()) {
                    await stream.CopyToAsync(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    await ctx.Client.UpdateCurrentUserAsync(avatar: ms);
                }
            } catch (WebException e) {
                throw new CommandFailedException("Web exception thrown while fetching the image.", e);
            }

            await this.InformAsync(ctx, "Successfully changed the bot avatar!", important: false);
        }
        #endregion

        #region COMMAND_BOTNAME
        [Command("botname"), NotBlocked]
        [Description("Set bot name.")]
        [Aliases("setbotname", "setname")]
        [UsageExampleArgs("TheBotfather")]
        [RequireOwner]
        public async Task SetBotNameAsync(CommandContext ctx,
                                         [RemainingText, Description("New name.")] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidCommandUsageException("Name missing.");

            await ctx.Client.UpdateCurrentUserAsync(username: name);
            await this.InformAsync(ctx, $"Renamed the current bot user to {Formatter.Bold(ctx.Client.CurrentUser.Username)}");
        }
        #endregion

        #region COMMAND_CLEARLOG
        [Command("clearlog"), NotBlocked, UsesInteractivity]
        [Description("Clear bot logs.")]
        [Aliases("clearlogs", "deletelogs", "deletelog")]
        [RequireOwner]
        public async Task ClearLogAsync(CommandContext ctx)
        {
            if (!await ctx.WaitForBoolReplyAsync("Are you sure you want to clear the logs?"))
                return;

            if (!this.Shared.LogProvider.ClearLog())
                throw new CommandFailedException("Failed to delete log file!");

            await this.InformAsync(ctx, $"Logs cleared!", important: false);
        }
        #endregion

        #region COMMAND_DBQUERY
        [Command("dbquery"), NotBlocked, Priority(0)]
        [Description("Execute SQL query on the bot database.")]
        [Aliases("sql", "dbq", "q")]
        [UsageExampleArgs("SELECT * FROM gf.msgcount;")]
        [RequireOwner]
        public async Task DatabaseQuery(CommandContext ctx,
                                       [RemainingText, Description("SQL Query.")] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new InvalidCommandUsageException("Query missing.");

            var res = new List<IReadOnlyDictionary<string, string>>();
            using (DatabaseContext db = this.Database.CreateContext())
            using (Microsoft.EntityFrameworkCore.Storage.RelationalDataReader dr = await db.Database.ExecuteSqlQueryAsync(query)) {
                DbDataReader reader = dr.DbDataReader;
                while (await reader.ReadAsync()) {
                    var dict = new Dictionary<string, string>();

                    for (int i = 0; i < reader.FieldCount; i++)
                        dict[reader.GetName(i)] = reader[i] is DBNull ? "<null>" : reader[i].ToString();

                    res.Add(new ReadOnlyDictionary<string, string>(dict));
                }
            }

            if (!res.Any() || !res.First().Any()) {
                await this.InformAsync(ctx, StaticDiscordEmoji.Information, "No results returned (this is alright if your query wasn't a SELECT query).");
                return;
            }

            int maxlen = 1 + res
                .First()
                .Select(r => r.Key)
                .OrderByDescending(r => r.Length)
                .First()
                .Length;

            await ctx.SendCollectionInPagesAsync(
                "Query results",
                res.Take(25),
                row => {
                    var sb = new StringBuilder();
                    foreach ((string col, string val) in row)
                        sb.Append(col).Append(new string(' ', maxlen - col.Length)).Append("| ").AppendLine(val);
                    return Formatter.BlockCode(sb.ToString());
                },
                this.ModuleColor,
                1
            );
        }

        [Command("dbquery"), Priority(1)]
        public async Task DatabaseQuery(CommandContext ctx)
        {
            if (!ctx.Message.Attachments.Any())
                throw new CommandFailedException("Either write a query or attach a .sql file containing it!");

            DiscordAttachment attachment = ctx.Message.Attachments.FirstOrDefault(att => att.FileName.EndsWith(".sql"));
            if (attachment is null)
                throw new CommandFailedException("No .sql files attached!");

            string query;
            try {
                query = await _http.GetStringAsync(attachment.Url).ConfigureAwait(false);
            } catch (Exception e) {
                this.Shared.LogProvider.Log(LogLevel.Debug, e);
                throw new CommandFailedException("An error occured while getting the file.", e);
            }

            await this.DatabaseQuery(ctx, query);
        }
        #endregion

        #region COMMAND_EVAL
        [Command("eval"), NotBlocked]
        [Description("Evaluates a snippet of C# code, in context. Surround the code in the code block.")]
        [Aliases("compile", "run", "e", "c", "r")]
        [UsageExampleArgs("\\`\\`\\`await Context.RespondAsync(\"Hello!\");\\`\\`\\`")]
        [RequireOwner]
        public async Task EvaluateAsync(CommandContext ctx,
                                       [RemainingText, Description("Code to evaluate.")] string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidCommandUsageException("Code missing.");

            int cs1 = code.IndexOf("```") + 3;
            int cs2 = code.LastIndexOf("```");
            if (cs1 == -1 || cs2 == -1)
                throw new InvalidCommandUsageException("You need to wrap the code into a code block.");
            code = code.Substring(cs1, cs2 - cs1);

            var emb = new DiscordEmbedBuilder {
                Title = "Evaluating...",
                Color = this.ModuleColor
            };

            DiscordMessage msg = await ctx.RespondAsync(embed: emb.Build());

            var globals = new EvaluationEnvironment(ctx);
            ScriptOptions sopts = ScriptOptions.Default
                .WithImports("System", "System.Collections.Generic", "System.Linq", "System.Net.Http",
                    "System.Net.Http.Headers", "System.Reflection", "System.Text", "System.Text.RegularExpressions",
                    "System.Threading.Tasks", "DSharpPlus", "DSharpPlus.CommandsNext", "DSharpPlus.Entities",
                    "DSharpPlus.Interactivity")
                .WithReferences(AppDomain.CurrentDomain.GetAssemblies().Where(xa => !xa.IsDynamic && !string.IsNullOrWhiteSpace(xa.Location)));

            var sw1 = Stopwatch.StartNew();
            Script<object> snippet = CSharpScript.Create(code, sopts, typeof(EvaluationEnvironment));
            System.Collections.Immutable.ImmutableArray<Diagnostic> diag = snippet.Compile();
            sw1.Stop();

            if (diag.Any(d => d.Severity == DiagnosticSeverity.Error)) {
                emb = new DiscordEmbedBuilder {
                    Title = "Compilation failed",
                    Description = $"Compilation failed after {sw1.ElapsedMilliseconds.ToString("#,##0")}ms with {diag.Length} errors.",
                    Color = DiscordColor.Red
                };

                foreach (Diagnostic d in diag.Take(3)) {
                    FileLinePositionSpan ls = d.Location.GetLineSpan();
                    emb.AddField($"Error at line: {ls.StartLinePosition.Line}, {ls.StartLinePosition.Character}", Formatter.InlineCode(d.GetMessage()));
                }

                if (diag.Length > 3) {
                    emb.AddField("Some errors were omitted", $"{diag.Length - 3} errors not displayed");
                }

                if (!(msg is null))
                    msg = await msg.ModifyAsync(embed: emb.Build());
                return;
            }

            Exception exc = null;
            ScriptState<object> css = null;
            var sw2 = Stopwatch.StartNew();
            try {
                css = await snippet.RunAsync(globals);
                exc = css.Exception;
            } catch (Exception e) {
                exc = e;
            }
            sw2.Stop();

            if (!(exc is null)) {
                emb = new DiscordEmbedBuilder {
                    Title = "Execution failed",
                    Description = $"Execution failed after {sw2.ElapsedMilliseconds.ToString("#,##0")}ms with {Formatter.InlineCode($"{exc.GetType()} : {exc.Message}")}.",
                    Color = this.ModuleColor
                };

                if (!(msg is null))
                    msg = await msg.ModifyAsync(embed: emb.Build());
                return;
            }

            emb = new DiscordEmbedBuilder {
                Title = "Evaluation successful",
                Color = DiscordColor.Aquamarine
            };

            emb.AddField("Result", css.ReturnValue is null ? "No value returned" : css.ReturnValue.ToString(), false);
            emb.AddField("Compilation time", string.Concat(sw1.ElapsedMilliseconds.ToString("#,##0"), "ms"), true);
            emb.AddField("Execution time", string.Concat(sw2.ElapsedMilliseconds.ToString("#,##0"), "ms"), true);

            if (!(css.ReturnValue is null))
                emb.AddField("Return type", css.ReturnValue.GetType().ToString(), true);

            if (!(msg is null))
                await msg.ModifyAsync(embed: emb.Build());
            else
                await ctx.RespondAsync(embed: emb.Build());
        }
        #endregion

        #region COMMAND_FILELOG
        [Command("filelog"), NotBlocked]
        [Description("Toggle writing to log file.")]
        [Aliases("setfl", "fl", "setfilelog")]
        [UsageExampleArgs("on", "off")]
        [RequireOwner]
        public Task FileLogAsync(CommandContext ctx,
                                [Description("Enable?")] bool enable = true)
        {
            this.Shared.LogProvider.LogToFile = enable;
            return this.InformAsync(ctx, $"File logging {(enable ? "enabled" : "disabled")}", important: false);
        }
        #endregion

        #region COMMAND_GENERATECOMMANDS
        [Command("generatecommandlist"), NotBlocked]
        [Description("Generates a markdown command-list. You can also provide a folder for the output.")]
        [Aliases("cmdlist", "gencmdlist", "gencmds", "gencmdslist")]
        [UsageExampleArgs("Temp/blabla.md")]
        [RequireOwner]
        public async Task GenerateCommandListAsync(CommandContext ctx,
                                                  [RemainingText, Description("File path.")] string path = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                path = "Documentation";

            DirectoryInfo current;
            DirectoryInfo parts;
            try {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
                current = Directory.CreateDirectory(path);
                parts = Directory.CreateDirectory(Path.Combine(current.FullName, "Parts"));
            } catch (IOException e) {
                throw new CommandFailedException("Failed to create the directories!", e);
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Command list");
            sb.AppendLine();

            IReadOnlyList<Command> commands = ctx.CommandsNext.GetAllRegisteredCommands();
            var modules = commands
                .GroupBy(c => ModuleAttribute.ForCommand(c))
                .OrderBy(g => g.Key.Module)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.QualifiedName).ToList());

            foreach ((ModuleAttribute mattr, List<Command> cmdlist) in modules) {
                sb.Append("# Module: ").Append(mattr.Module.ToString()).AppendLine().AppendLine();

                foreach (Command cmd in cmdlist) {
                    if (cmd is CommandGroup || cmd.Parent is null)
                        sb.Append("## ").Append(cmd is CommandGroup ? "Group: " : "").AppendLine(cmd.QualifiedName);
                    else
                        sb.Append("### ").AppendLine(cmd.QualifiedName);

                    sb.AppendLine("<details><summary markdown='span'>Expand for additional information</summary><p>").AppendLine();

                    if (cmd.IsHidden)
                        sb.AppendLine(Formatter.Italic("Hidden.")).AppendLine();

                    sb.AppendLine(Formatter.Italic(cmd.Description ?? "No description provided.")).AppendLine();

                    IEnumerable<CheckBaseAttribute> execChecks = cmd.ExecutionChecks.AsEnumerable();
                    CommandGroup parent = cmd.Parent;
                    while (!(parent is null)) {
                        execChecks = execChecks.Union(parent.ExecutionChecks);
                        parent = parent.Parent;
                    }

                    IEnumerable<string> perms = execChecks
                        .Where(chk => chk is RequirePermissionsAttribute)
                        .Select(chk => chk as RequirePermissionsAttribute)
                        .Select(chk => chk.Permissions.ToPermissionString())
                        .Union(execChecks
                            .Where(chk => chk is RequireOwnerOrPermissionsAttribute)
                            .Select(chk => chk as RequireOwnerOrPermissionsAttribute)
                            .Select(chk => chk.Permissions.ToPermissionString())
                        );
                    IEnumerable<string> uperms = execChecks
                        .Where(chk => chk is RequireUserPermissionsAttribute)
                        .Select(chk => chk as RequireUserPermissionsAttribute)
                        .Select(chk => chk.Permissions.ToPermissionString());
                    IEnumerable<string> bperms = execChecks
                        .Where(chk => chk is RequireBotPermissionsAttribute)
                        .Select(chk => chk as RequireBotPermissionsAttribute)
                        .Select(chk => chk.Permissions.ToPermissionString());

                    if (execChecks.Any(chk => chk is RequireOwnerAttribute))
                        sb.AppendLine(Formatter.Bold("Owner-only.")).AppendLine();
                    if (execChecks.Any(chk => chk is RequirePrivilegedUserAttribute))
                        sb.AppendLine(Formatter.Bold("Privileged users only.")).AppendLine();

                    if (perms.Any()) {
                        sb.AppendLine(Formatter.Bold("Requires permissions:"));
                        sb.AppendLine(Formatter.InlineCode(string.Join(", ", perms))).AppendLine();
                    }
                    if (uperms.Any()) {
                        sb.AppendLine(Formatter.Bold("Requires user permissions:"));
                        sb.AppendLine(Formatter.InlineCode(string.Join(", ", uperms))).AppendLine();
                    }
                    if (bperms.Any()) {
                        sb.AppendLine(Formatter.Bold("Requires bot permissions:"));
                        sb.AppendLine(Formatter.InlineCode(string.Join(", ", bperms))).AppendLine();
                    }

                    if (cmd.Aliases.Any()) {
                        sb.AppendLine(Formatter.Bold("Aliases:"));
                        sb.AppendLine(Formatter.InlineCode(string.Join(", ", cmd.Aliases))).AppendLine();
                    }

                    foreach (CommandOverload overload in cmd.Overloads.OrderByDescending(o => o.Priority)) {
                        if (!overload.Arguments.Any())
                            continue;

                        sb.AppendLine(Formatter.Bold(cmd.Overloads.Count > 1 ? $"Overload {overload.Priority.ToString()}:" : "Arguments:")).AppendLine();
                        foreach (CommandArgument arg in overload.Arguments) {
                            if (arg.IsOptional)
                                sb.Append("(optional) ");

                            string type = $"[{ctx.CommandsNext.GetUserFriendlyTypeName(arg.Type)}";
                            if (arg.IsCatchAll)
                                type += "...";
                            type += "]";

                            sb.Append(Formatter.InlineCode(type));
                            sb.Append(" : ");

                            sb.Append(string.IsNullOrWhiteSpace(arg.Description) ? "No description provided." : Formatter.Italic(arg.Description));

                            if (arg.IsOptional)
                                sb.Append(" (def: ").Append(Formatter.InlineCode(arg.DefaultValue is null ? "None" : arg.DefaultValue.ToString())).Append(")");

                            sb.AppendLine().AppendLine();
                        }
                    }

                    if (cmd.CustomAttributes.FirstOrDefault(chk => chk is UsageExampleArgsAttribute) is UsageExampleArgsAttribute examples)
                        sb.AppendLine(Formatter.Bold("Examples:")).AppendLine().AppendLine(Formatter.BlockCode(examples.JoinExamples(cmd, ctx), "xml"));

                    sb.AppendLine("</p></details>").AppendLine().AppendLine("---").AppendLine();
                }

                string filename = Path.Combine(parts.FullName, $"{mattr.Module.ToString()}.md");
                try {
                    File.WriteAllText(filename, sb.ToString());
                } catch (IOException e) {
                    throw new CommandFailedException($"IO Exception occured while saving {filename}!", e);
                }

                sb.Clear();
            }

            sb.AppendLine("# Command modules:");
            foreach ((ModuleAttribute mattr, List<Command> cmdlist) in modules) {
                string mname = mattr.Module.ToString();
                sb.Append("  - ").Append('[').Append(mname).Append(']').Append("(").Append(parts.Name).Append('/').Append(mname).Append(".md").AppendLine(")");
            }

            try {
                File.WriteAllText(Path.Combine(current.FullName, $"README.md"), sb.ToString());
            } catch (IOException e) {
                throw new CommandFailedException($"IO Exception occured while saving the main file!", e);
            }

            await this.InformAsync(ctx, $"Command list created at: {Formatter.InlineCode(current.FullName)}!", important: false);
        }
        #endregion

        #region COMMAND_LEAVEGUILDS
        [Command("leaveguilds"), NotBlocked]
        [Description("Leaves the given guilds.")]
        [Aliases("leave", "gtfo")]
        [UsageExampleArgs("337570344149975050", "337570344149975050 201315884709576708")]
        [RequireOwner]
        public async Task LeaveGuildsAsync(CommandContext ctx,
                                          [Description("Guild ID list.")] params ulong[] gids)
        {
            if (gids is null || !gids.Any())
                throw new InvalidCommandUsageException("IDs missing.");

            var eb = new StringBuilder();
            foreach (ulong gid in gids) {
                try {
                    if (ctx.Client.Guilds.TryGetValue(gid, out DiscordGuild guild))
                        await guild.LeaveAsync();
                    else
                        eb.AppendLine($"Warning: I am not a member of the guild with ID: {Formatter.InlineCode(gid.ToString())}!");
                } catch {
                    eb.AppendLine($"Error: Failed to leave guild with ID: {Formatter.InlineCode(gid.ToString())}!");
                }
            }

            if (gids.All(gid => gid != ctx.Guild.Id)) {
                if (eb.Length > 0)
                    await this.InformFailureAsync(ctx, $"Action finished with following errors:\n\n{eb.ToString()}");
                else
                    await this.InformAsync(ctx, StaticDiscordEmoji.Information, "Successfully left all given guilds!", important: false);
            }
        }
        #endregion

        #region COMMAND_LOG
        [Command("log"), NotBlocked, Priority(1)]
        [Description("Upload the bot log file or add a remark to it.")]
        [Aliases("getlog", "remark", "rem")]
        [UsageExampleArgs("debug Hello world!")]
        [RequireOwner]
        public async Task LogAsync(CommandContext ctx, 
                                  [Description("Bypass current configuration and search file anyway?")] bool bypassConfig = false)
        {
            if (!bypassConfig && !this.Shared.BotConfiguration.LogToFile)
                throw new CommandFailedException("Logs aren't dumped to any files.");
            var fi = new FileInfo(this.Shared.BotConfiguration.LogPath);
            if (fi.Exists && fi.Length > 8 * 1024 * 1024)
                throw new CommandFailedException("The file is too big to upload!");
            using (var fs = new FileStream(this.Shared.BotConfiguration.LogPath, FileMode.Open))
                await ctx.RespondWithFileAsync(fs);
        }

        [Command("log"), NotBlocked, Priority(0)]
        public Task LogAsync(CommandContext ctx,
                            [Description("Log level.")] string level,
                            [RemainingText, Description("Remark.")] string text)
        {
            if (!Enum.TryParse(level.Titleize(), out LogLevel logLevel))
                throw new CommandFailedException($"Invalid log level!");
            this.Shared.LogProvider.Log(logLevel, text);
            return this.InformAsync(ctx, "Done!", important: false);
        }
        #endregion

        #region COMMAND_SENDMESSAGE
        [Command("sendmessage"), NotBlocked]
        [Description("Sends a message to a user or channel.")]
        [Aliases("send", "s")]
        [UsageExampleArgs("u 303463460233150464 Hi to user!", "c 120233460278590414 Hi to channel!")]
        [RequirePrivilegedUser]
        public async Task SendAsync(CommandContext ctx,
                                   [Description("u/c (for user or channel.)")] string desc,
                                   [Description("User/Channel ID.")] ulong xid,
                                   [RemainingText, Description("Message.")] string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new InvalidCommandUsageException();

            if (desc == "u") {
                DiscordDmChannel dm = await ctx.Client.CreateDmChannelAsync(xid);
                if (dm is null)
                    throw new CommandFailedException("I can't talk to that user...");
                await dm.SendMessageAsync(message);
            } else if (desc == "c") {
                DiscordChannel channel = await ctx.Client.GetChannelAsync(xid);
                await channel.SendMessageAsync(message);
            } else {
                throw new InvalidCommandUsageException("Descriptor can only be 'u' or 'c'.");
            }

            await this.InformAsync(ctx, $"Successfully sent the given message!", important: false);
        }
        #endregion

        #region COMMAND_SHUTDOWN
        [Command("shutdown"), Priority(1), NotBlocked]
        [Description("Triggers the dying in the vineyard scene (power off the bot).")]
        [Aliases("disable", "poweroff", "exit", "quit")]
        [UsageExampleArgs("10s")]
        [RequirePrivilegedUser]
        public Task ExitAsync(CommandContext _,
                             [Description("Time until shutdown.")] TimeSpan timespan,
                             [Description("Exit code.")] int exitCode = 0)
            => TheGodfather.Stop(exitCode, timespan);

        [Command("shutdown"), Priority(0)]
        public Task ExitAsync(CommandContext _,
                             [Description("Exit code.")] int exitCode = 0)
            => TheGodfather.Stop(exitCode);
        #endregion

        #region COMMAND_SUDO
        [Command("sudo"), NotBlocked]
        [Description("Executes a command as another user.")]
        [Aliases("execas", "as")]
        [UsageExampleArgs("@Someone rate")]
        [RequireOwner]
        public Task SudoAsync(CommandContext ctx,
                             [Description("Member to execute as.")] DiscordMember member,
                             [RemainingText, Description("Command text to execute.")] string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new InvalidCommandUsageException("Missing command to execute.");

            Command cmd = ctx.CommandsNext.FindCommand(command, out string args);
            CommandContext fctx = ctx.CommandsNext.CreateFakeContext(member, ctx.Channel, command, ctx.Prefix, cmd, args);
            return cmd is null ? Task.CompletedTask : ctx.CommandsNext.ExecuteCommandAsync(fctx);
        }
        #endregion

        #region COMMAND_TOGGLEIGNORE
        [Command("toggleignore")]
        [Description("Toggle bot's reaction to commands.")]
        [Aliases("ti")]
        [RequirePrivilegedUser]
        public Task ToggleIgnoreAsync(CommandContext ctx)
        {
            this.Shared.ListeningStatus = !this.Shared.ListeningStatus;
            return this.InformAsync(ctx, $"Listening status set to: {Formatter.Bold(this.Shared.ListeningStatus.ToString())}", important: false);
        }
        #endregion

        #region COMMAND_UPDATE
        [Command("update"), NotBlocked]
        [Description("Update and restart the bot.")]
        [Aliases("upd", "u")]
        [RequireOwner]
        public Task UpdateAsync(CommandContext ctx)
        {
            ProcessStartInfo psi;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                psi = new ProcessStartInfo {
                    FileName = "bash",
                    Arguments = $"install.sh {Process.GetCurrentProcess().Id}",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    CreateNoWindow = true
                };
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                psi = new ProcessStartInfo {
                    FileName = "install.bat",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    CreateNoWindow = true
                };
            } else {
                throw new CommandFailedException("Cannot determine host OS (OSX is not supported)!");
            }

            var proc = new Process { StartInfo = psi };
            proc.Start();
            return this.ExitAsync(ctx);
        }
        #endregion
    }
}
