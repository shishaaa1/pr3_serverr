using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class GameStateData
    {
        public List<SnakeData> Snakes { get; set; }
        public List<Snakes.Point> Foods { get; set; }
        public List<Leaders> Leaders { get; set; }
    }
}
