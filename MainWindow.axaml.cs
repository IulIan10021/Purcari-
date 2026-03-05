using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PurcariDesktop.Data;
using PurcariDesktop.Models;
using PurcariDesktop.Reports.Services;
namespace PurcariDesktop;


public partial class MainWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public ObservableCollection<Wine> Wines { get; set; } = new();

    // ===== SUGGESTIONS =====
    public ObservableCollection<Wine> SearchSuggestions { get; set; } = new();

    private bool _hasSuggestions;
    public bool HasSuggestions
    {
        get => _hasSuggestions;
        set { _hasSuggestions = value; Notify(); }
    }

    private Wine? _selectedSuggestion;
    public Wine? SelectedSuggestion
    {
        get => _selectedSuggestion;
        set
        {
            _selectedSuggestion = value;
            Notify();

            if (value != null)
            {
                
                SearchText = value.Name;

               
                HideSuggestions();

               
                SelectWineInGrid(value.WineId);
            }
        }
    }

    private Wine? _selectedWine;
    public Wine? SelectedWine
    {
        get => _selectedWine;
        set
        {
            _selectedWine = value;
            Notify();

            if (value != null)
            {
                NameInput = value.Name;
                TypeInput = value.Type;
                PriceInput = value.Price.ToString(CultureInfo.InvariantCulture);
                YearInput = value.Year.ToString();
                StatusMessage = "✔ Vin selectat. Poți Update/Delete.";
            }
        }
    }

    private string _nameInput = "";
    public string NameInput { get => _nameInput; set { _nameInput = value; Notify(); } }

    private string _typeInput = "";
    public string TypeInput { get => _typeInput; set { _typeInput = value; Notify(); } }

    private string _priceInput = "";
    public string PriceInput { get => _priceInput; set { _priceInput = value; Notify(); } }

    private string _yearInput = "";
    public string YearInput { get => _yearInput; set { _yearInput = value; Notify(); } }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            Notify();
            UpdateSuggestions(); //arata fara sa apaesi btn apply
        }
    }

    private int _resultsCount;
    public int ResultsCount { get => _resultsCount; set { _resultsCount = value; Notify(); } }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value; Notify(); } }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadWines();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    // ================= SELECT =================
    private void LoadWines()
    {
        using  var db = new AppDbContext();
        var list = db.Wines.OrderBy(w => w.WineId).ToList();

        Wines.Clear();
        foreach (var w in list) Wines.Add(w);

        ResultsCount = Wines.Count;
    }

    public void RefreshCommand(object? sender, RoutedEventArgs e)
    {
        StatusMessage = "";
        LoadWines();
        HideSuggestions();
    }

    // ================= INSERT =================
    public void AddCommand(object? sender, RoutedEventArgs e)
    {
        try
        {
            var name = NameInput.Trim();
            var type = TypeInput.Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
            {
                StatusMessage = "❗ Completează Name și Type.";
                return;
            }

            if (!decimal.TryParse(PriceInput.Replace(',', '.'),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            {
                StatusMessage = "❗ Price invalid (ex: 100 sau 100.50).";
                return;
            }

            int.TryParse(YearInput, out var year);

            using var db = new AppDbContext();
            db.Wines.Add(new Wine
            {
                Name = name,
                Type = type,
                Price = price,
                Year = year
            });

            db.SaveChanges();

            StatusMessage = "✔ Vin adăugat!";
            SearchText = "";
            LoadWines();
            ClearInputs();
            HideSuggestions();
        }
        catch (Exception ex)
        {
            StatusMessage = "✖ Eroare Add: " + ex.Message;
        }
    }

    // ================= UPDATE =================
    public void UpdateCommand(object? sender, RoutedEventArgs e)
    {
        if (SelectedWine == null)
        {
            StatusMessage = "❗ Selectează un vin din tabel pentru Update.";
            return;
        }

        try
        {
            if (!decimal.TryParse(PriceInput.Replace(',', '.'),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            {
                StatusMessage = "❗ Price invalid.";
                return;
            }

            int.TryParse(YearInput, out var year);

            using var db = new AppDbContext();
            var wine = db.Wines.FirstOrDefault(x => x.WineId == SelectedWine.WineId);

            if (wine == null)
            {
                StatusMessage = "❗ Vinul nu mai există în DB.";
                return;
            }

            wine.Name = NameInput.Trim();
            wine.Type = TypeInput.Trim();
            wine.Price = price;
            wine.Year = year;

            db.SaveChanges();

            StatusMessage = "✔ Vin actualizat!";
            SearchText = "";
            LoadWines();
            ClearInputs();
            HideSuggestions();
        }
        catch (Exception ex)
        {
            StatusMessage = "✖ Eroare Update: " + ex.Message;
        }
    }

    // ================= DELETE =================
    public void DeleteCommand(object? sender, RoutedEventArgs e)
    {
        if (SelectedWine == null)
        {
            StatusMessage = "❗ Selectează un vin din tabel pentru Delete.";
            return;
        }

        try
        {
            using var db = new AppDbContext();
            var wine = db.Wines.FirstOrDefault(x => x.WineId == SelectedWine.WineId);

            if (wine == null)
            {
                StatusMessage = "❗ Vinul nu mai există în DB.";
                return;
            }

            db.Wines.Remove(wine);
            db.SaveChanges();

            StatusMessage = "🗑 Vin șters!";
            SearchText = "";
            LoadWines();
            ClearInputs();
            HideSuggestions();
        }
        catch (Exception ex)
        {
            StatusMessage = "✖ Eroare Delete: " + ex.Message;
        }
    }

    // ================= APPLY SEARCH (filter grid) =================
    public void SearchCommand(object? sender, RoutedEventArgs e)
    {
        try
        {
            var q = (SearchText ?? "").Trim().ToLowerInvariant();

            using var db = new AppDbContext();
            var list = string.IsNullOrWhiteSpace(q)
                ? db.Wines.OrderBy(w => w.WineId).ToList()
                : db.Wines
                    .Where(w => w.Name.ToLower().Contains(q) || w.Type.ToLower().Contains(q))
                    .OrderBy(w => w.WineId)
                    .ToList();

            Wines.Clear();
            foreach (var w in list) Wines.Add(w);

            ResultsCount = Wines.Count;

            if (Wines.Count > 0)
                SelectedWine = Wines.First();
            else
            {
                SelectedWine = null;
                ClearInputs();
            }

            HideSuggestions();
        }
        catch (Exception ex)
        {
            StatusMessage = "✖ Eroare Search: " + ex.Message;
        }
    }

    public void ClearSearchCommand(object? sender, RoutedEventArgs e)
    {
        SearchText = "";
        StatusMessage = "";
        LoadWines();
        ClearInputs();
        HideSuggestions();
    }

    // ================= SUGGESTIONS (LIVE) =================
    private void UpdateSuggestions()
    {
        var q = (SearchText ?? "").Trim();

        if (q.Length < 2)
        {
            HideSuggestions();
            return;
        }

        using var db = new AppDbContext();
        var qLower = q.ToLowerInvariant();

        var list = db.Wines
            .Where(w => w.Name.ToLower().Contains(qLower) || w.Type.ToLower().Contains(qLower))
            .OrderBy(w => w.Name)
            .Take(8)
            .ToList();

        SearchSuggestions.Clear();
        foreach (var w in list) SearchSuggestions.Add(w);

        ResultsCount = list.Count;
        HasSuggestions = SearchSuggestions.Count > 0;
    }

    private void HideSuggestions()
    {
        SearchSuggestions.Clear();
        HasSuggestions = false;
        _selectedSuggestion = null;
        Notify(nameof(SelectedSuggestion));
    }

    private void SelectWineInGrid(int wineId)
    {
        // reîncarcă lista completă și selectează vinul ales
        LoadWines();

        var match = Wines.FirstOrDefault(w => w.WineId == wineId);
        if (match != null)
            SelectedWine = match;
    }

    private void ClearInputs()
    {
        NameInput = "";
        TypeInput = "";
        PriceInput = "";
        YearInput = "";
    }
    public void GenerateReportCommand(object? sender, RoutedEventArgs e)
    {
        try
        {
            var data = new ReportDataService().GetWines();
            if (data.Count == 0)
            {
                StatusMessage = "Nu există vinuri în DB pentru raport.";
                return;
            }

            var outputDir = System.IO.Path.Combine(AppContext.BaseDirectory, "PublishedReports");

            var report = new WineCatalogReportService();
            var (pdf, xlsx, png) = report.GenerateAll(outputDir, data);

            StatusMessage =
                $"✔ Raport generat cu succes!\n\nPDF: {pdf}\nExcel: {xlsx}\nGrafic PNG: {png}";
        }
        catch (Exception ex)
        {
            StatusMessage = "✖ Eroare raport: " + ex.Message;
        }
    }
    
}

