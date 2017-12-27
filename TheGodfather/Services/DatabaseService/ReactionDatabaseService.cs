﻿#region USING_DIRECTIVES
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Npgsql;
using NpgsqlTypes;
#endregion

namespace TheGodfather.Services
{
    public partial class DatabaseService
    {
        #region TEXT_REACTION_SERVICES
        public async Task<Dictionary<ulong, Dictionary<string, string>>> GetAllTextReactionsAsync()
        {
            await _sem.WaitAsync();

            var triggers = new Dictionary<ulong, Dictionary<string, string>>();

            using (var con = new NpgsqlConnection(_connectionString))
            using (var cmd = con.CreateCommand()) {
                await con.OpenAsync().ConfigureAwait(false);

                cmd.CommandText = "SELECT * FROM gf.text_reactions;";

                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false)) {
                    while (await reader.ReadAsync().ConfigureAwait(false)) {
                        ulong gid = (ulong)(long)reader["gid"];
                        if (triggers.ContainsKey(gid)) {
                            if (triggers[gid] == null)
                                triggers[gid] = new Dictionary<string, string>();
                        } else {
                            triggers.Add(gid, new Dictionary<string, string>());
                        }
                        triggers[gid].Add((string)reader["trigger"], (string)reader["response"]);
                    }
                }
            }

            _sem.Release();
            return triggers;
        }

        public async Task AddTextReactionAsync(ulong gid, string trigger, string response)
        {
            await _sem.WaitAsync();

            using (var con = new NpgsqlConnection(_connectionString))
            using (var cmd = con.CreateCommand()) {
                await con.OpenAsync().ConfigureAwait(false);

                cmd.CommandText = "INSERT INTO gf.text_reactions VALUES (@gid, @trigger, @response);";
                cmd.Parameters.AddWithValue("gid", NpgsqlDbType.Bigint, gid);
                cmd.Parameters.AddWithValue("trigger", NpgsqlDbType.Varchar, trigger);
                cmd.Parameters.AddWithValue("response", NpgsqlDbType.Varchar, response);

                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            _sem.Release();
        }

        public async Task DeleteTextReactionAsync(ulong gid, string trigger)
        {
            await _sem.WaitAsync();

            using (var con = new NpgsqlConnection(_connectionString))
            using (var cmd = con.CreateCommand()) {
                await con.OpenAsync().ConfigureAwait(false);

                cmd.CommandText = "DELETE FROM gf.text_reactions WHERE gid = @gid AND trigger = @trigger;";
                cmd.Parameters.AddWithValue("gid", NpgsqlDbType.Bigint, gid);
                cmd.Parameters.AddWithValue("trigger", NpgsqlDbType.Varchar, trigger);

                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            _sem.Release();
        }

        public async Task DeleteAllGuildTextReactionsAsync(ulong gid)
        {
            await _sem.WaitAsync();

            using (var con = new NpgsqlConnection(_connectionString))
            using (var cmd = con.CreateCommand()) {
                await con.OpenAsync().ConfigureAwait(false);

                cmd.CommandText = "DELETE FROM gf.text_reactions WHERE gid = @gid;";
                cmd.Parameters.AddWithValue("gid", NpgsqlDbType.Bigint, gid);

                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            _sem.Release();
        }
        #endregion

        #region EMOJI_REACTION_SERVICES
        public async Task<Dictionary<ulong, Dictionary<string, string>>> GetAllEmojiReactionsAsync()
        {
            await _sem.WaitAsync();

            var triggers = new Dictionary<ulong, Dictionary<string, string>>();

            using (var con = new NpgsqlConnection(_connectionString))
            using (var cmd = con.CreateCommand()) {
                await con.OpenAsync().ConfigureAwait(false);

                cmd.CommandText = "SELECT * FROM gf.emoji_reactions;";

                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false)) {
                    while (await reader.ReadAsync().ConfigureAwait(false)) {
                        ulong gid = (ulong)(long)reader["gid"];
                        if (triggers.ContainsKey(gid)) {
                            if (triggers[gid] == null)
                                triggers[gid] = new Dictionary<string, string>();
                        } else {
                            triggers.Add(gid, new Dictionary<string, string>());
                        }
                        triggers[gid].Add((string)reader["trigger"], (string)reader["reaction"]);
                    }
                }
            }

            _sem.Release();
            return triggers;
        }

        public async Task AddEmojiReactionAsync(ulong gid, string trigger, string reaction)
        {
            await _sem.WaitAsync();

            using (var con = new NpgsqlConnection(_connectionString))
            using (var cmd = con.CreateCommand()) {
                await con.OpenAsync().ConfigureAwait(false);

                cmd.CommandText = "INSERT INTO gf.emoji_reactions VALUES (@gid, @trigger, @reaction);";
                cmd.Parameters.AddWithValue("gid", NpgsqlDbType.Bigint, gid);
                cmd.Parameters.AddWithValue("trigger", NpgsqlDbType.Varchar, trigger);
                cmd.Parameters.AddWithValue("reaction", NpgsqlDbType.Varchar, reaction);

                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            _sem.Release();
        }

        public async Task DeleteEmojiReactionAsync(ulong gid, string trigger)
        {
            await _sem.WaitAsync();

            using (var con = new NpgsqlConnection(_connectionString))
            using (var cmd = con.CreateCommand()) {
                await con.OpenAsync().ConfigureAwait(false);

                cmd.CommandText = "DELETE FROM gf.emoji_reactions WHERE gid = @gid AND trigger = @trigger;";
                cmd.Parameters.AddWithValue("gid", NpgsqlDbType.Bigint, gid);
                cmd.Parameters.AddWithValue("trigger", NpgsqlDbType.Varchar, trigger);

                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            _sem.Release();
        }

        public async Task DeleteAllGuildEmojiReactionsAsync(ulong gid)
        {
            await _sem.WaitAsync();

            using (var con = new NpgsqlConnection(_connectionString))
            using (var cmd = con.CreateCommand()) {
                await con.OpenAsync().ConfigureAwait(false);

                cmd.CommandText = "DELETE FROM gf.emoji_reactions WHERE gid = @gid;";
                cmd.Parameters.AddWithValue("gid", NpgsqlDbType.Bigint, gid);

                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            _sem.Release();
        }
        #endregion
    }
}