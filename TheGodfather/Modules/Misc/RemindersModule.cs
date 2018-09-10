﻿#region USING_DIRECTIVES
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;

using Humanizer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TheGodfather.Common;
using TheGodfather.Common.Attributes;
using TheGodfather.Exceptions;
using TheGodfather.Extensions;
using TheGodfather.Services;
#endregion

namespace TheGodfather.Modules.Misc
{
    [Group("remind")]
    [Description("Manage reminders. Group call resends a message after given time span.")]
    [Aliases("reminders", "reminder")]
    [UsageExamples("!remind 1h Drink water!")]
    [RequireOwnerOrPermissions(Permissions.Administrator)]
    [Cooldown(3, 5, CooldownBucketType.Channel), NotBlocked]
    public class RemindersModule : TheGodfatherModule
    {

        public RemindersModule(SharedData shared, DBService db)
            : base(shared, db)
        {
            this.ModuleColor = DiscordColor.LightGray;
        }


        [GroupCommand, Priority(3)]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [Description("Time span until reminder.")] TimeSpan timespan,
                                     [Description("Channel to send message to.")] DiscordChannel channel,
                                     [RemainingText, Description("What to send?")] string message)
            => this.AddAsync(ctx, timespan, channel, message);

        [GroupCommand, Priority(2)]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [Description("Channel to send message to.")] DiscordChannel channel,
                                     [Description("Time span until reminder.")] TimeSpan timespan,
                                     [RemainingText, Description("What to send?")] string message)
            => this.AddAsync(ctx, timespan, channel, message);

        [GroupCommand, Priority(1)]
        public Task ExecuteGroupAsync(CommandContext ctx,
                                     [Description("Time span until reminder.")] TimeSpan timespan,
                                     [RemainingText, Description("What to send?")] string message)
            => this.AddAsync(ctx, timespan, null, message);

        [GroupCommand, Priority(0)]
        public Task ExecuteGroupAsync(CommandContext ctx)
            => this.ListAsync(ctx);



        #region COMMAND_REMINDERS_ADD
        [Command("add"), Priority(2)]
        [Description("Schedule a new reminder. You can also specify a channel where to send the reminder.")]
        [Aliases("new", "+", "a", "+=", "<", "<<")]
        [UsageExamples("!remind add 1h Drink water!")]
        public async Task AddAsync(CommandContext ctx,
                                  [Description("Time span until reminder.")] TimeSpan timespan,
                                  [Description("Channel to send message to.")] DiscordChannel channel,
                                  [RemainingText, Description("What to send?")] string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new InvalidCommandUsageException("Missing time or repeat string.");

            if (message.Length > 120)
                throw new InvalidCommandUsageException("Message must be shorter than 120 characters.");

            channel = channel ?? ctx.Channel;

            if (timespan.TotalMinutes < 1 || timespan.TotalDays > 31)
                throw new InvalidCommandUsageException("Time span cannot be less than 1 minute or greater than 31 days.");

            DateTimeOffset when = DateTimeOffset.Now + timespan;

            var task = new SendMessageTaskInfo(ctx.Channel.Id, ctx.User.Id, message, when);
            await SavedTaskExecutor.ScheduleAsync(this.Shared, this.Database, ctx.Client, task);

            await this.InformAsync(ctx, StaticDiscordEmoji.AlarmClock, $"I will remind {channel.Mention} in {Formatter.Bold(timespan.Humanize(5))} ({when.ToUtcTimestamp()}) to:\n\n{Formatter.Italic(message)}", important: false);
        }

        [Command("add"), Priority(1)]
        public Task AddAsync(CommandContext ctx,
                            [Description("Channel to send message to.")] DiscordChannel channel,
                            [Description("Time span until reminder.")] TimeSpan timespan,
                            [RemainingText, Description("What to send?")] string message)
            => this.AddAsync(ctx, timespan, channel, message);

        [Command("add"), Priority(0)]
        public Task AddAsync(CommandContext ctx,
                            [Description("Time span until reminder.")] TimeSpan timespan,
                            [RemainingText, Description("What to send?")] string message)
            => this.AddAsync(ctx, timespan, null, message);
        #endregion

        #region COMMAND_REMINDERS_DELETE
        [Command("delete")]
        [Description("Unschedule a reminder.")]
        [Aliases("-", "remove", "rm", "del", "-=", ">", ">>", "unschedule")]
        [UsageExamples("!remind delete 1")]
        public async Task DeleteAsync(CommandContext ctx,
                                     [Description("Reminder ID.")] params int[] ids)
        {
            if (!ids.Any())
                throw new InvalidCommandUsageException("Missing IDs of reminders to remove.");

            var eb = new StringBuilder();
            foreach (int id in ids) {
                if (!this.Shared.TaskExecuters.ContainsKey(id) || !(this.Shared.TaskExecuters[id].TaskInfo is SendMessageTaskInfo)) {
                    eb.AppendLine($"Reminder with ID {Formatter.Bold(id.ToString())} does not exist!");
                    continue;
                }

                var ti = this.Shared.TaskExecuters[id].TaskInfo as SendMessageTaskInfo;
                if (ti.InitiatorId != ctx.User.Id) {
                    eb.AppendLine($"You didn't create reminder with ID {Formatter.Bold(id.ToString())}!");
                    continue;
                }

                await SavedTaskExecutor.UnscheduleAsync(this.Shared, id);
            }

            if (eb.Length > 0)
                await this.InformFailureAsync(ctx, $"Action finished with following warnings/errors:\n\n{eb.ToString()}");
            else
                await this.InformAsync(ctx, "Successfully removed all specified reminders.", important: false);
        }
        #endregion

        #region COMMAND_REMINDERS_LIST
        [Command("list")]
        [Description("List your registered reminders in the current channel.")]
        [Aliases("ls")]
        [UsageExamples("!remind list")]
        public Task ListAsync(CommandContext ctx)
        {
            IEnumerable<(int Id, SendMessageTaskInfo TExec)> remindTasks = this.Shared.TaskExecuters.Values
                .Where(t => t.TaskInfo is SendMessageTaskInfo)
                .Select(t => (t.Id, t.TaskInfo as SendMessageTaskInfo))
                .Where(t => t.Item2.InitiatorId == ctx.User.Id && t.Item2.ChannelId == ctx.Channel.Id);

            if (!remindTasks.Any())
                throw new CommandFailedException("No reminders meet the speficied criteria.");

            return ctx.SendCollectionInPagesAsync(
                $"Your reminders in this channel:",
                remindTasks,
                t => $"ID: {Formatter.Bold(t.Id.ToString())} ({t.TExec.ExecutionTime.ToUtcTimestamp()}):{Formatter.BlockCode(t.TExec.Message)}",
                this.ModuleColor,
                1
            );
        }
        #endregion

        #region COMMAND_REMINDERS_LISTALL
        [Command("listall")]
        [Description("List all registered reminders for the given channel.")]
        [Aliases("lsa")]
        [UsageExamples("!remind listall")]
        [RequirePrivilegedUser]
        public Task ListAllAsync(CommandContext ctx,
                                [Description("Channel.")] DiscordChannel channel = null)
        {
            channel = channel ?? ctx.Channel;

            IEnumerable<(int Id, SendMessageTaskInfo TExec)> remindTasks = this.Shared.TaskExecuters.Values
                .Where(t => t.TaskInfo is SendMessageTaskInfo)
                .Select(t => (t.Id, t.TaskInfo as SendMessageTaskInfo))
                .Where(t => t.Item2.ChannelId == ctx.Channel.Id);

            if (!remindTasks.Any())
                throw new CommandFailedException("No reminders meet the speficied criteria.");

            return ctx.SendCollectionInPagesAsync(
                $"All reminders in channel {channel.Name}:",
                remindTasks,
                t => $"ID: {Formatter.Bold(t.Id.ToString())}, UID: {Formatter.Bold(t.TExec.InitiatorId.ToString())} ({t.TExec.ExecutionTime.ToUtcTimestamp()}):{Formatter.BlockCode(t.TExec.Message)}",
                this.ModuleColor,
                1
            );
        }
        #endregion
    }
}