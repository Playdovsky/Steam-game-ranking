using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml;
using Game_Ranking;
using Newtonsoft.Json.Linq;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Wpf;

namespace Game_Ranking
{
    public partial class Window1 : Window
    {
        private Game selectedGame;

        public Window1()
        {
            InitializeComponent();
        }

        private async Task InitializeAsync()
        {
            await UpdateGameThumbnailAsync();
        }

        /// <summary>
        /// PopulateGameData to metoda wywoływana przez MainWindow z przekazanymi parametrami
        /// o wybranej grze, nastepnie w metodzie zostają te dane wypisane w labelach a na koniec
        /// wywoływane są następne dwie metody
        /// </summary>
        public void PopulateGameData(Game selectedGame)
        {
            this.selectedGame = selectedGame;
            LabelGameHeader.Content = selectedGame.Title;
            LabelAppId.Content = selectedGame.AppID;
            LabelAllTimePeak.Content = selectedGame.AllTimePeak;
            LabelDailyPeak.Content = selectedGame.Last24hPeak;
            LabelPeakMonth.Content = selectedGame.Last30Peak;
            LabelActualPlayerCount.Content = selectedGame.ActualPeak;
            InitializeAsync();
            DrawChart();
        }

        /// <summary>
        /// Metoda umożliwiająca po kliknięciu w tytuł gry przejście do strony steam wybranej gry
        /// </summary>
        private void LabelGameHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Do you want to open the link?", "Open Link", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var uri = new Uri(selectedGame.Link);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error has occured while opening the link: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Pobranie z API miniaturki wybranej gry
        /// </summary>
        public async Task<string> GetGameThumbnailAsync()
        {
            string appId = selectedGame.AppID;
            string apiKey = "";
            string apiUrl = $"https://store.steampowered.com/api/appdetails?appids={appId}";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string apiResponse = await response.Content.ReadAsStringAsync();
                        JObject jsonResponse = JObject.Parse(apiResponse);

                        //null-conditional operator "?." do bezpiecznego dostępu do właściwości
                        string thumbnailLink = jsonResponse?[appId]?["data"]?["header_image"]?.ToString();

                        return thumbnailLink;
                    }
                    else
                    {
                        MessageBox.Show($"Error: {response.ReasonPhrase}");
                    }
                }
                catch (HttpRequestException ex)
                {
                    MessageBox.Show($"HTTP request error: {ex.Message}");
                }
            }
            return null;
        }

        /// <summary>
        /// wstawienie miniaturki
        /// </summary>
        public async Task UpdateGameThumbnailAsync()
        {
            string thumbnailLink = await GetGameThumbnailAsync();
            LabelMiniatura.Source = new BitmapImage(new Uri(thumbnailLink));
        }

        /// <summary>
        /// DrawChart() to metoda odpowiadająca za przygotowanie okna do wstawienia wykresu,
        /// porządkuje ona czynności związane z dostępem do pliku, z ilościami graczy, z ładowaniem pliku
        /// oraz na końcu z rysowaniem wykresu na podstawie uzyskanych w tym procesie danych
        /// </summary>
        public void DrawChart()
        {
            string selectedAppID = selectedGame.AppID;
            string fileName = "../../Data/GamesPlayerCountData.txt";

            double[] gameData = LoadDataFromFile(fileName, selectedAppID);
            DrawScatterPlot(gameData);
        }

        /// <summary>
        /// Poniższa metoda zajmuje się przygotowaniem pliku a następnie przeszukaniem go w celu
        /// znalezienia odpowiedniego appID wybranej przez użytkownika gry dla której w następnym kroku będą pobierane
        /// ilości graczy w pewnym badanym okresie czasowym
        /// </summary>
        private double[] LoadDataFromFile(string fileName, string selectedAppID)
        {
            try
            {
                List<double> appData = new List<double>();

                //odczyt pliku za pomocą StreamReader
                using (StreamReader reader = new StreamReader(fileName))
                {
                    string line;
                    //pętla sprawdzająca każdą linijkę, która jest not-null
                    while ((line = reader.ReadLine()) != null)
                    {
                        //dzielenie wartości, znakiem oddzielającym jest spacja
                        string[] parts = line.Split(' ');
                        //jeśli linijka zawiera minimum dwie wartości (ponieważ 1 wartość to appID) to rozpoczyna się dzielenie danych
                        if (parts.Length >= 2)
                        {
                            string appID = parts[0].Trim();

                            //po wyborze odpowiedniego appID zgadzającego się z wybraną grą przez użytkownika wydobywane są dane a pętla zostaje przerwana
                            if (appID == selectedAppID)
                            {
                                for (int i = 1; i < parts.Length; i++)
                                {
                                    if (double.TryParse(parts[i], out double parsedValue))
                                    {
                                        appData.Add(parsedValue);
                                    }
                                }

                                break;
                            }
                        }
                    }
                }

                return appData.ToArray();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error has occured while loading data from file: {ex.Message}");
                return new double[0];
            }
        }

        /// <summary>
        /// Rysowanie wykresu na podstawie uzyskanych danych z powyższych metod
        /// </summary>
        private void DrawScatterPlot(double[] data)
        {
            string titleHexColor = "#64ADFE";
            OxyColor titleColor = OxyColor.Parse(titleHexColor);
            OxyColor axisColor = OxyColors.White;
            OxyColor borderColor = OxyColors.White;
            string lineHexColor = "#A3CF06";
            OxyColor lineColor = OxyColor.Parse(lineHexColor);

            var plotModel = new PlotModel { Title = "Players per Execution", TitleFontWeight = OxyPlot.FontWeights.Bold, TitleColor = titleColor };

            var lineSeries = new LineSeries
            {
                Color = lineColor,
                StrokeThickness = 3
            };

            for (int i = 0; i < data.Length; i++)
            {
                lineSeries.Points.Add(new DataPoint(i + 1, data[i]));
            }
            plotModel.Series.Add(lineSeries);

            for (int i = 0; i < data.Length; i++)
            {
                var scatterSeries = new LineSeries
                {
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 4,
                    MarkerFill = OxyColors.White
                };
                scatterSeries.Points.Add(new DataPoint(i + 1, data[i]));
                plotModel.Series.Add(scatterSeries);
            }

            var bottomAxis = new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                MinimumPadding = 0.1,
                MaximumPadding = 0.1,
                TextColor = axisColor,
                AxislineColor = axisColor,
                Title = "Execution count",
                TitleColor = OxyColors.White,
                TitleFontWeight = OxyPlot.FontWeights.Bold
            };

            var leftAxis = new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Left,
                MinimumPadding = 0.1,
                MaximumPadding = 0.1,
                TextColor = axisColor,
                AxislineColor = axisColor,
                StringFormat = "0",
                Title = "Players",
                TitleColor = OxyColors.White,
                TitleFontWeight = OxyPlot.FontWeights.Bold,
                AxisTitleDistance = 15
            };

            bottomAxis.TicklineColor = axisColor;
            leftAxis.TicklineColor = axisColor;
            bottomAxis.AxislineColor = axisColor;
            leftAxis.AxislineColor = axisColor;

            plotModel.Axes.Add(bottomAxis);
            plotModel.Axes.Add(leftAxis);

            plotModel.PlotAreaBorderColor = borderColor;

            plotView.Model = plotModel;
        }

    }
}