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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Net.Http;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.IO;
using System.Diagnostics.PerformanceData;

namespace Game_Ranking
{
    public partial class MainWindow : Window
    {
        List<Game> games = new List<Game>();

        public MainWindow()
        {
            InitializeComponent();
            ReadXML();
        }

        /// <summary>
        /// ReadXML() odczytuje i przygotowuje podstawowe dane o grach
        /// do wykorzystania ich w dalszych etapach korzystania z programu.
        /// Metoda uzupełnia również listę gier danymi wykorzystując konstruktor
        /// klasy Game.cs
        /// </summary>
        public void ReadXML()
        {
            try
            {
                //odczyt danych z .xml
                XmlDocument xmlFile = new XmlDocument();
                xmlFile.Load("../../Data/GamesData.xml");
                XmlNodeList nodes = xmlFile.SelectNodes("/games/game");

                foreach (XmlNode node in nodes)
                {
                    //wykorzystanie konstruktora i za jego pośrednictwem przepisanie danych z .xml do listy
                    string appID = node.SelectSingleNode("appID")?.InnerText;
                    string title = node.SelectSingleNode("title")?.InnerText;
                    int allTimePeak = int.Parse(node.SelectSingleNode("allTimePeak")?.InnerText);
                    int last30Peak = int.Parse(node.SelectSingleNode("last30Peak")?.InnerText);
                    int last24hPeak = int.Parse(node.SelectSingleNode("last24hPeak")?.InnerText);
                    string link = node.SelectSingleNode("link")?.InnerText;

                    //actualPeak jako jedyna zmienna automatycznie otrzymuje wartość 0 gdyż jej wartość w przyszłości
                    //zostanie nadpisana przez liczbę pobraną z API
                    int actualPeak = 0;
                    double playerChange = 0;

                    Game game = new Game(appID, title, allTimePeak, last30Peak, last24hPeak, actualPeak, playerChange, link);
                    games.Add(game);
                }
               Counter(xmlFile);
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Error has occured while loading XML file: {ex.Message}");
            }
        }

        /// <summary>
        /// Counter() to metoda odpowiedzialna za zliczanie ilości włączeń programu,
        /// licznik wykorzystywany jest przy zapisywaniu nowych danych do pliku (co 5 uruchomienie)
        /// </summary>
        public void Counter(XmlDocument xmlFile)
        {
            bool counterSuccess = false;
            try
            {
                int count = int.Parse(File.ReadAllText("../../Data/counter.txt"));
                count++;
                if (count % 5 == 0)
                {
                   counterSuccess = true;
                   CallSteamAPIPlayerCount(xmlFile, counterSuccess);

                }
                else
                {
                    counterSuccess = false;
                    CallSteamAPIPlayerCount(xmlFile, counterSuccess);

                }
                using (StreamWriter writer = new StreamWriter("../../Data/counter.txt", false))
                {
                    writer.Write(count);
                }

            }
            catch (System.IO.FileNotFoundException ex)
            {
                MessageBox.Show($"Run count error: {ex.Message}");
            }
        }

