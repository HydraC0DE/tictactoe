using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tictactoe.Models
{
    public class Match
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }


        //public string BoardImagePath { get; set; }
        //public string MoveSuggestion { get; set; }
        //public DateTime Date { get; set; }
        //public bool UserWon { get; set; }

        public string Result { get; set; }  // e.g., "X won", "O won", or "Draw"
        public string Date { get; set; }    // simple date string

        public string? ImagePath { get; set; }
        public double Latitude { get; set; } 
        public double Longitude { get; set; }
    }
}
