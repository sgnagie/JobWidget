# Job Widget

A small always-on-top, borderless desktop widget that shows a scrolling list of
job postings (title + location), auto-refreshing on a timer.
Currently pulls from Adzura, Jooble and Remotive.  

## 1. Install the .NET SDK

This project targets **.NET 10** (current LTS release as of mid-2026).

Download it here: https://dotnet.microsoft.com/download

After installing, confirm it worked by opening a terminal and running:
```
dotnet --version
```
You should see something starting with `10.`.

## 2. Get a free Adzuna API key

The widget pulls job data from Adzuna's free developer API.

1. Sign up at https://developer.adzuna.com/
2. Once registered, you'll get an **App ID** and **App Key**.
3. Open `Services/JobService.cs` and replace:
   ```csharp
   private const string AppId = "YOUR_APP_ID";
   private const string AppKey = "YOUR_APP_KEY";
   ```
   with your actual values.
4. Also check the `Country` constant in the same file — it's set to `"us"` by
   default (two-letter country code Adzuna uses in its URLs, e.g. `"gb"`, `"ca"`, `"au"`).

## 3. Set your search terms

Open `MainWindow.xaml.cs` and edit these two lines near the top of the class
to whatever job title and location you want to track:
```csharp
private const string SearchKeywords = "software engineer";
private const string SearchLocation = "Louisville";
```

## 4. Run it

From the project folder:
```
dotnet restore
dotnet run
```

The widget will appear in the top-right corner of your screen. You can:
- **Drag it** anywhere by clicking and holding on the widget.
- **Close it** with the ✕ button in the top-right corner.
- It **auto-scrolls** through postings and **refreshes** from the API every
  10 minutes (change `_refreshInterval` in `MainWindow.xaml.cs` if you want a
  different cadence).

## 5. (Optional) Run it without a terminal window

By default `dotnet run` is fine for testing. For a "real" standalone app you
can publish it as a self-contained .exe:
```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
The resulting `.exe` will be under `bin\Release\net10.0-windows\win-x64\publish\`.
You can then create a shortcut to it, or drop it in your Windows Startup folder
so it launches automatically when you log in
(`Win+R` → `shell:startup` → paste a shortcut there).

## Project structure
```
JobWidget/
├── JobWidget.csproj        # Project file (targets net10.0-windows, WPF enabled)
├── App.xaml / .cs          # Application entry point
├── MainWindow.xaml         # Widget UI (transparent, borderless, floating)
├── MainWindow.xaml.cs      # Fetch loop, auto-scroll, drag/close logic
├── Models/
│   └── JobPosting.cs       # Simple Title/Location/Company data class
└── Services/
    └── JobService.cs       # Calls the Adzuna API and parses the JSON response
```

## Notes / things you might want to tweak
- **Multiple searches**: right now it does one keyword + one location. If you
  want to track several roles/cities, you could call `FetchJobsAsync` multiple
  times and merge the results, or add a rotating list of search terms.
- **Styling**: colors/opacity/corner radius are all in `MainWindow.xaml` under
  the outer `Border` — easy to restyle.
- **Rate limits**: Adzuna's free tier has a request cap (check your dashboard),
  so avoid setting `_refreshInterval` too aggressively.
