using Microsoft.Maui.Controls;
using System;
using tictactoe.Data;
using tictactoe.Models;
using System.Text.Json;

namespace tictactoe.Views
{
    public partial class HistoryPage : ContentPage
    {
        private readonly MatchRepository _repo;

        public HistoryPage(MatchRepository repo)
        {
            InitializeComponent();
            _repo = repo;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            var matches = await _repo.GetAllMatchesAsync();
            MatchesList.ItemsSource = matches;
        }

        private async void OnMatchSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is Match selectedMatch)
            {
                // Pass only what you need
                var lat = selectedMatch.Latitude;
                var lon = selectedMatch.Longitude;
                var result = Uri.EscapeDataString(selectedMatch.Result);
                var jsonMatch = JsonSerializer.Serialize(selectedMatch);

                await Shell.Current.GoToAsync($"{nameof(MatchDetailPage)}?match={Uri.EscapeDataString(jsonMatch)}");

                ((ListView)sender).SelectedItem = null;
            }
        }

    }
}
