using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace tictactoe.Models
{
    public class Match
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string GameJson { get; set; }

        public DateTime Date { get; set; }    // simple date string

        public string? ImagePath { get; set; }
        public double Latitude { get; set; } 
        public double Longitude { get; set; }

        public string Result { get; set; }


        [Ignore]
        public Game CurrentGame
        {
            get => string.IsNullOrEmpty(GameJson)
                ? new Game()
                : JsonSerializer.Deserialize<Game>(GameJson);
            set => GameJson = JsonSerializer.Serialize(value);
        }
    }
}