        /// <summary>
        /// CallSteamAPI() odpowiada za połączenie ze steam API oraz za pobranie danych.
        /// Jako tymczasowe zabezpieczenie aby korzystać z metody należy wprowadzić swój własny klucz steam API.
        /// Klucz zdobyć można na stronie: https://steamcommunity.com/dev/apikey
        /// </summary>
        public async void CallSteamAPIPlayerCount(XmlDocument xmlFile, bool counterSuccess)
        {
            string apiKey = ""; //unikalny prywatny klucz API użytkownika (działa też bez klucza)
            string apiUrlBase = "https://api.steampowered.com/ISteamUserStats/GetNumberOfCurrentPlayers/v1/?appid=";

            //nawiązywanie połączenia z API
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("SteamWebAPIKey", apiKey);

                foreach (Game game in games)
                {
                    string appID = game.AppID;
                    string apiUrl = apiUrlBase + appID;

                    try
                    {
                        HttpResponseMessage response = await client.GetAsync(apiUrl);

                        if (response.IsSuccessStatusCode)
                        {
                            string apiResponse = await response.Content.ReadAsStringAsync();
                            JObject jsonResponse = JObject.Parse(apiResponse);

                            int playerCount = 0;

                            if (jsonResponse["response"]?["player_count"] != null)
                            {
                                playerCount = (int)jsonResponse["response"]["player_count"];
                                game.ActualPeak = playerCount;

                                xmlFile.Load("../../Data/GamesData.xml");
                                XmlNodeList nodes = xmlFile.SelectNodes("/games/game");

                                foreach (XmlNode node in nodes)
                                {
                                    string currentAppID = node.SelectSingleNode("appID")?.InnerText;

                                    if (currentAppID == appID)
                                    {
                                        XmlNode last24hPeakNode = node.SelectSingleNode("last24hPeak");

                                        //jeśli aktualny playerCount jest większy od ostatniego największego, to ostatni największy staje się aktualnym
                                        if (playerCount > game.Last24hPeak)
                                        {
                                            game.Last24hPeak = playerCount;
                                            last24hPeakNode.InnerText = playerCount.ToString();
                                            xmlFile.Save("../../Data/GamesData.xml");
                                        }

                                        //obliczanie zmian procentowych dla gier między ostatnią wpisaną wartością w plik a aktualnie występującą
                                        try
                                        {
                                            List<double> appData = new List<double>();

                                            using (StreamReader reader = new StreamReader("../../Data/GamesPlayerCountData.txt"))
                                            {
                                                string line;

                                                while ((line = reader.ReadLine()) != null)
                                                {
                                                    string[] parts = line.Split(' ');

                                                    if (parts.Length >= 2)
                                                    {
                                                        string appIDFile = parts[0].Trim();

                                                        if (appIDFile == appID)
                                                        {
                                                            for (int i = 1; i < parts.Length; i++)
                                                            {
                                                                if (double.TryParse(parts[i], out double parsedValue))
                                                                {
                                                                    appData.Add(parsedValue);
                                                                    double lastValue = double.Parse(parts[parts.Length - 1]);
                                                                    double previousValue = double.Parse(parts[parts.Length - 2]);

                                                                    game.PlayerChange = (game.ActualPeak - lastValue) * 100.0 / lastValue;
                                                                    game.PlayerChange = Math.Round(game.PlayerChange, 2);
                                                                }
                                                            }
                                                            break;
                                                        }
                                                    }
                                                }
                                            }

                                            //jeśli aktualne uruchomienie programu jest podzielne przez 5 zapisuje aktualną liczbę graczy do pliku
                                            if (counterSuccess)
                                            {
                                                SavePlayerCountToFile("../../Data/GamesPlayerCountData.txt", appID, playerCount);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show($"Error while loading data from file: {ex.Message}");
                                        }
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                MessageBox.Show("Error: cannot find player_count property");
                            }
                        }
                        else
                        {
                            MessageBox.Show("Error: cannot get data from this appID " + appID);
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        MessageBox.Show($"HTTP request error: {ex.Message}");
                    }
                }
            }

            DataGridGames.ItemsSource = games;
            DataGridSort();
        }

        /// <summary>
        /// DataGridSort() wykorzystuje lambdę wyszukującą i na jej podstawie
        /// sortuje dane w kolumnie 'Actual player count' od najwyższej do najniższej.
        /// Zanim sortowanie się wykona najpierw metoda oczekuje bezpieczne 2 sekundy
        /// w celu upewnienia się, że wszystkie dane zostały poprawnie wczytane
        /// UWAGA: Lambda wyszukuje po nazwie kolumny, zmiana jednej litery spowoduje błąd!
        /// </summary>
        private async void DataGridSort()
        {
            await Task.Delay(1000);
            var column = DataGridGames.Columns.SingleOrDefault(c => c.Header.ToString() == "Actual player count");
            DataGridGames.Items.SortDescriptions.Clear();
            DataGridGames.Items.SortDescriptions.Add(new SortDescription(column.SortMemberPath, ListSortDirection.Descending));
        }

        /// <summary>
        /// SavePlayerCountToFile zajmuje się zapisaniem nowej liczby graczy do pliku tak aby
        /// później liczba ta została wykorzystana na wykresie.
        /// Metoda wywoływana jest co 5 uruchomienie programu, liczenie wykonuje Counter()
        /// </summary>
        private static void SavePlayerCountToFile(string filePath, string gameId, int playerCount)
        {
            string[] lines = File.ReadAllLines(filePath);
            
            for (int i = 0; i < lines.Length; i++)
            {
                string[] values = lines[i].Split(' ');

                if (values.Length > 0 && values[0] == gameId)
                {
                    lines[i] += $" {playerCount}";
                    break;
                }
            }
            File.WriteAllLines(filePath, lines);
        }

        /// <summary>
        /// Poniższa metoda to filtr w czasie rzeczywistym.
        /// Lambda wyszukująca porównuje dane wpisane w TextBox z danymi z listy
        /// wyświetlając tylko te dane, które pasują i robi to dynamicznie
        /// </summary>
        private void TextBoxFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = TextBoxFilter.Text.ToLower();
            var filteredGames = games.Where(x => x.Title.ToLower().Contains(filter)).ToList();
            DataGridGames.ItemsSource = filteredGames;
        }
    
        /// <summary>
        /// Metoda otwierająca nowe okno ze szczegółowymi danymi po dwukrotnym kliknięciu rekordu grida.
        /// Dane o wybranej grze są wydobywane i przekazywane do nowego okna
        /// a następnie wykorzystywane są do wyświetlenia w GameDetails.xaml,
        /// </summary>
        private void DataGridGames_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                if (dataGrid.SelectedItems.Count > 0)
                {
                    //wydobycie danych z wybranego rekordu grida
                    Game selectedGame = (Game)dataGrid.SelectedItem;

                    //przypisanie tychże wartości odpowiednim zmiennym
                    string appId = selectedGame.AppID;
                    string title = selectedGame.Title;
                    int allTimePeak = selectedGame.AllTimePeak;
                    int last30Peak = selectedGame.Last30Peak;
                    int last24hPeak = selectedGame.Last24hPeak;
                    int actualPeak = selectedGame.ActualPeak;

                    //tworzenie okna GameDetails i wywołanie metody tego okna przekazując parametry
                    Window1 gameDetailsWindow = new Window1();
                    gameDetailsWindow.PopulateGameData(selectedGame);
                    gameDetailsWindow.Show();
                }
            }
        }
    }
}
