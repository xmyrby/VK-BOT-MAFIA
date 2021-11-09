using System.Threading;
using System;
using System.Collections.Generic;
using VkNet;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Model;
using VkNet.Model.RequestParams;
using VkNet.Utils;
using MySql.Data.MySqlClient;
using VkNet.Model.Keyboard;

namespace VK_BOT_INSPECTOR
{
    class Program
    {
        static List<string> swearing = new List<string>();
        static readonly VkApi api = new VkApi();

        static Random rnd = new Random();

        static MySqlConnection connection;
        static MySqlCommand command;

        static void CheckGame(int id)
        {
            int count = 0, mcount = 0;
            command.CommandText = $"SELECT COUNT(*) as `count` FROM `players` WHERE `lobbyid` = {id} AND `role` != 'Мафия'";
            MySqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                count = reader.GetInt32("count");
            }
            reader.Close();

            command.CommandText = $"SELECT COUNT(*) as `mcount` FROM `players` WHERE `lobbyid` = {id} AND `role` = 'Мафия'";
            reader = command.ExecuteReader();

            while (reader.Read())
            {
                mcount = reader.GetInt32("mcount");
            }
            reader.Close();

            if (count <= 1 && mcount >= 1)
            {
                long mafiaId = 0;
                command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid` = {id} AND `role` = 'Мафия'";
                reader = command.ExecuteReader();

                while (reader.Read())
                {
                    mafiaId = reader.GetInt64("id");
                }
                reader.Close();
                SendLobbyMessage(id, $"🤵 Победа мафии! Ею был @id{mafiaId} ({GetVkName(mafiaId)})", true);
                command.CommandText = $"UPDATE `players` SET `lobbyid`=0, `inlobby`=0 WHERE `lobbyid`={id};DELETE FROM `lobbies` WHERE `id` = {id}";
                command.ExecuteNonQuery();
            }
            else if (mcount < 1)
            {
                SendLobbyMessage(id, $"👦 Победа мирных жителей!", true);
                command.CommandText = $"UPDATE `players` SET `lobbyid`=0, `inlobby`=0 WHERE `lobbyid`={id};DELETE FROM `lobbies` WHERE `id` = {id}";
                command.ExecuteNonQuery();
            }
        }

        static string NightEnd(int id)
        {
            string answer = "🌙 Этой ночью\n";

            command.CommandText = $"SELECT `id`,`state`,`role` FROM `players` WHERE `state`!=0 AND `lobbyid`={id}";
            MySqlDataReader reader = command.ExecuteReader();

            long died = 0;

            while (reader.Read())
            {
                long playerId = reader.GetInt64("id");
                int state = reader.GetInt16("state");
                if (state % 100 == 1)
                {
                    answer += $"🔫 @id{playerId} ({GetVkName(playerId)}) был застрелен, его роль: {reader.GetString("role")}\n";
                    died = playerId;
                }
                else if (state % 100 == 11)
                {
                    answer += $"👻 @id{playerId} ({GetVkName(playerId)}) был вылечен после выстрела в голову\n";
                }
                else if (state % 100 == 10)
                {
                    answer += $"💚 @id{playerId} ({GetVkName(playerId)}) съел таблетки, но зря\n";
                }
                if (state / 100 >= 1)
                {
                    answer += $"💘 @id{playerId} ({GetVkName(playerId)}) встретил свою любовь, лишаясь возможности голосовать\n";
                }
            }
            reader.Close();

            if (died != 0)
            {
                LeaveFromLobby(id, died);
                KeyboardBuilder keyboard = new KeyboardBuilder();
                keyboard.AddButton(new AddButtonParams { Label = "Найти лобби", Color = KeyboardButtonColor.Positive });
                keyboard.AddLine();
                keyboard.AddButton(new AddButtonParams { Label = "Создать лобби", Color = KeyboardButtonColor.Primary });
                keyboard.SetOneTime();

                SendMessage(died, "🔫 Тебя застрелили, но ты можешь сыграть еще", keyboard.Build());
            }

            CheckGame(id);

            return answer;
        }

        static void SendLobbyMessage(int id, string message, bool end)
        {
            command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid` = {id}";
            MySqlDataReader reader = command.ExecuteReader();

            KeyboardBuilder keyboard = new KeyboardBuilder();
            keyboard.Clear();

            if (end)
            {
                keyboard.AddButton(new AddButtonParams { Label = "Найти лобби", Color = KeyboardButtonColor.Positive });
                keyboard.AddLine();
                keyboard.AddButton(new AddButtonParams { Label = "Создать лобби", Color = KeyboardButtonColor.Primary });
                keyboard.SetOneTime();
            }

            while (reader.Read())
            {
                SendMessage(reader.GetInt64("id"), message, keyboard.Build());
            }
            reader.Close();
        }

