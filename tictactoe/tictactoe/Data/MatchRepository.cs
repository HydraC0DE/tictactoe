using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;
using tictactoe.Models;

namespace tictactoe.Data
{
    public class MatchRepository
    {
        private readonly SQLiteAsyncConnection _db;

        public MatchRepository()
        {
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "matches.db");
            _db = new SQLiteAsyncConnection(dbPath);
            _db.CreateTableAsync<Match>().Wait();
        }

        public Task<List<Match>> GetAllMatchesAsync() => _db.Table<Match>().ToListAsync();

        public Task AddMatchAsync(Match match) => _db.InsertAsync(match);

    }
}
