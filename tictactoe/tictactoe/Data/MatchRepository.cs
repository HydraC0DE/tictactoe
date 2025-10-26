using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;
using tictactoe.Models;

namespace tictactoe.Data
{
    //public class MatchRepository
    //{
    //    private readonly SQLiteAsyncConnection _db;

    //    public MatchRepository()
    //    {
    //        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "matches.db");
    //        _db = new SQLiteAsyncConnection(dbPath);
    //        _db.CreateTableAsync<Match>().Wait();
    //    }

    //    public Task<List<Match>> GetAllMatchesAsync() => _db.Table<Match>().ToListAsync();

    //    public Task AddMatchAsync(Match match) => _db.InsertAsync(match);

    //}

    public class MatchRepository
    {
        private readonly SQLiteAsyncConnection _db;

        public MatchRepository()
        {
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "matches.db");
            _db = new SQLiteAsyncConnection(dbPath);
            _db.CreateTableAsync<Match>().Wait();
        }

        public Task AddMatchAsync(Match match) => _db.InsertAsync(match);


        public Task<List<Match>> GetAllMatchesAsync() => _db.Table<Match>().ToListAsync();


        public Task<Match> GetMatchAsync(int id) =>
            _db.Table<Match>().FirstOrDefaultAsync(m => m.Id == id);


        public Task<int> UpdateMatchAsync(Match match) => _db.UpdateAsync(match);


        public Task<int> DeleteMatchAsync(Match match) => _db.DeleteAsync(match);

        public async Task<int> DeleteMatchByIdAsync(int id)
        {
            var match = await GetMatchAsync(id);
            return match != null ? await _db.DeleteAsync(match) : 0;
        }
        public Task<int> DeleteAllMatchesAsync() => _db.DeleteAllAsync<Match>();
    }
}