        static void CheckVote(int id)
        {
            command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `vote`=0 AND `state`<100";
            MySqlDataReader reader = command.ExecuteReader();

            if (reader.HasRows)
            {
                reader.Close();
            }
            else
            {
                reader.Close();

                command.CommandText = $"SELECT `vote` FROM `players` WHERE `lobbyid`={id}";
                reader = command.ExecuteReader();

                List<long> voteIds = new List<long>();

                while (reader.Read())
                {
                    voteIds.Add(reader.GetInt64("vote"));
                }
                reader.Close();

                int max = 0;
                long voted = 0;
                int count;
                long votedId = 0;
                for (int i = 0; i < voteIds.Count; i++)
                {
                    count = 1;
                    voted = voteIds[i];
                    for (int j = 0; j < voteIds.Count - i - 1; j++)
                    {
                        if (voteIds[i + j + 1] == voted)
                        {
                            count++;
                        }
                        if (count > max)
                        {
                            max = count;
                            votedId = voted;
                        }
                    }
                }

                string role = GetPlayer(votedId).Role;

                SendLobbyMessage(id, $"💥 @id{votedId} ({GetVkName(votedId)}) был казнён, его роль: {role} ({max} Проголосовали)", false);

                LeaveFromLobby(id, votedId);

                KeyboardBuilder keyboard = new KeyboardBuilder();
                keyboard.AddButton(new AddButtonParams { Label = "Найти лобби", Color = KeyboardButtonColor.Positive });
                keyboard.AddLine();
                keyboard.AddButton(new AddButtonParams { Label = "Создать лобби", Color = KeyboardButtonColor.Primary });
                keyboard.SetOneTime();

                SendMessage(votedId, "💥 Тебя казнили, но ты можешь сыграть еще", keyboard.Build());

                CheckGame(id);

                SendLobbyMessage(id, "🌙 Ночь начинается!", false);
                command.CommandText = $"UPDATE `lobbies` SET `stage`=1 WHERE `id`={id}";
                command.ExecuteNonQuery();
                UpdateStage(id, 1);

                command.CommandText = $"UPDATE `players` SET `vote` =0, `state`=0 WHERE `lobbyid` = {id};UPDATE `lobbies` SET `stage`=1 WHERE `id`={id}";
                command.ExecuteNonQuery();
            }
        }

        static string GetStagePlayers(long playerId, int type, int id)
        {
            switch (type)
            {
                case 0:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id`!={playerId}";
                        break;
                    }
                case 1:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id}";
                        break;
                    }
                case 2:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id`!={playerId}";
                        break;
                    }
                case 3:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id`!={playerId}";
                        break;
                    }
                case 4:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id`!={playerId}";
                        break;
                    }
                default: break;
            }

            MySqlDataReader reader = command.ExecuteReader();
            string answer = "👥 Список доступных игроков:\n";

