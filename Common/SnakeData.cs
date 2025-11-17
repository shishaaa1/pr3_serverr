using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class SnakeData
    {
        public int Id { get; set; }
        public List<Snakes.Point> Points { get; set; }
        public Snakes.Direction Direction { get; set; }
        public bool GameOver { get; set; }
        public string Color { get; set; }
    }
}
