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
using System.Diagnostics;

namespace VK_BOT_INSPECTOR
{
    class Program
    {
        static List<string> swearing = new List<string>();
        static readonly VkApi api = new VkApi();

        static Random rnd = new Random();

        static MySqlConnection connection;
        static MySqlCommand command;

        static bool GetSpecialStateLobby(int id, string role)
        {
            bool res = false;
            command.CommandText = $"SELECT `{role}` FROM `lobbies` WHERE `id` = {id}";
            MySqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                if (reader.GetInt32($"{role}") == 1)
                {
                    res = true;
                }
            }
            reader.Close();

            return res;
        }

        static void CheckGame(int id)
        {
            command.CommandText = $"SELECT `id` FROM `lobbies` WHERE `id` = {id}";
            MySqlDataReader reader = command.ExecuteReader();

            if (reader.HasRows)
            {
                reader.Close();
                int count = 0, mcount = 0;
                command.CommandText = $"SELECT COUNT(*) as `count` FROM `players` WHERE `lobbyid` = {id} AND `role` != 'Мафия'  AND `role` !=''";
                reader = command.ExecuteReader();

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
                    SendMessage(2000000001, $"🤵 Игра {GetLobbyName(id)} завершилась победой мафии", null);
                    command.CommandText = $"UPDATE `players` SET `lobbyid`=0, `inlobby`=0, `role`='' WHERE `lobbyid`={id};DELETE FROM `lobbies` WHERE `id` = {id}";
                    command.ExecuteNonQuery();
                }
                else if (mcount < 1)
                {
                    SendLobbyMessage(id, $"👦 Победа мирных жителей!", true);
                    SendMessage(2000000001, $"👦 Игра {GetLobbyName(id)} завершилась победой мирных жителей", null);
                    command.CommandText = $"UPDATE `players` SET `lobbyid`=0, `inlobby`=0, `role`='' WHERE `lobbyid`={id};DELETE FROM `lobbies` WHERE `id` = {id}";
                    command.ExecuteNonQuery();
                }
            }
        }

        static string NightEnd(int id)
        {
            string answer = "🌙 Этой ночью\n";

            command.CommandText = $"SELECT `id`,`state`,`role`,`specialstate` FROM `players` WHERE (`state`!=0 OR `specialstate`!=0) AND `lobbyid`={id} AND `role` !=''";
            MySqlDataReader reader = command.ExecuteReader();

            long died = 0;
            long killed = 0;

            while (reader.Read())
            {
                long playerId = reader.GetInt64("id");
                int state = reader.GetInt16("state");
                int specialState = reader.GetInt16("specialstate");
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

                if (specialState % 10 == 1 && state % 100 / 10 == 0)
                {
                    answer += $"🧨 @id{playerId} ({GetVkName(playerId)}) был убит, его роль: {reader.GetString("role")}\n";
                    killed = playerId;
                }
                else if (specialState % 10 == 1 && state % 100 / 10 == 1)
                {
                    answer += $"💉 @id{playerId} ({GetVkName(playerId)}) был собран по кусочкам после взрыва\n";
                }
                if (specialState >= 10)
                {
                    answer += $"⚖ @id{playerId} ({GetVkName(playerId)}) под защитой адвоката, за него нельзя голосовать\n";
                }
            }
            reader.Close();

            if (died != 0)
            {
                command.CommandText = $"UPDATE `players` SET `role` = '' WHERE `id` = {died}";
                command.ExecuteNonQuery();
                SendMessage(died, "🔫 Тебя застрелили, но ты еще можешь наблюдать (Чтобы выйти из лобби - отправьте команду Выйти)", null);
            }
            if (killed != 0)
            {
                command.CommandText = $"UPDATE `players` SET `role` = '' WHERE `id` = {killed}";
                command.ExecuteNonQuery();
                SendMessage(killed, "🧨 Тебя Убили, но ты еще можешь наблюдать (Чтобы выйти из лобби - отправьте команду Выйти)", null);
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
            command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `vote`=0 AND `state`<100 AND `role` !=''";
            MySqlDataReader reader = command.ExecuteReader();

            if (reader.HasRows)
            {
                reader.Close();
            }
            else
            {
                reader.Close();

                command.CommandText = $"SELECT `vote` FROM `players` WHERE `lobbyid`={id} AND `role` !='' AND `vote`!=0";
                reader = command.ExecuteReader();

                List<long> voteIds = new List<long>();

                while (reader.Read())
                {
                    voteIds.Add(reader.GetInt64("vote"));
                }
                reader.Close();

                int max = 0;
                long voted;
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

                command.CommandText = $"UPDATE `players` SET `role` = '' WHERE `id` = {votedId}";
                command.ExecuteNonQuery();

                SendMessage(votedId, "💥 Тебя казнили, но ты можешь наблюдать за игрой (Чтобы выйти из лобби - отправьте команду Выйти)", null);

                CheckGame(id);

                SendLobbyMessage(id, "🌙 Ночь начинается!", false);
                command.CommandText = $"UPDATE `lobbies` SET `stage`=1 WHERE `id`={id}";
                command.ExecuteNonQuery();
                UpdateStage(id, 1);

                command.CommandText = $"UPDATE `players` SET `vote` =0, `state`=0,`specialstate`=0 WHERE `lobbyid` = {id};UPDATE `lobbies` SET `stage`=1 WHERE `id`={id}";
                command.ExecuteNonQuery();
            }
        }

        static string GetStagePlayers(long playerId, int type, int id)
        {
            switch (type)
            {
                case 0:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id`!={playerId} AND `role` !=''";
                        break;
                    }
                case 1:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `role` !=''";
                        break;
                    }
                case 2:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id`!={playerId} AND `role` !=''";
                        break;
                    }
                case 3:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id`!={playerId} AND `role` !=''";
                        break;
                    }
                case 4:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id`!={playerId} AND `role` !=''";
                        break;
                    }
                case 5:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id`!={playerId} AND `role` !=''";
                        break;
                    }
                case 6:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id`!={playerId} AND `role` !='' AND `specialstate`<10";
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
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id` != {playerId} AND `role` !=''";
                        break;
                    }
                case 1:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `role` !=''";
                        break;
                    }
                case 2:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id` != {playerId} AND `role` !=''";
                        break;
                    }
                case 3:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id` != {playerId} AND `role` !=''";
                        break;
                    }
                case 4:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id` != {playerId} AND `role` !=''";
                        break;
                    }
                case 5:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id` != {playerId} AND `role` !=''";
                        break;
                    }
                case 6:
                    {
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id` != {playerId} AND `role` !='' AND `specialstate`<10";
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
                            keyboard.AddButton(new AddButtonParams { Label = $"Убить {reader.GetString("id")}", Color = KeyboardButtonColor.Negative });
                            break;
                        }
                    case 5:
                        {
                            keyboard.AddButton(new AddButtonParams { Label = $"Оправдать {reader.GetString("id")}", Color = KeyboardButtonColor.Primary });
                            break;
                        }
                    case 6:
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
                        long yakuzaId = 0;
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid` = {id} AND `role`='Якудза'";
                        MySqlDataReader reader = command.ExecuteReader();

                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                yakuzaId = reader.GetInt64("id");
                            }
                            reader.Close();

                            SendMessage(yakuzaId, GetStagePlayers(yakuzaId, 4, id), GetStageKeyboard(yakuzaId, 4, id));
                        }
                        else
                        {
                            reader.Close();
                            command.CommandText = $"UPDATE `lobbies` SET `stage` = {6} WHERE `id`={id}";
                            command.ExecuteNonQuery();
                            UpdateStage(id, 6);
                        }

                        break;
                    }
                case 6:
                    {
                        long advocateId = 0;
                        command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid` = {id} AND `role`='Адвокат'";
                        MySqlDataReader reader = command.ExecuteReader();

                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                advocateId = reader.GetInt64("id");
                            }
                            reader.Close();

                            SendMessage(advocateId, GetStagePlayers(advocateId, 5, id), GetStageKeyboard(advocateId, 5, id));
                        }
                        else
                        {
                            reader.Close();
                            command.CommandText = $"UPDATE `lobbies` SET `stage` = {7} WHERE `id`={id}";
                            command.ExecuteNonQuery();
                            UpdateStage(id, 7);
                        }

                        break;
                    }
                case 7:
                    {
                        string NightEndS = NightEnd(id);
                        List<long> playersIds = new List<long>();
                        command.CommandText = $"SELECT `id`,`state`,`role` FROM `players` WHERE `lobbyid` = {id}";
                        MySqlDataReader reader = command.ExecuteReader();

                        while (reader.Read())
                        {
                            long playerId = reader.GetInt64("id");
                            SendMessage(playerId, NightEndS, null);
                            if (reader.GetInt16("state") < 100 && reader.GetString("role") != "")
                            {
                                playersIds.Add(playerId);
                            }
                        }
                        reader.Close();

                        for (int i = 0; i < playersIds.Count; i++)
                        {
                            SendMessage(playersIds[i], GetStagePlayers(playersIds[i], 6, id), GetStageKeyboard(playersIds[i], 6, id));
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

            if (GetSpecialStateLobby(id, "yakuza") == true)
            {
                randomId = rnd.Next(0, playersIds.Count);
                command.CommandText = $"UPDATE `players` SET `role`='Якудза' WHERE `id` = {playersIds[randomId]}";
                command.ExecuteNonQuery();
                SendMessage(playersIds[randomId], $"❗ Игра началась, Ваша роль: Якудза", null);
                playersIds.RemoveAt(randomId);
            }

            if (GetSpecialStateLobby(id, "advocate") == true)
            {
                randomId = rnd.Next(0, playersIds.Count);
                command.CommandText = $"UPDATE `players` SET `role`='Адвокат' WHERE `id` = {playersIds[randomId]}";
                command.ExecuteNonQuery();
                SendMessage(playersIds[randomId], $"❗ Игра началась, Ваша роль: Адвокат", null);
                playersIds.RemoveAt(randomId);
            }

            for (int i = 0; i < playersIds.Count; i++)
            {
                SendMessage(playersIds[i], $"❗ Игра началась, Ваша роль: Мирный житель", null);
            }

            UpdateStage(id, 1);

            SendMessage(2000000001, $"❗ Игра {GetLobbyName(id)} Началась", null);
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

        static string GetLobbyName(int id)
        {
            command.CommandText = $"SELECT `name` FROM `lobbies` WHERE `id`={id}";
            MySqlDataReader reader = command.ExecuteReader();

            string name = "";
            while (reader.Read())
            {
                name = reader.GetString("name");
            }
            reader.Close();
            return name;
        }

        static string GetVkName(long? id)
        {
            try
            {
                var user = api.Users.Get(new long[] { (long)(id) }, ProfileFields.FirstName | ProfileFields.LastName);

                return user[0].FirstName + " " + user[0].LastName;
            }
            catch
            {
                return "[GetVkName ERROR, Input id probably 0]";
            }
        }

        static void EnterMessage(int lobby, long? id, string type)
        {
            string name = GetVkName(id);

            KeyboardBuilder keyboard = new KeyboardBuilder();
            bool special = GetSpecialStateLobby(lobby, "yakuza");
            keyboard.AddButton(new AddButtonParams { Label = $"{(special == false ? '+' : '-')} Якудза", Color = (special == false ? KeyboardButtonColor.Positive : KeyboardButtonColor.Negative) });
            keyboard.AddLine();
            special = GetSpecialStateLobby(lobby, "advocate");
            keyboard.AddButton(new AddButtonParams { Label = $"{(special == false ? '+' : '-')} Адвокат", Color = (special == false ? KeyboardButtonColor.Positive : KeyboardButtonColor.Negative) });
            keyboard.AddLine();
            keyboard.AddButton(new AddButtonParams { Label = "Начать", Color = KeyboardButtonColor.Positive });
            keyboard.AddLine();
            keyboard.AddButton(new AddButtonParams { Label = "Выйти", Color = KeyboardButtonColor.Negative });
            keyboard.SetOneTime();


            command.CommandText = $"SELECT `players`.`id` FROM `players`,`lobbies` WHERE `lobbies`.`id` = {lobby} AND `owner`=`players`.`id` AND `players`.`id`!= {id} AND `players`.`lobbyid`=`lobbies`.`id`";
            MySqlDataReader reader = command.ExecuteReader();

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

        static void SpecialStateTriggerMessage(int lobby, string type)
        {
            KeyboardBuilder keyboard = new KeyboardBuilder();
            bool special = GetSpecialStateLobby(lobby, "yakuza");
            keyboard.AddButton(new AddButtonParams { Label = $"{(special == false ? '+' : '-')} Якудза", Color = (special == false ? KeyboardButtonColor.Positive : KeyboardButtonColor.Negative) });
            keyboard.AddLine();
            special = GetSpecialStateLobby(lobby, "advocate");
            keyboard.AddButton(new AddButtonParams { Label = $"{(special == false ? '+' : '-')} Адвокат", Color = (special == false ? KeyboardButtonColor.Positive : KeyboardButtonColor.Negative) });
            keyboard.AddLine();
            keyboard.AddButton(new AddButtonParams { Label = "Начать", Color = KeyboardButtonColor.Positive });
            keyboard.AddLine();
            keyboard.AddButton(new AddButtonParams { Label = "Выйти", Color = KeyboardButtonColor.Negative });
            keyboard.SetOneTime();


            command.CommandText = $"SELECT `players`.`id` FROM `players`,`lobbies` WHERE `lobbies`.`id` = {lobby} AND `owner`=`players`.`id` AND `players`.`lobbyid`=`lobbies`.`id`";
            MySqlDataReader reader = command.ExecuteReader();

            if (reader.Read())
            {
                SendMessage(reader.GetInt64("id"), $"💫 {type}", keyboard.Build());
            }
            reader.Close();

            command.CommandText = $"SELECT `players`.`id` FROM `players`,`lobbies` WHERE `lobbies`.`id` = {lobby} AND `owner`!=`players`.`id` AND `players`.`lobbyid`=`lobbies`.`id`";
            reader = command.ExecuteReader();

            keyboard.Clear();
            keyboard.AddButton(new AddButtonParams { Label = "Выйти", Color = KeyboardButtonColor.Negative });

            while (reader.Read())
            {
                SendMessage(reader.GetInt64("id"), $"💫 {type}", keyboard.Build());
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
                command.CommandText = $"UPDATE `lobbies` SET `players`=`players`+1 WHERE `name` = '{lobby}';UPDATE `players` SET `lobbyid` = {lobbyId}, `inlobby`=1, `state`=0,`specialstate`=0, `vote`=0 WHERE `id` = {id}";
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
            command.CommandText = $"INSERT INTO `lobbies`(`name`,`players`,`owner`,`stage`,`yakuza`,`advocate`) VALUES('{name}', 1, {id},0,0,0);SELECT @lobby := MAX(`id`) FROM `lobbies`;UPDATE `players` SET `inlobby`=1,`lobbyid` = @lobby, `state`=0,`specialstate`=0,`vote`=0 WHERE `id`={id}";
            command.ExecuteNonQuery();

            SendMessage(2000000001, $"‼ @id{id} ({GetVkName(id)}) Создал лобби {name}", null);
        }

        static void LeaveFromLobby(int lobby, long? id)
        {
            int stage = GetLobbyStage(lobby);
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
                    command.CommandText = $"UPDATE `players` SET `inlobby`=0,`lobbyid`=0,`state`=0,`specialstate`=0,`vote`=0,`role`='' WHERE `id` = {id};SELECT @newowner := `id` FROM `players` WHERE `inlobby` = 1 AND `lobbyid`={lobby} LIMIT 1;UPDATE `lobbies` SET `owner`=@newowner, `players`=`players`-1 WHERE `id` = {lobby}";
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

                        if (stage == 0)
                        {
                            SendMessage(reader.GetInt64("owner"), $"❗ Теперь вы владелец лобби", keyboard.Build());
                        }
                    }
                    reader.Close();
                }
                else
                {
                    command.CommandText = $"UPDATE `players` SET `inlobby`=0,`lobbyid`=0,`state`=0,`specialstate`=0,`vote`=0,`role`='' WHERE `id` = {id};UPDATE `lobbies` SET `players`=`players`-1 WHERE `id` = {lobby}";
                    command.ExecuteNonQuery();
                }
            }
            else
            {
                command.CommandText = $"UPDATE `players` SET `inlobby`=0,`lobbyid`=0,`state`=0,`specialstate`=0,`vote`=0,`role`='' WHERE `id` = {id};DELETE FROM `lobbies` WHERE `id` = {lobby}";
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

            keyboard.AddButton(new AddButtonParams { Label = "Найти лобби", Color = KeyboardButtonColor.Positive });
            keyboard.AddLine();
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
                player.SpecialState = reader.GetInt32("specialstate");
                player.Vote = reader.GetInt64("vote");
                reader.Close();
            }
            else
            {
                reader.Close();

                command.CommandText = $"INSERT INTO `players`(`id`,`inlobby`,`lobbyid`,`role`,`state`,`specialstate`,`vote`) VALUES({id},0,0,'',0,0,0)";
                command.ExecuteNonQuery();
                player.Id = id;
                player.InLobby = false;
                player.LobbyId = 0;
                player.Role = "";
                player.State = 0;
                player.SpecialState = 0;
                player.Vote = 0;
            }

            return player;
        }

        static void Main(string[] args)
        {
            try
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
                        if (conv[i].Conversation.Peer.Id != 2000000001)
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
                                            int add = 0;

                                            if (GetSpecialStateLobby(player.LobbyId, "yakuza") == true)
                                            {
                                                add++;
                                            }
                                            if (GetSpecialStateLobby(player.LobbyId, "advocate") == true)
                                            {
                                                add++;
                                            }

                                            if (GetLobbyPlayersCount(player.LobbyId) >= 4 + add)
                                            {
                                                StartGame(player.LobbyId);
                                            }
                                            else
                                            {
                                                KeyboardBuilder keyboard = new KeyboardBuilder();
                                                bool special = GetSpecialStateLobby(player.LobbyId, "yakuza");
                                                keyboard.AddButton(new AddButtonParams { Label = $"{(special == false ? '+' : '-')} Якудза", Color = (special == false ? KeyboardButtonColor.Positive : KeyboardButtonColor.Negative) });
                                                keyboard.AddLine();
                                                special = GetSpecialStateLobby(player.LobbyId, "advocate");
                                                keyboard.AddButton(new AddButtonParams { Label = $"{(special == false ? '+' : '-')} Адвокат", Color = (special == false ? KeyboardButtonColor.Positive : KeyboardButtonColor.Negative) });
                                                keyboard.AddLine();
                                                keyboard.AddButton(new AddButtonParams { Label = "Начать", Color = KeyboardButtonColor.Positive });
                                                keyboard.AddLine();
                                                keyboard.AddButton(new AddButtonParams { Label = "Выйти", Color = KeyboardButtonColor.Negative });
                                                keyboard.SetOneTime();

                                                SendMessage(player.Id, $"🚫 Нужно минимум {4 + add} игрока", keyboard.Build());
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

                                            player = GetPlayer(player.Id);

                                            KeyboardBuilder keyboard = new KeyboardBuilder();
                                            bool special = GetSpecialStateLobby(player.LobbyId, "yakuza");
                                            keyboard.AddButton(new AddButtonParams { Label = $"{(special == false ? '+' : '-')} Якудза", Color = (special == false ? KeyboardButtonColor.Positive : KeyboardButtonColor.Negative) });
                                            keyboard.AddLine();
                                            special = GetSpecialStateLobby(player.LobbyId, "advocate");
                                            keyboard.AddButton(new AddButtonParams { Label = $"{(special == false ? '+' : '-')} Адвокат", Color = (special == false ? KeyboardButtonColor.Positive : KeyboardButtonColor.Negative) });
                                            keyboard.AddLine();
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

                                        int stage = GetLobbyStage(player.LobbyId);

                                        if (player.InLobby == false)
                                        {
                                            SendMessage(player.Id, "🚫 Вы не в лобби", keyboard.Build());
                                        }
                                        else if (stage == 0 || player.Role == "")
                                        {
                                            if (stage == 0)
                                            {
                                                EnterMessage(player.LobbyId, player.Id, "вышел из");
                                            }
                                            LeaveFromLobby(player.LobbyId, player.Id);
                                            SendMessage(player.Id, "✅ Вы вышли из лобби", keyboard.Build());
                                        }

                                        break;
                                    }
                                case "+ якудза":
                                    {
                                        if (player.InLobby == true && GetLobbyOwner(player.LobbyId) == player.Id && GetLobbyStage(player.LobbyId) == 0)
                                        {
                                            command.CommandText = $"UPDATE `lobbies` SET `yakuza` = 1 WHERE `id` = {player.LobbyId}";
                                            command.ExecuteNonQuery();

                                            SpecialStateTriggerMessage(player.LobbyId, "Роль 'Якудза' будет доступна в этой игре");
                                        }

                                        break;
                                    }
                                case "- якудза":
                                    {
                                        if (player.InLobby == true && GetLobbyOwner(player.LobbyId) == player.Id && GetLobbyStage(player.LobbyId) == 0)
                                        {
                                            command.CommandText = $"UPDATE `lobbies` SET `yakuza` = 0 WHERE `id` = {player.LobbyId}";
                                            command.ExecuteNonQuery();

                                            SpecialStateTriggerMessage(player.LobbyId, "Роль 'Якудза' будет недоступна в этой игре");
                                        }

                                        break;
                                    }
                                case "+ адвокат":
                                    {
                                        if (player.InLobby == true && GetLobbyOwner(player.LobbyId) == player.Id && GetLobbyStage(player.LobbyId) == 0)
                                        {
                                            command.CommandText = $"UPDATE `lobbies` SET `advocate` = 1 WHERE `id` = {player.LobbyId}";
                                            command.ExecuteNonQuery();

                                            SpecialStateTriggerMessage(player.LobbyId, "Роль 'Адвокат' будет доступна в этой игре");
                                        }

                                        break;
                                    }
                                case "- адвокат":
                                    {
                                        if (player.InLobby == true && GetLobbyOwner(player.LobbyId) == player.Id && GetLobbyStage(player.LobbyId) == 0)
                                        {
                                            command.CommandText = $"UPDATE `lobbies` SET `advocate` = 0 WHERE `id` = {player.LobbyId}";
                                            command.ExecuteNonQuery();

                                            SpecialStateTriggerMessage(player.LobbyId, "Роль 'Адвокат' будет недоступна в этой игре");
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
                                                    if (player.Role == "Мафия" && GetLobbyStage(player.LobbyId) == 1 && victim.LobbyId == player.LobbyId && victim.Role != "Мафия" && victim.Role != "")
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
                                                    if (player.Role == "Доктор" && GetLobbyStage(player.LobbyId) == 2 && victim.LobbyId == player.LobbyId && victim.Role != "")
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
                                                    if (player.Role == "Любовница" && GetLobbyStage(player.LobbyId) == 3 && victim.LobbyId == player.LobbyId && victim.Role != "Любовница" && victim.Role != "")
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
                                                    if (player.Role == "Детектив" && GetLobbyStage(player.LobbyId) == 4 && victim.LobbyId == player.LobbyId && victim.Role != "Детектив" && victim.Role != "")
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
                                            case "убить":
                                                {
                                                    long victimId = Convert.ToInt64(message.Text.Split(' ')[1]);
                                                    Player victim = GetPlayer(victimId);
                                                    if (player.Role == "Якудза" && GetLobbyStage(player.LobbyId) == 5 && victim.LobbyId == player.LobbyId && victim.Role != "Якудза" && victim.Role != "")
                                                    {
                                                        command.CommandText = $"UPDATE `lobbies` SET `stage`=6 WHERE `id`={player.LobbyId};UPDATE `players` SET `specialstate`=`specialstate`+1 WHERE `id`={victimId}";
                                                        command.ExecuteNonQuery();
                                                        SendMessage(player.Id, $"🧨 Вы убили игрока @id{victimId} ({GetVkName(victimId)})", null);
                                                        UpdateStage(player.LobbyId, 6);
                                                    }
                                                    break;
                                                }
                                            case "оправдать":
                                                {
                                                    long victimId = Convert.ToInt64(message.Text.Split(' ')[1]);
                                                    Player victim = GetPlayer(victimId);
                                                    if (player.Role == "Адвокат" && GetLobbyStage(player.LobbyId) == 6 && victim.LobbyId == player.LobbyId && victim.Role != "Адвокат" && victim.Role != "")
                                                    {
                                                        command.CommandText = $"UPDATE `lobbies` SET `stage`=7 WHERE `id`={player.LobbyId};UPDATE `players` SET `specialstate`=`specialstate`+10 WHERE `id`={victimId}";
                                                        command.ExecuteNonQuery();
                                                        SendMessage(player.Id, $"⚖ Вы оправдали игрока @id{victimId} ({GetVkName(victimId)}), за него нельзя будет голосовать", null);
                                                        UpdateStage(player.LobbyId, 7);
                                                    }
                                                    break;
                                                }
                                            case "голосовать":
                                                {
                                                    long victimId = Convert.ToInt64(message.Text.Split(' ')[1]);
                                                    Player victim = GetPlayer(victimId);
                                                    if (player.State < 100 && GetLobbyStage(player.LobbyId) == 7 && victim.LobbyId == player.LobbyId && player.Vote == 0 && victim.Role != "" && player.Role != "" && victim.SpecialState < 10)
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
                                                    else if (player.Role == "")
                                                    {
                                                        SendMessage(player.Id, "💀 Вы мертвы", null);
                                                    }
                                                    else if (victim.SpecialState >= 10)
                                                    {
                                                        SendMessage(player.Id, "⚖ За этого игрока нельзя голосовать", GetStageKeyboard((long)player.Id, 6, player.LobbyId));
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
                    }
                    Thread.Sleep(1000);
                }
            }
            catch (Exception e)
            {
                connection.Close();
                Process.Start("VK BOT INSPECTOR.exe", rnd.Next(0, 100).ToString());
            }
        }
    }
}
