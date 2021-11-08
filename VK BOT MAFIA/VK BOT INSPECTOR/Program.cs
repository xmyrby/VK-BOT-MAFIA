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

        static string GetStagePlayers(long playerId, int type, int id)
        {
            if(type==0)
            {
                command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id`!={playerId}";
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

            if (type == 0)
            {
                command.CommandText = $"SELECT `id` FROM `players` WHERE `lobbyid`={id} AND `id` != {playerId}";
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
                if(type==0)
                {
                    keyboard.AddButton(new AddButtonParams { Label = $"Застрелить {reader.GetString("id")}", Color = KeyboardButtonColor.Negative });
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

                        while (reader.Read())
                        {
                            mafiaId = reader.GetInt64("id");
                        }
                        reader.Close();

                        SendMessage(mafiaId,GetStagePlayers(mafiaId,0,id),GetStageKeyboard(mafiaId,0,id));

                        break;
                    };
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
            command.CommandText = $"SELECT `id` FROM `lobbies` WHERE `name` = '{lobby}' AND `stage` = 0 AND `players` < 10";
            MySqlDataReader reader = command.ExecuteReader();

            if (reader.Read())
            {
                int lobbyId = reader.GetInt32("id");
                reader.Close();
                command.CommandText = $"UPDATE `lobbies` SET `players`=`players`+1 WHERE `name` = '{lobby}';UPDATE `players` SET `lobbyid` = {lobbyId}, `inlobby`=1 WHERE `id` = {id}";
                command.ExecuteNonQuery();
                return true;
            }
            else
            {
                return false;
            }
        }

        static void CreateLobby(string name, long? id)
        {
            command.CommandText = $"INSERT INTO `lobbies`(`name`,`players`,`owner`,`stage`) VALUES('{name}', 1, {id},0);SELECT @lobby := MAX(`id`) FROM `lobbies`;UPDATE `players` SET `inlobby`=1,`lobbyid` = @lobby WHERE `id`={id}";
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
                    command.CommandText = $"UPDATE `players` SET `inlobby`=0,`lobbyid`=0 WHERE `id` = {id};SELECT @newowner := `id` FROM `players` WHERE `inlobby` = 1 AND `lobbyid`={lobby} LIMIT 1;UPDATE `lobbies` SET `owner`=@newowner, `players`=`players`-1 WHERE `id` = {lobby}";
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
                    command.CommandText = $"UPDATE `players` SET `inlobby`=0,`lobbyid`=0 WHERE `id` = {id};UPDATE `lobbies` SET `players`=`players`-1 WHERE `id` = {lobby}";
                    command.ExecuteNonQuery();
                }
            }
            else
            {
                command.CommandText = $"UPDATE `players` SET `inlobby`=0,`lobbyid`=0 WHERE `id` = {id};DELETE FROM `lobbies` WHERE `id` = {lobby}";
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

            command.CommandText = $"SELECT `name` FROM `lobbies` WHERE `players`<=10 ORDER BY `players` DESC LIMIT 10";
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
                keyboard.AddButton(new AddButtonParams { Label = $"Войти {reader.GetString("name")}", Color = KeyboardButtonColor.Default });
            }
            reader.Close();

            keyboard.AddLine();
            keyboard.AddButton(new AddButtonParams { Label = "Создать лобби", Color = KeyboardButtonColor.Primary });

            keyboard.SetOneTime();

            return keyboard.Build();
        }

        static string GetLobbies()
        {
            string answer = "";

            command.CommandText = $"SELECT * FROM `lobbies` WHERE `players`<10 AND `stage`=0 ORDER BY `players` DESC LIMIT 10";
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
                answer += $"👥 {reader.GetString("name")} {reader.GetInt16("players")}/10 Игроков\n";
            }
            reader.Close();

            answer += "\nЧтобы войти в лобби по ID напишите: Войти + ID лобби";

            return answer;
        }

        static void SendMessage(long? id, string message, MessageKeyboard keyboard)
        {
            api.Messages.Send(new MessagesSendParams
            {
                RandomId = rnd.Next(0, 1000000000),
                PeerId = id,
                Message = message,
                Keyboard = keyboard
            });
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
                reader.Close();
            }
            else
            {
                reader.Close();

                command.CommandText = $"INSERT INTO `players`(`id`,`inlobby`,`lobbyid`,`role`,`state`) VALUES({id},0,0,'',0)";
                command.ExecuteNonQuery();
                player.Id = id;
                player.InLobby = false;
                player.LobbyId = 0;
                player.Role = "";
                player.State = 0;
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
                                    StartGame(player.LobbyId);
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
                                            break;
                                        }
                                    case "застрелить":
                                        {
                                            long victimId = Convert.ToInt64(message.Text.Split(' ')[1]);
                                            Player victim = GetPlayer(victimId);
                                            if (player.Role=="Мафия" || GetLobbyStage(player.LobbyId)==1 || victim.LobbyId == player.LobbyId || victim.Role!="Мафия")
                                            {
                                                command.CommandText = $"UPDATE `lobbies` SET `stage`=2 WHERE `id`={player.LobbyId};UPDATE `players` SET `state`=`state`+1 WHERE `id`={victimId}";
                                                command.ExecuteNonQuery();

                                                SendMessage(player.Id, $"🔫 Вы застрелили @id{victimId} ({GetVkName(victimId)})",null);
                                            }
                                            else
                                            {
                                                UpdateStage(player.LobbyId,1);
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
