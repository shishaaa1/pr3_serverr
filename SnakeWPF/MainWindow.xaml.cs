using Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace SnakeWPF
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public List<SnakeData> AllSnakes { get; set; } = new List<SnakeData>();
        public static MainWindow mainWindow;
        public Common.ViewModelUserSettings ViewModelUserSettings = new Common.ViewModelUserSettings();
        public Common.ViewModelGames ViewModelGames = null;
        public static IPAddress remoteIPAddress = IPAddress.Parse("127.0.0.1");
        public static int remotePort = 6000;
        public Thread tRec;
        public UdpClient receivingUdpClient;
        public Pages.Home Home = new Pages.Home();
        public Pages.Game Game = new Pages.Game();
        public void InitReceiverSocket()
        {
            if (receivingUdpClient == null)
            {
                receivingUdpClient = new UdpClient(0);
                var ep = (IPEndPoint)receivingUdpClient.Client.LocalEndPoint;
                ViewModelUserSettings.Port = ep.Port.ToString();

                Debug.WriteLine("UDP создан, порт=" + ep.Port);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            mainWindow = this;
            OpenPage(Home);
        }
        public void StartReceiver()
        {
            if (tRec?.IsAlive == true) return;

            tRec = new Thread(Receiver)
            {
                IsBackground = true,
                Name = "UDP Receiver Thread"
            };
            tRec.Start();
        }
        public void OpenPage(Page PageOpen)
        {
            DoubleAnimation startAnimation = new DoubleAnimation();
            startAnimation.From = 1;
            startAnimation.To = 0;
            startAnimation.Duration = TimeSpan.FromSeconds(0.6);
            startAnimation.Completed += delegate
            {
                frame.Navigate(PageOpen);
                DoubleAnimation endAnimation = new DoubleAnimation();
                endAnimation.From = 0;
                endAnimation.To = 1;
                endAnimation.Duration = TimeSpan.FromSeconds(0.6);
                frame.BeginAnimation(OpacityProperty, endAnimation);
            };
            frame.BeginAnimation(OpacityProperty, startAnimation);
        }

        public void Receiver()
        {
            try
            {
                if (receivingUdpClient == null)
                {
                    Debug.WriteLine("Receiver: receivingUdpClient == null -> выход");
                    return;
                }

                Debug.WriteLine($"Receiver стартовал на порту: {(receivingUdpClient.Client.LocalEndPoint as IPEndPoint)?.Port}");
                IPEndPoint remoteEP = null;

                // throttle: обновление UI не чаще, чем каждые 40 ms
                var lastUiUpdate = DateTime.MinValue;
                var uiUpdateInterval = TimeSpan.FromMilliseconds(40);

                while (true)
                {
                    try
                    {
                        byte[] data = receivingUdpClient.Receive(ref remoteEP); // блокирующий вызов
                        if (data == null || data.Length == 0)
                        {
                            Debug.WriteLine("Receive вернул пустой буфер");
                            continue;
                        }

                        string raw = Encoding.UTF8.GetString(data);
                        Debug.WriteLine($"[RECV] От {remoteEP}: {raw.Length} байт");

                        // Быстрая диагностика: если пришла строка-команда (например "/ok" или "/start|..."),
                        // то логируем и пропускаем, потому что нам нужен FullGameState JSON.
                        if (raw.StartsWith("/"))
                        {
                            Debug.WriteLine($"[RECV] Текстовая команда: {raw}");
                            // Некоторые сервера шлют команды вместо JSON — игнорируем здесь.
                            continue;
                        }

                        Common.FullGameState fullState = null;
                        try
                        {
                            fullState = JsonConvert.DeserializeObject<Common.FullGameState>(raw);
                        }
                        catch (Exception jsonEx)
                        {
                            Debug.WriteLine("Ошибка десериализации FullGameState: " + jsonEx.Message);
                            Debug.WriteLine("Содержимое пакета: " + raw);
                            continue;
                        }

                        if (fullState == null)
                        {
                            Debug.WriteLine("fullState == null после десериализации");
                            continue;
                        }

                        // Присваиваем Id клиента как можно раньше (до UI), если ещё не присвоен
                        if (ViewModelUserSettings.IdSnake == -1 && fullState.PlayerId >= 0)
                        {
                            ViewModelUserSettings.IdSnake = fullState.PlayerId;
                            Debug.WriteLine("Установлен ViewModelUserSettings.IdSnake = " + ViewModelUserSettings.IdSnake);
                        }

                        // Обновляем локальную модель ViewModelGames (чтобы EventKeyUp смог работать)
                        try
                        {
                            var mySnake = fullState.GameState?.Snakes?.FirstOrDefault(s => s.Id == ViewModelUserSettings.IdSnake);
                            if (mySnake != null)
                            {
                                if (ViewModelGames == null)
                                    ViewModelGames = new ViewModelGames();

                                ViewModelGames.IdSnake = ViewModelUserSettings.IdSnake;
                                ViewModelGames.SnakesPlayers.Points = mySnake.Points ?? new List<Snakes.Point>();
                                ViewModelGames.SnakesPlayers.direction = mySnake.Direction;
                                ViewModelGames.SnakesPlayers.GameOver = mySnake.GameOver;

                                if (fullState.GameState.Foods != null && fullState.GameState.Foods.Count > 0)
                                    ViewModelGames.Points = fullState.GameState.Foods[0];
                            }
                        }
                        catch (Exception vmEx)
                        {
                            Debug.WriteLine("Ошибка при обновлении ViewModelGames: " + vmEx.Message);
                        }

                        // throttle UI обновления
                        var now = DateTime.UtcNow;
                        if ((now - lastUiUpdate) >= uiUpdateInterval)
                        {
                            lastUiUpdate = now;

                            Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    // Навигация — делаем ПЕРЕД CreateUI, и делаем это только если мы на Home
                                    if (frame.Content is Pages.Home || frame.Content == null)
                                    {
                                        // Убедимся: Id задан до перехода, потому что Game.CreateUI использует Id из MainWindow
                                        Debug.WriteLine("Навигация: переход на Game");
                                        OpenPage(Game);
                                    }

                                    // Теперь отрисовка состояния (CreateUI использует fullState)
                                    Game.CreateUI(fullState);
                                }
                                catch (Exception uiEx)
                                {
                                    Debug.WriteLine("Ошибка в Dispatcher UI: " + uiEx);
                                }
                            });
                        }
                    }
                    catch (SocketException sockEx)
                    {
                        Debug.WriteLine("SocketException в Receiver: " + sockEx.Message);
                        break; // корректно выходим — сокет закрылся
                    }
                    catch (ObjectDisposedException)
                    {
                        Debug.WriteLine("UDP клиент был закрыт (ObjectDisposedException) — выходим из Receiver");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Ошибка в Receive loop: " + ex.Message);
                        // не разрушаем цикл при случайных ошибках
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Критическая ошибка в Receiver: " + ex);
            }
            finally
            {
                Debug.WriteLine("Receiver поток завершён.");
            }
        }

        public static void Send(string datagram)
        {
            UdpClient sender = new UdpClient();
            IPEndPoint endPoint = new IPEndPoint(remoteIPAddress, remotePort);
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(datagram);
                sender.Send(bytes, bytes.Length, endPoint);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Возникло исключение:" + ex.ToString() + "\n " + ex.Message);
            }
        }
        private void EventKeyUp(object sender, KeyEventArgs e)
      {
            if (!string.IsNullOrEmpty(ViewModelUserSettings.IPAddress) &&
                !string.IsNullOrEmpty(ViewModelUserSettings.Port) &&
                (ViewModelGames != null && !ViewModelGames.SnakesPlayers.GameOver))
            {
                if (e.Key == Key.Up)
                    Send($"Up|{JsonConvert.SerializeObject(ViewModelUserSettings)}");
                else if (e.Key == Key.Down)
                    Send($"Down|{JsonConvert.SerializeObject(ViewModelUserSettings)}");
                else if (e.Key == Key.Left)
                    Send($"Left|{JsonConvert.SerializeObject(ViewModelUserSettings)}");
                else if ((e.Key == Key.Right))
                    Send($"Right|{JsonConvert.SerializeObject(ViewModelUserSettings)}");
            }
        }
        private void QuitApplication(object sender, System.ComponentModel.CancelEventArgs e)
        {
            receivingUdpClient.Close();
            tRec.Abort();
        }
    }
}
