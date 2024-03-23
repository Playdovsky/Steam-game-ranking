using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game_Ranking
{
    public class Game
    {
        public string AppID { get; set; }
        public string Title { get; set; }
        public int AllTimePeak { get; set; }
        public int Last30Peak {  get; set; }
        public int Last24hPeak { get; set; }
        public int ActualPeak {  get; set; }
        public double PlayerChange { get; set; }
        public string Link { get; set; }
    
        public Game(string appID, string title, int allTimePeak, int last30Peak, int last24hPeak, int actualPeak, double playerChange, string link)
        {
            AppID = appID;
            Title = title;
            AllTimePeak = allTimePeak;
            Last30Peak = last30Peak;
            Last24hPeak = last24hPeak;
            ActualPeak = actualPeak;
            PlayerChange = playerChange;
            Link = link;
        }
    }
}
