using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Newtonsoft.Json;

namespace pr3_serverr
{
    public class Program
    {
        public static List<Leaders> Leaders = new List<Leaders>();
        public static List<ViewModelUserSettings> remoteIPAddress = new List<ViewModelUserSettings>();
        public static List<ViewModelGames> viewModelGames = new List<ViewModelGames>();
        private static int localPort = 6000;
        public static int MaxSpeed = 15;
        static void Main(string[] args)
        {
            try
            {
                Thread tRec = new Thread(new ThreadStart(Receiver));
                tRec.Start();
                Thread tTime = new Thread(Timer);
                tTime.Start();
            }
            catch(Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Возникло исключение: " + ex.ToString() + "\n " + ex.Message);
            }
        }
        public static int AddSnake()
        {
            ViewModelGames viewModelGamesPlayer = new ViewModelGames();
            viewModelGamesPlayer.SnakesPlayers = new Snakes()
            {
                Points = new List<Snakes.Point>()
        {
            new Snakes.Point() {X=30,Y=10 },
            new Snakes.Point() {X=20,Y=10 },
            new Snakes.Point() {X=10,Y=10 },
        },
                direction = Snakes.Direction.Start
            };
            viewModelGamesPlayer.Points = new Snakes.Point(new Random().Next(10, 783), new Random().Next(10, 410));
            viewModelGames.Add(viewModelGamesPlayer);

            int index = viewModelGames.Count - 1;
            viewModelGames[index].IdSnake = index;
            return index;
        }
        private static void Send()
        {
            foreach (var user in remoteIPAddress)
            {
                try
                {
                    var fullState = new FullGameState
                    {
                        PlayerId = user.IdSnake,
                        GameState = new GameStateData
                        {
                            Snakes = viewModelGames.Select(v => new SnakeData
                            {
                                Id = v.IdSnake,
                                Points = v.SnakesPlayers.Points,
                                Direction = v.SnakesPlayers.direction,
                                GameOver = v.SnakesPlayers.GameOver,
                                Color = remoteIPAddress.FirstOrDefault(u => u.IdSnake == v.IdSnake)?.Color ?? "Red"
                            }).ToList(),
                            Foods = viewModelGames.Select(v => v.Points).ToList(),
                            Leaders = Leaders.Take(10).ToList()
                        }
                    };

                    string json = JsonConvert.SerializeObject(fullState);
                    byte[] data = Encoding.UTF8.GetBytes(json);

                    using (var client = new UdpClient())
                    {
                        var ep = new IPEndPoint(IPAddress.Parse(user.IPAddress), int.Parse(user.Port));
                        client.Send(data, data.Length, ep);
                    }

                    Console.WriteLine($"Отправлено игроку {user.IPAddress}:{user.Port} (ID {user.IdSnake})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка отправки: " + ex.Message);
                }
            }
        }
        public static void Receiver()
        {
            UdpClient receivingUpdClient = new UdpClient(localPort);
            IPEndPoint RemoteIpEndPoint = null;
            try
            {
                Console.WriteLine("Команды сервера: ");
                while (true)
                {
                    byte[] receiveBytes = receivingUpdClient.Receive(ref RemoteIpEndPoint);
                    string returnData = Encoding.UTF8.GetString(receiveBytes);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Получил команду: " + returnData.ToString());
                    if (returnData.ToString().Contains("/start"))
                    {
                        string[] dataMessage = returnData.ToString().Split('|');
                        ViewModelUserSettings viewModelUserSettings = JsonConvert.DeserializeObject<ViewModelUserSettings>(dataMessage[1]);

                        if (string.IsNullOrEmpty(viewModelUserSettings.Color))
                            viewModelUserSettings.Color = GetRandomColor();

                        remoteIPAddress.Add(viewModelUserSettings);
                        viewModelUserSettings.IdSnake = AddSnake();
                        viewModelGames[viewModelUserSettings.IdSnake].IdSnake = viewModelUserSettings.IdSnake;
                        Send();

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Подключился игрок {viewModelUserSettings.Name} (ID {viewModelUserSettings.IdSnake})");
                    }
                    else
                    {
                        string[] dataMessage = returnData.ToString().Split('|');
                        ViewModelUserSettings viewModelUserSettings = JsonConvert.DeserializeObject<ViewModelUserSettings>(dataMessage[1]);
                        int IdPlayer = -1;
                        IdPlayer = remoteIPAddress.FindIndex(x => x.IPAddress == viewModelUserSettings.IPAddress && x.Port == viewModelUserSettings.Port);
                        if (IdPlayer != -1)
                        {
                            if (dataMessage[0] == "Up" && viewModelGames[IdPlayer].SnakesPlayers.direction != Snakes.Direction.Down)
                                viewModelGames[IdPlayer].SnakesPlayers.direction = Snakes.Direction.Up;
                            else if (dataMessage[0] == "Down" &&
                                viewModelGames[IdPlayer].SnakesPlayers.direction != Snakes.Direction.Up)
                                viewModelGames[IdPlayer].SnakesPlayers.direction = Snakes.Direction.Down;
                            else if (dataMessage[0] == "Left" &&
                                viewModelGames[IdPlayer].SnakesPlayers.direction != Snakes.Direction.Right)
                                viewModelGames[IdPlayer].SnakesPlayers.direction = Snakes.Direction.Left;
                            else if (dataMessage[0] == "Right" &&
                                viewModelGames[IdPlayer].SnakesPlayers.direction != Snakes.Direction.Left)
                                viewModelGames[IdPlayer].SnakesPlayers.direction = Snakes.Direction.Right;
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Возникло исключение: " + ex.ToString() + "\n "+ ex.Message);
            }
        }
        public static void Timer()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(100); // ~10 FPS

                    // Удаление мёртвых змей
                    var deadSnakes = viewModelGames.Where(x => x.SnakesPlayers.GameOver).ToList();
                    foreach (var dead in deadSnakes)
                    {
                        var user = remoteIPAddress.FirstOrDefault(u => u.IdSnake == dead.IdSnake);
                        if (user != null)
                        {
                            Console.WriteLine($"Отключён игрок: {user.IPAddress}:{user.Port}");
                            remoteIPAddress.Remove(user);
                        }
                        viewModelGames.Remove(dead);
                    }

                    foreach (var user in remoteIPAddress.ToList())
                    {
                        var snakeGame = viewModelGames.FirstOrDefault(g => g.IdSnake == user.IdSnake);
                        if (snakeGame == null) continue;

                        var snake = snakeGame.SnakesPlayers;
                        if (snake.GameOver) continue;

                        for (int i = snake.Points.Count - 1; i >= 0; i--)
                        {
                            if (i != 0)
                            {
                                snake.Points[i] = snake.Points[i - 1];
                            }
                            else
                            {
                                int speed = Math.Min(10 + (int)Math.Round(snake.Points.Count / 20f), MaxSpeed);
                                var currenthead = snake.Points[0];
                                switch (snake.direction)
                                {
                                    case Snakes.Direction.Right: currenthead.X += speed; break;
                                    case Snakes.Direction.Left: currenthead.X -= speed; break;
                                    case Snakes.Direction.Down: currenthead.Y += speed; break;
                                    case Snakes.Direction.Up: currenthead.Y -= speed; break;
                                    case Snakes.Direction.Start: break;
                                }
                                snake.Points[0] = currenthead;
                            }
                        }
                        var head = snake.Points[0];
                        if (head.X <= 0 || head.X >= 793 || head.Y <= 0 || head.Y >= 420)
                        {
                            snake.GameOver = true;
                        }
                        if (snake.direction != Snakes.Direction.Start)
                        {
                            for (int i = 1; i < snake.Points.Count; i++)
                            {
                                var p = snake.Points[i];
                                if (Math.Abs(head.X - p.X) <= 1 && Math.Abs(head.Y - p.Y) <= 1)
                                {
                                    snake.GameOver = true;
                                    break;
                                }
                            }
                        }
                        var food = snakeGame.Points;
                        if (!snake.GameOver && Math.Abs(head.X - food.X) <= 15 && Math.Abs(head.Y - food.Y) <= 15)
                        {
                            snakeGame.Points = new Snakes.Point(
                                new Random().Next(10, 783),
                                new Random().Next(10, 419)
                            );
                            snake.Points.Add(new Snakes.Point { X = snake.Points[1].X, Y = snake.Points[1].Y });
                            LoadLeaders();
                            Leaders.Add(new Leaders { Name = user.Name, Points = snake.Points.Count - 3 });
                            Leaders = Leaders.OrderByDescending(x => x.Points).ThenBy(x => x.Name).Take(100).ToList();
                            SaveLeaders();
                        }
                    }

                    Send(); 
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка в таймере: {ex.Message}");
                }
            }
        }
        public static void SaveLeaders()
        {
            string json = JsonConvert.SerializeObject(Leaders);
            StreamWriter SW = new StreamWriter("./leaders.txt");
            SW.WriteLine(json);
            SW.Close();
        }
        public static void LoadLeaders()
        {
            if (File.Exists("./leaders.txt"))
            {
                StreamReader SR = new StreamReader("./leaders.txt");
                string json = SR.ReadLine();
                SR.Close();
                if (!string.IsNullOrEmpty(json))
                    Leaders = JsonConvert.DeserializeObject<List<Leaders>>(json);
                else
                    Leaders = new List<Leaders>();
            }
        }
        private static string GetRandomColor()
        {
            var colors = new[] { "Red", "Green", "Blue", "Yellow", "Purple", "Orange", "Pink" };
            return colors[new Random().Next(colors.Length)];
        }
    }
}
