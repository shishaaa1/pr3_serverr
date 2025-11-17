using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SnakeWPF.Pages
{
    /// <summary>
    /// Логика взаимодействия для Game.xaml
    /// </summary>
    public partial class Game : Page
    {
        public int StepCadr = 0;
        public List<SnakeData> AllSnakes { get; set; } = new List<SnakeData>();
        public Game()
        {
            InitializeComponent();
        }
        public void CreateUI(FullGameState state)
        {
            // Защита от пустых данных
            if (state?.GameState?.Snakes == null || state.GameState.Snakes.Count == 0)
            {
                Dispatcher.Invoke(() =>
                {
                    canvas.Children.Clear();
                    TextBlock tb = new TextBlock
                    {
                        Text = "Ожидание игроков...\nили сервер не шлёт данные",
                        Foreground = Brushes.Red,
                        FontSize = 24,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    canvas.Children.Add(tb);
                });
                return;
            }

            Dispatcher.Invoke(() =>
            {
                canvas.Children.Clear();

                int myId = MainWindow.mainWindow.ViewModelUserSettings.IdSnake;

                // === РИСУЕМ ВСЕХ ЗМЕЕК ===
                foreach (var snake in state.GameState.Snakes)
                {
                    if (snake.Points == null || snake.Points.Count == 0) continue;

                    bool isMine = snake.Id == myId;
                    bool isDead = snake.GameOver;

                    // Цвет тела
                    Color bodyColor = ParseColor(snake.Color);
                    Color headColor = isMine ? Colors.Orange : Colors.Gold;

                    if (isDead)
                    {
                        bodyColor = Colors.Gray;
                        headColor = Colors.DarkRed;
                    }

                    // Рисуем сегменты с хвоста к голове
                    for (int i = snake.Points.Count - 1; i >= 0; i--)
                    {
                        var p = snake.Points[i];

                        Ellipse segment = new Ellipse
                        {
                            Width = 18,
                            Height = 18,
                            Fill = new SolidColorBrush(i == 0 ? headColor : bodyColor),
                            Stroke = Brushes.Black,
                            StrokeThickness = 1,
                            Opacity = isDead ? 0.6 : 1.0,
                            Margin = new Thickness(p.X - 9, p.Y - 9, 0, 0)
                        };

                        // Пульсация только у своей живой головы
                        if (i == 0 && isMine && !isDead)
                        {
                            var scale = new ScaleTransform(1, 1);
                            segment.RenderTransform = scale;
                            segment.RenderTransformOrigin = new Point(0.5, 0.5);

                            var pulse = new DoubleAnimation(1.0, 1.3, TimeSpan.FromSeconds(0.4))
                            {
                                AutoReverse = true,
                                RepeatBehavior = RepeatBehavior.Forever
                            };
                            scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
                            scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
                        }

                        canvas.Children.Add(segment);
                    }
                }

                // === РИСУЕМ ЯБЛОКИ ===
                if (state.GameState.Foods != null)
                {
                    foreach (var food in state.GameState.Foods)
                    {
                        if (food == null) continue;

                        var appleBrush = new ImageBrush
                        {
                            ImageSource = new BitmapImage(new Uri("pack://application:,,,/Image/Apple.png"))
                        };

                        var apple = new Ellipse
                        {
                            Width = 36,
                            Height = 36,
                            Fill = appleBrush,
                            Margin = new Thickness(food.X - 18, food.Y - 18, 0, 0)
                        };

                        // Вращение яблока
                        var rotate = new RotateTransform(0);
                        apple.RenderTransform = rotate;
                        apple.RenderTransformOrigin = new Point(0.5, 0.5);

                        var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(8))
                        {
                            RepeatBehavior = RepeatBehavior.Forever
                        };
                        rotate.BeginAnimation(RotateTransform.AngleProperty, spin);

                        canvas.Children.Add(apple);
                    }
                }
            });
        }
        // Заменил switch-выражение на обычный switch (работает в C# 7.3)
        private Color ParseColor(string colorName)
        {
            if (string.IsNullOrEmpty(colorName)) return Colors.Gray;

            switch (colorName.ToLower())
            {
                case "red": return Colors.Red;
                case "green": return Colors.LimeGreen;
                case "blue": return Colors.DeepSkyBlue;
                case "yellow": return Colors.Yellow;
                case "purple": return Colors.MediumOrchid;
                case "orange": return Colors.Orange;
                case "pink": return Colors.HotPink;
                default: return Colors.Gray;
            }
        }
    }
}