            while (reader.Read())
            {
                long curId = reader.GetInt64("id");
                answer += $"{curId} @id{curId} ({GetVkName(curId)})\n";
            }
            reader.Close();
            return answer;
        }

        static MessageKeyboard GetStageKeyboard(long playerId, int type, int id)
        {
            KeyboardBuilder keyboard = new KeyboardBuilder();

            switch (type)
            {
                case 0:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id` != {playerId}";
                        break;
                    }
                case 1:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id}";
                        break;
                    }
                case 2:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id` != {playerId}";
                        break;
                    }
                case 3:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id` != {playerId}";
                        break;
                    }
                case 4:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id` != {playerId}";
                        break;
                    }
                default: break;
            }

            MySqlDataReader reader = command.ExecuteReader();
            int c = 0;
            while (reader.Read())
            {
                c++;
                if (c == 3)
                {
                    keyboard.AddLine();
                    c = 1;
                }
                switch (type)
                {
                    case 0:
                        {
                            keyboard.AddButton(new AddButtonParams { Label = $"Застрелить {reader.GetString("id")}", Color = KeyboardButtonColor.Negative });
                            break;
                        }
                    case 1:
                        {
                            keyboard.AddButton(new AddButtonParams { Label = $"Вылечить {reader.GetString("id")}", Color = KeyboardButtonColor.Positive });
                            break;
                        }
                    case 2:
                        {
                            keyboard.AddButton(new AddButtonParams { Label = $"Полюбить {reader.GetString("id")}", Color = KeyboardButtonColor.Default });
                            break;
                        }
                    case 3:
                        {
                            keyboard.AddButton(new AddButtonParams { Label = $"Проверить {reader.GetString("id")}", Color = KeyboardButtonColor.Primary });
                            break;
                        }
                    case 4:
                        {
                            keyboard.AddButton(new AddButtonParams { Label = $"Голосовать {reader.GetString("id")}", Color = KeyboardButtonColor.Primary });
                            break;
                        }
                    default: break;
                }
            }
            reader.Close();
            keyboard.SetOneTime();

            return keyboard.Build();
        }

        static void UpdateStage(int id, int curStage)
        {
            switch (curStage)
            {
                case 1:
                    {
                        long mafiaId = 0;
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid` = {id} AND `role`='Мафия'";
                        MySqlDataReader reader = command.ExecuteReader();

                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                mafiaId = reader.GetInt64("id");
                            }
                            reader.Close();

                            SendMessage(mafiaId, GetStagePlayers(mafiaId, 0, id), GetStageKeyboard(mafiaId, 0, id));
                        }
                        else
                        {
                            reader.Close();
                            command.CommandText = $"UPDATE `lobbies` SET `stage` = {2} WHERE `id`={id}";
                            command.ExecuteNonQuery();
                            UpdateStage(id, 2);
                        }

                        break;
                    };
                case 2:
                    {
                        long doctorId = 0;
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid` = {id} AND `role`='Доктор'";
                        MySqlDataReader reader = command.ExecuteReader();

                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                doctorId = reader.GetInt64("id");
                            }
                            reader.Close();

                            SendMessage(doctorId, GetStagePlayers(doctorId, 1, id), GetStageKeyboard(doctorId, 1, id));
                        }
                        else
                        {
                            reader.Close();
                            command.CommandText = $"UPDATE `lobbies` SET `stage` = {3} WHERE `id`={id}";
                            command.ExecuteNonQuery();
                            UpdateStage(id, 3);
                        }

                        break;
                    }
                case 3:
                    {
                        long loveId = 0;
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid` = {id} AND `role`='Любовница'";
                        MySqlDataReader reader = command.ExecuteReader();

                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                loveId = reader.GetInt64("id");
                            }
                            reader.Close();

                            SendMessage(loveId, GetStagePlayers(loveId, 2, id), GetStageKeyboard(loveId, 2, id));
                        }
                        else
                        {
                            reader.Close();
                            command.CommandText = $"UPDATE `lobbies` SET `stage` = {4} WHERE `id`={id}";
                            command.ExecuteNonQuery();
                            UpdateStage(id, 4);
                        }

                        break;
                    }
                case 4:
                    {
                        long detectiveId = 0;
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid` = {id} AND `role`='Детектив'";
                        MySqlDataReader reader = command.ExecuteReader();

                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                detectiveId = reader.GetInt64("id");
                            }
                            reader.Close();

                            SendMessage(detectiveId, GetStagePlayers(detectiveId, 3, id), GetStageKeyboard(detectiveId, 3, id));
                        }
                        else
                        {
                            reader.Close();
                            command.CommandText = $"UPDATE `lobbies` SET `stage` = {5} WHERE `id`={id}";
                            command.ExecuteNonQuery();
                            UpdateStage(id, 5);
                        }

                        break;
                    }
                case 5:
                    {
                        string NightEndS = NightEnd(id);
                        long loveId = 0;
                        List<long> playersIds = new List<long>();
                        command.CommandText = $"SELECT `id`,`state` FROM `players` WHERE `lobbyid` = {id}";
                        MySqlDataReader reader = command.ExecuteReader();

                        while (reader.Read())
                        {
                            if (reader.GetInt16("state") < 100)
                            {
                                playersIds.Add(reader.GetInt64("id"));
                            }
                            else
                            {
                                loveId = reader.GetInt32("id");
                            }
                        }
                        reader.Close();

                        if (loveId != 0)
                        {
                            SendMessage(loveId, NightEndS, null);
                        }

                        for (int i = 0; i < playersIds.Count; i++)
                        {
                            SendMessage(playersIds[i], NightEndS, null);
                            SendMessage(playersIds[i], GetStagePlayers(playersIds[i], 4, id), GetStageKeyboard(playersIds[i], 4, id));
                        }

                        break;
                    }
                default: break;
            }
        }

        static void StartGame(int id)
        {
            command.CommandText = $"UPDATE `lobbies` SET `stage` = 1 WHERE `id` = {id};UPDATE `players` SET `role` = 'Мирный житель' WHERE `lobbyid`={id}";
            command.ExecuteNonQuery();

            List<long> playersIds = new List<long>();

            command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid` = {id}";
            MySqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                playersIds.Add(reader.GetInt64("id"));
            }
            reader.Close();

            int randomId = rnd.Next(0, playersIds.Count);
            command.CommandText = $"UPDATE `players` SET `role`='Мафия' WHERE `id` = {playersIds[randomId]}";
            command.ExecuteNonQuery();
            SendMessage(playersIds[randomId], $"❗ Игра началась, Ваша роль: Мафия", null);
            playersIds.RemoveAt(randomId);

            randomId = rnd.Next(0, playersIds.Count);
            command.CommandText = $"UPDATE `players` SET `role`='Детектив' WHERE `id` = {playersIds[randomId]}";
            command.ExecuteNonQuery();
            SendMessage(playersIds[randomId], $"❗ Игра началась, Ваша роль: Детектив", null);
            playersIds.RemoveAt(randomId);

            randomId = rnd.Next(0, playersIds.Count);
            command.CommandText = $"UPDATE `players` SET `role`='Любовница' WHERE `id` = {playersIds[randomId]}";
            command.ExecuteNonQuery();
            SendMessage(playersIds[randomId], $"❗ Игра началась, Ваша роль: Любовница", null);
            playersIds.RemoveAt(randomId);

            randomId = rnd.Next(0, playersIds.Count);
            command.CommandText = $"UPDATE `players` SET `role`='Доктор' WHERE `id` = {playersIds[randomId]}";
            command.ExecuteNonQuery();
            SendMessage(playersIds[randomId], $"❗ Игра началась, Ваша роль: Доктор", null);
            playersIds.RemoveAt(randomId);

            for (int i = 0; i < playersIds.Count; i++)
            {
                SendMessage(playersIds[i], $"❗ Игра началась, Ваша роль: Мирный житель", null);
            }

            UpdateStage(id, 1);
        }

        static long GetLobbyOwner(int id)
        {
            command.CommandText = $"SELECT `owner` FROM `lobbies` WHERE `id`={id}";
            MySqlDataReader reader = command.ExecuteReader();

            long owner = 0;
            while (reader.Read())
            {
                owner = reader.GetInt32("owner");
            }
            reader.Close();
            return owner;
        }

        static int GetLobbyStage(int id)
        {
            command.CommandText = $"SELECT `stage` FROM `lobbies` WHERE `id`={id}";
            MySqlDataReader reader = command.ExecuteReader();

            int stage = 0;
            while (reader.Read())
            {
                stage = reader.GetInt32("stage");
            }
            reader.Close();
            return stage;
        }

        static string GetVkName(long? id)
        {
            var user = api.Users.Get(new long[] { (long)(id) }, ProfileFields.FirstName | ProfileFields.LastName);

            return user[0].FirstName + " " + user[0].LastName;
        }

        static void EnterMessage(int lobby, long? id, string type)
        {
            string name = GetVkName(id);

            command.CommandText = $"SELECT `players`.`id` FROM `players`,`lobbies` WHERE `lobbies`.`id` = {lobby} AND `owner`=`players`.`id` AND `players`.`id`!= {id} AND `players`.`lobbyid`=`lobbies`.`id`";
            MySqlDataReader reader = command.ExecuteReader();

            KeyboardBuilder keyboard = new KeyboardBuilder();
            keyboard.AddButton(new AddButtonParams { Label = "Начать", Color = KeyboardButtonColor.Positive });
            keyboard.AddLine();
            keyboard.AddButton(new AddButtonParams { Label = "Выйти", Color = KeyboardButtonColor.Negative });
            keyboard.SetOneTime();

            if (reader.Read())
            {
                SendMessage(reader.GetInt64("id"), $"⚠ @id{id} ({name}) {type} лобби", keyboard.Build());
            }
            reader.Close();

            command.CommandText = $"SELECT `players`.`id` FROM `players`,`lobbies` WHERE `lobbies`.`id` = {lobby} AND `owner`!=`players`.`id` AND `players`.`id`!= {id} AND `players`.`lobbyid`=`lobbies`.`id`";
            reader = command.ExecuteReader();

            keyboard.Clear();
            keyboard.AddButton(new AddButtonParams { Label = "Выйти", Color = KeyboardButtonColor.Negative });

            while (reader.Read())
            {
                SendMessage(reader.GetInt64("id"), $"⚠ @id{id} ({name}) {type} лобби", keyboard.Build());
            }
            reader.Close();


        }

        static bool EnterLobby(long? id, string lobby)
        {
            command.CommandText = $"SELECT `id` FROM `lobbies` WHERE `name` = '{lobby}' AND `stage` = 0 AND `players` < 15";
            MySqlDataReader reader = command.ExecuteReader();

            if (reader.Read())
            {
                int lobbyId = reader.GetInt32("id");
                reader.Close();
                command.CommandText = $"UPDATE `lobbies` SET `players`=`players`+1 WHERE `name` = '{lobby}';UPDATE `players` SET `lobbyid` = {lobbyId}, `inlobby`=1, `state`=0, `vote`=0 WHERE `id` = {id}";
                command.ExecuteNonQuery();
                return true;
            }
            else
            {
                reader.Close();
                return false;
            }
        }

        static void CreateLobby(string name, long? id)
        {
            command.CommandText = $"INSERT INTO `lobbies`(`name`,`players`,`owner`,`stage`) VALUES('{name}', 1, {id},0);SELECT @lobby := MAX(`id`) FROM `lobbies`;UPDATE `players` SET `inlobby`=1,`lobbyid` = @lobby, `state`=0,`vote`=0 WHERE `id`={id}";
            command.ExecuteNonQuery();
        }

        static void LeaveFromLobby(int lobby, long? id)
        {
            command.CommandText = $"SELECT * FROM `lobbies` WHERE `id` = {lobby}";
            MySqlDataReader reader = command.ExecuteReader();

            int players = 0;

            long owner = 0;

            while (reader.Read())
            {
                players = reader.GetInt16("players");
                owner = reader.GetInt64("owner");
            }
            reader.Close();

            if (players > 1)
            {
                if (owner == id)
                {
                    command.CommandText = $"UPDATE `players` SET `inlobby`=0,`lobbyid`=0,`state`=0,`vote`=0 WHERE `id` = {id};SELECT @newowner := `id` FROM `players` WHERE `inlobby` = 1 AND `lobbyid`={lobby} LIMIT 1;UPDATE `lobbies` SET `owner`=@newowner, `players`=`players`-1 WHERE `id` = {lobby}";
                    command.ExecuteNonQuery();

                    command.CommandText = $"SELECT `owner` FROM `lobbies` WHERE `id` = {lobby}";
                    reader = command.ExecuteReader();

                    if (reader.Read())
                    {
                        KeyboardBuilder keyboard = new KeyboardBuilder();
                        keyboard.AddButton(new AddButtonParams { Label = "Начать", Color = KeyboardButtonColor.Positive });
                        keyboard.AddLine();
                        keyboard.AddButton(new AddButtonParams { Label = "Выйти", Color = KeyboardButtonColor.Negative });
                        keyboard.SetOneTime();

                        SendMessage(reader.GetInt64("owner"), $"❗ Теперь вы владелец лобби", keyboard.Build());
                    }
                    reader.Close();
                }
                else
                {
                    command.CommandText = $"UPDATE `players` SET `inlobby`=0,`lobbyid`=0,`state`=0,`vote`=0 WHERE `id` = {id};UPDATE `lobbies` SET `players`=`players`-1 WHERE `id` = {lobby}";
                    command.ExecuteNonQuery();
                }
            }
            else
            {
                command.CommandText = $"UPDATE `players` SET `inlobby`=0,`lobbyid`=0,`state`=0,`vote`=0 WHERE `id` = {id};DELETE FROM `lobbies` WHERE `id` = {lobby}";
                command.ExecuteNonQuery();
            }
        }

        static string GenerateLobbyName()
        {
            string name = "";

            for (int i = 0; i < 8; i++)
            {
                int type = rnd.Next(0, 2);
                if (type == 0)
                {
                    name += (char)rnd.Next(65, 91);
                }
                else
                {
                    name += rnd.Next(0, 10);
                }
            }

            return name;
        }

        static MessageKeyboard GetLobbiesKeyboard()
        {
            KeyboardBuilder keyboard = new KeyboardBuilder();

            command.CommandText = $"SELECT `name` FROM `lobbies` WHERE `players`<15 AND `stage`=0 ORDER BY `players` DESC LIMIT 10";
            MySqlDataReader reader = command.ExecuteReader();
            int c = 0;
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    c++;
                    if (c == 3)
                    {
                        keyboard.AddLine();
                        c = 1;
                    }
                    keyboard.AddButton(new AddButtonParams { Label = $"Войти {reader.GetString("name")}", Color = KeyboardButtonColor.Default });
                }
                keyboard.AddLine();
            }
            reader.Close();

            keyboard.AddButton(new AddButtonParams { Label = "Создать лобби", Color = KeyboardButtonColor.Primary });

            keyboard.SetOneTime();

            return keyboard.Build();
        }

        static string GetLobbies()
        {
            string answer = "";

            command.CommandText = $"SELECT * FROM `lobbies` WHERE `players`<15 AND `stage`=0 ORDER BY `players` DESC LIMIT 10";
            MySqlDataReader reader = command.ExecuteReader();

            if (reader.HasRows)
            {
                answer = "📖 Список доступных лобби:\n\n";
            }
            else
            {
                answer = "🚫 Нет доступных лобби\n";
            }

            while (reader.Read())
            {
                answer += $"👥 {reader.GetString("name")} {reader.GetInt16("players")}/15 Игроков\n";
            }
            reader.Close();

            answer += "\nЧтобы войти в лобби по ID напишите: Войти + ID лобби";

            return answer;
        }

        static void SendMessage(long? id, string message, MessageKeyboard keyboard)
        {
            try
            {
                if (keyboard == null)
                {
                    keyboard = new KeyboardBuilder().Clear().Build();
                }
                api.Messages.Send(new MessagesSendParams
                {
                    RandomId = rnd.Next(0, 1000000000),
                    PeerId = id,
                    Message = message,
                    Keyboard = keyboard
                });
            }
            catch
            {

            }

        }

        static int GetLobbyPlayersCount(int id)
        {
            command.CommandText = $"SELECT `players` FROM `lobbies` WHERE `id`={id}";
            MySqlDataReader reader = command.ExecuteReader();

            int count = 0;

            while (reader.Read())
            {
                count = reader.GetInt16("players");
            }
            reader.Close();

            return count;
        }

        static Player GetPlayer(long? id)
        {
            Player player = new Player();
            command.CommandText = $"SELECT * FROM `players` WHERE `id` = {id}";
            MySqlDataReader reader = command.ExecuteReader();

            if (reader.HasRows)
            {
                reader.Read();
                player.Id = reader.GetInt64("id");
                player.InLobby = reader.GetBoolean("inlobby");
                player.LobbyId = reader.GetInt16("lobbyid");
                player.Role = reader.GetString("role");
                player.State = reader.GetInt32("state");
                player.Vote = reader.GetInt64("vote");
                reader.Close();
            }
            else
            {
                reader.Close();

                command.CommandText = $"INSERT INTO `players`(`id`,`inlobby`,`lobbyid`,`role`,`state`,`vote`) VALUES({id},0,0,'',0,0)";
                command.ExecuteNonQuery();
                player.Id = id;
                player.InLobby = false;
                player.LobbyId = 0;
                player.Role = "";
                player.State = 0;
                player.Vote = 0;
            }

            return player;
        }

        static void Main(string[] args)
        {
            api.Authorize(new ApiAuthParams
            {
                AccessToken = "7bf78b816fb08598ff7f7b97598ca01686a124758cbfcb0c6db9247872708e2655a48d8b28b58e3089dfb"
            });

            string connectionParameters = "Server=localhost;Database=mafia;Port=3306;User=root;Password=;SslMode=none;Allow User Variables=True";
            connection = new MySqlConnection(connectionParameters);
            connection.Open();
            command = new MySqlCommand() { Connection = connection };

            while (true)
            {
                var conv = api.Messages.GetConversations(new GetConversationsParams() { Filter = GetConversationFilter.Unread }).Items;

                int count = conv.Count;

                for (int i = 0; i < count; i++)
                {
                    Message message = conv[i].LastMessage;
                    Player player = GetPlayer(message.FromId);
                    switch (message.Text.ToLower())
                    {
                        case "начать":
                            {
                                if (player.InLobby == false)
                                {
                                    KeyboardBuilder keyboard = new KeyboardBuilder();
                                    keyboard.AddButton(new AddButtonParams { Label = "Найти лобби", Color = KeyboardButtonColor.Positive });
                                    keyboard.AddLine();
                                    keyboard.AddButton(new AddButtonParams { Label = "Создать лобби", Color = KeyboardButtonColor.Primary });
                                    keyboard.SetOneTime();

                                    SendMessage(player.Id, "🖐🏻 Добро пожаловать в мафию!\nХочешь сыграть?\nНайди лобби или создай своё!", keyboard.Build());
                                }
                                else if (player.InLobby == true && GetLobbyOwner(player.LobbyId) == player.Id && GetLobbyStage(player.LobbyId) == 0)
                                {
                                    if (GetLobbyPlayersCount(player.LobbyId) >= 4)
                                    {
                                        StartGame(player.LobbyId);
                                    }
                                    else
                                    {
                                        KeyboardBuilder keyboard = new KeyboardBuilder();
                                        keyboard.AddButton(new AddButtonParams { Label = "Начать", Color = KeyboardButtonColor.Positive });
                                        keyboard.AddLine();
                                        keyboard.AddButton(new AddButtonParams { Label = "Выйти", Color = KeyboardButtonColor.Negative });
                                        keyboard.SetOneTime();

                                        SendMessage(player.Id, $"🚫 Нужно минимум 4 игрока", keyboard.Build());
                                    }
                                }

                                break;
                            }
                        case "найти лобби":
                            {
                                if (player.InLobby == false)
                                {
                                    SendMessage(player.Id, GetLobbies(), GetLobbiesKeyboard());
                                }
                                else
                                {
                                    KeyboardBuilder keyboard = new KeyboardBuilder();
                                    keyboard.AddButton(new AddButtonParams { Label = "Выйти", Color = KeyboardButtonColor.Negative });
                                    keyboard.SetOneTime();

                                    SendMessage(player.Id, "🚫 Вы уже в лобби", keyboard.Build());
                                }

                                break;
                            }
                        case "создать лобби":
                            {
                                if (player.InLobby == false)
                                {
                                    string lobbyName = GenerateLobbyName();

                                    CreateLobby(lobbyName, player.Id);

                                    KeyboardBuilder keyboard = new KeyboardBuilder();
                                    keyboard.AddButton(new AddButtonParams { Label = "Начать", Color = KeyboardButtonColor.Positive });
                                    keyboard.AddLine();
                                    keyboard.AddButton(new AddButtonParams { Label = "Выйти", Color = KeyboardButtonColor.Negative });
                                    keyboard.SetOneTime();

                                    SendMessage(player.Id, $"✅ Вы создали лобби с именем {lobbyName}", keyboard.Build());
                                }
                                else
                                {
                                    KeyboardBuilder keyboard = new KeyboardBuilder();
                                    keyboard.AddButton(new AddButtonParams { Label = "Выйти", Color = KeyboardButtonColor.Negative });
                                    keyboard.SetOneTime();

                                    SendMessage(player.Id, "🚫 Вы уже в лобби", keyboard.Build());
                                }

                                break;
                            }
                        case "выйти":
                            {
                                KeyboardBuilder keyboard = new KeyboardBuilder();
                                keyboard.AddButton(new AddButtonParams { Label = "Найти лобби", Color = KeyboardButtonColor.Positive });
                                keyboard.AddLine();
                                keyboard.AddButton(new AddButtonParams { Label = "Создать лобби", Color = KeyboardButtonColor.Primary });
                                keyboard.SetOneTime();

                                if (player.InLobby == false)
                                {
                                    SendMessage(player.Id, "🚫 Вы не в лобби", keyboard.Build());
                                }
                                else if (GetLobbyStage(player.LobbyId) == 0)
                                {
                                    EnterMessage(player.LobbyId, player.Id, "вышел из");
                                    LeaveFromLobby(player.LobbyId, player.Id);
                                    SendMessage(player.Id, "✅ Вы вышли из лобби", keyboard.Build());
                                }

                                break;
                            }
                        default:
                            {
                                string splited = message.Text.ToLower().Split(' ')[0];
                                switch (splited)
                                {
                                    case "войти":
                                        {
                                            string lobby = message.Text.Split(' ')[1].ToUpper();

                                            bool enter = EnterLobby(player.Id, lobby);
                                            if (player.InLobby == false)
                                            {
                                                if (enter)
                                                {
                                                    player = GetPlayer(player.Id);
                                                    KeyboardBuilder keyboard = new KeyboardBuilder();
                                                    keyboard.AddButton(new AddButtonParams { Label = "Выйти", Color = KeyboardButtonColor.Negative });
                                                    keyboard.SetOneTime();

                                                    EnterMessage(player.LobbyId, player.Id, "присоединился к");
                                                    SendMessage(player.Id, $"✅ Вы вошли в лобби {lobby}", keyboard.Build());
                                                }
                                                else
                                                {
                                                    KeyboardBuilder keyboard = new KeyboardBuilder();
                                                    keyboard.AddButton(new AddButtonParams { Label = "Найти лобби", Color = KeyboardButtonColor.Positive });
                                                    keyboard.AddLine();
                                                    keyboard.AddButton(new AddButtonParams { Label = "Создать лобби", Color = KeyboardButtonColor.Primary });
                                                    keyboard.SetOneTime();

                                                    SendMessage(player.Id, $"🚫 Такого лобби нет", keyboard.Build());
                                                }
                                            }
                                            else
                                            {
                                                KeyboardBuilder keyboard = new KeyboardBuilder();
                                                keyboard.AddButton(new AddButtonParams { Label = "Выйти", Color = KeyboardButtonColor.Negative });
                                                keyboard.SetOneTime();

                                                SendMessage(player.Id, $"🚫 Вы уже в лобби", keyboard.Build());
                                            }

                                            break;
                                        }
                                    case "застрелить":
                                        {
                                            long victimId = Convert.ToInt64(message.Text.Split(' ')[1]);
                                            Player victim = GetPlayer(victimId);
                                            if (player.Role == "Мафия" && GetLobbyStage(player.LobbyId) == 1 && victim.LobbyId == player.LobbyId && victim.Role != "Мафия")
                                            {
                                                command.CommandText = $"UPDATE `lobbies` SET `stage`=2 WHERE `id`={player.LobbyId};UPDATE `players` SET `state`=`state`+1 WHERE `id`={victimId}";
                                                command.ExecuteNonQuery();

                                                SendMessage(player.Id, $"🔫 Вы застрелили @id{victimId} ({GetVkName(victimId)})", null);
                                                UpdateStage(player.LobbyId, 2);
                                            }
                                            break;
                                        }
                                    case "вылечить":
                                        {
                                            long victimId = Convert.ToInt64(message.Text.Split(' ')[1]);
                                            Player victim = GetPlayer(victimId);
                                            if (player.Role == "Доктор" && GetLobbyStage(player.LobbyId) == 2 && victim.LobbyId == player.LobbyId)
                                            {
                                                command.CommandText = $"UPDATE `lobbies` SET `stage`=3 WHERE `id`={player.LobbyId};UPDATE `players` SET `state`=`state`+10 WHERE `id`={victimId}";
                                                command.ExecuteNonQuery();

                                                SendMessage(player.Id, $"💚 Вы вылечили @id{victimId} ({GetVkName(victimId)})", null);
                                                UpdateStage(player.LobbyId, 3);
                                            }
                                            break;
                                        }
                                    case "полюбить":
                                        {
                                            long victimId = Convert.ToInt64(message.Text.Split(' ')[1]);
                                            Player victim = GetPlayer(victimId);
                                            if (player.Role == "Любовница" && GetLobbyStage(player.LobbyId) == 3 && victim.LobbyId == player.LobbyId && victim.Role != "Любовница")
                                            {
                                                command.CommandText = $"UPDATE `lobbies` SET `stage`=4 WHERE `id`={player.LobbyId};UPDATE `players` SET `state`=`state`+100 WHERE `id`={victimId}";
                                                command.ExecuteNonQuery();

                                                SendMessage(player.Id, $"💘 Вы полюбили @id{victimId} ({GetVkName(victimId)})", null);
                                                UpdateStage(player.LobbyId, 4);
                                            }
                                            break;
                                        }
                                    case "проверить":
                                        {
                                            long victimId = Convert.ToInt64(message.Text.Split(' ')[1]);
                                            Player victim = GetPlayer(victimId);
                                            if (player.Role == "Детектив" && GetLobbyStage(player.LobbyId) == 4 && victim.LobbyId == player.LobbyId && victim.Role != "Детектив")
                                            {
                                                command.CommandText = $"UPDATE `lobbies` SET `stage`=5 WHERE `id`={player.LobbyId}";
                                                command.ExecuteNonQuery();
                                                if (victim.Role != "Мирный житель")
                                                {
                                                    SendMessage(player.Id, $"🔎 Вы проверили игрока @id{victimId} ({GetVkName(victimId)}), кажется он что-то скрывает", null);
                                                }
                                                else
                                                {
                                                    SendMessage(player.Id, $"🔎 Вы проверили игрока @id{victimId} ({GetVkName(victimId)}), он чист", null);
                                                }
                                                UpdateStage(player.LobbyId, 5);
                                            }
                                            break;
                                        }
                                    case "голосовать":
                                        {
                                            long victimId = Convert.ToInt64(message.Text.Split(' ')[1]);
                                            Player victim = GetPlayer(victimId);
                                            if (player.State < 100 && GetLobbyStage(player.LobbyId) == 5 && victim.LobbyId == player.LobbyId && player.Vote == 0)
                                            {
                                                command.CommandText = $"UPDATE `players` SET `vote`={victimId} WHERE `id`={player.Id}";
                                                command.ExecuteNonQuery();
                                                SendMessage(player.Id, "💥 Вы успешно проголосовали", null);
                                                CheckVote(player.LobbyId);
                                            }
                                            else if (player.State >= 100)
                                            {
                                                SendMessage(player.Id, "💘 Вас полюбили, голосовать нельзя", null);
                                            }
                                            else if (player.Vote != 0)
                                            {
                                                SendMessage(player.Id, "💥 Вы уже проголосовали", null);
                                            }
                                            break;
                                        }
                                    default:
                                        {
                                            break;
                                        }
                                }
                                break;
                            }
                    }
                }

                Thread.Sleep(1000);
            }
        }
    }
}
