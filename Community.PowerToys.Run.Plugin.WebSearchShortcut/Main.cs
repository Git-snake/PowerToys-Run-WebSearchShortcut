﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Input;
using Community.PowerToys.Run.Plugin.WebSearchShortcut.Models;
using Community.PowerToys.Run.Plugin.WebSearchShortcut.Properties;
using Community.PowerToys.Run.Plugin.WebSearchShortcut.Suggestion;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Wox.Plugin.Logger;
using BrowserInfo = Wox.Plugin.Common.DefaultBrowserInfo;

namespace Community.PowerToys.Run.Plugin.WebSearchShortcut
{
  /// <summary>
  /// Main class of this plugin that implement all used interfaces.
  /// </summary>
  public class Main : IPlugin, IDelayedExecutionPlugin, IContextMenu, ISettingProvider, IDisposable, IPluginI18n
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="Main"/> class.
    /// </summary>
    public Main()
    {
      Storage = new PluginJsonStorage<WebSearchShortcutSettings>();
      Settings = Storage.Load();
      Settings.StorageDirectoryPath = Storage.DirectoryPath;
      WebSearchShortcutStorage = new WebSearchShortcutStorage(Settings);
    }

    internal Main(WebSearchShortcutSettings settings, IWebSearchShortcutStorage webSearchShortcutStorage)
    {
      Storage = new PluginJsonStorage<WebSearchShortcutSettings>();
      Settings = settings;
      WebSearchShortcutStorage = webSearchShortcutStorage;
    }
    /// <summary>
    /// ID of the plugin.
    /// </summary>
    public static string PluginID => "B5E595872B8068104D5AD6BBE39A6664";

    public static string PluginName => Resources.plugin_name;

    /// <summary>
    /// Name of the plugin.
    /// </summary>
    public string Name => Resources.plugin_name;

    /// <summary>
    /// Description of the plugin.
    /// </summary>
    public string Description => Resources.plugin_description;

    /// <summary>
    /// Additional options for the plugin.
    /// </summary>
    public IEnumerable<PluginAdditionalOption> AdditionalOptions => Settings.GetAdditionalOptions();

    private PluginInitContext? Context { get; set; }

    public static string PluginDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;

    public static Dictionary<string, string> IconPath { get; set; } = new()
        {
            { "Search", @"Images\Search.light.png" },
            { "Config", @"Images\Config.light.png" },
            { "Reload", @"Images\Reload.light.png" },
            { "Suggestion", @"Images\Suggestion.light.png" },
            { "Warn", @"Images\Warn.light.png" }
        };

    private bool Disposed { get; set; }

    private PluginJsonStorage<WebSearchShortcutSettings> Storage { get; }

    private WebSearchShortcutSettings Settings { get; }

    private IWebSearchShortcutStorage WebSearchShortcutStorage { get; }

    private Suggestions suggestion = new();

    /// <summary>
    /// Return a filtered list, based on the given query.
    /// </summary>
    /// <param name="query">The query to filter the list.</param>
    /// <returns>A filtered list, can be empty when nothing was found.</returns>
    public List<Result> Query(Query query)
    {
      if (query?.Search is null)
      {
        return [];
      }

      var args = query.Search;

      if (args.Trim().Equals("!reload", StringComparison.OrdinalIgnoreCase))
      {
        return
        [
          new()
          {
            QueryTextDisplay = args,
            Title = Resources.reload_title,
            SubTitle = Resources.reload_sub_title,
            IcoPath = IconPath["Reload"],
            Action = _ =>
            {
              ReloadData();
              return true;
            },
          },
        ];
      }

      if (args.Trim().Equals("!config", StringComparison.OrdinalIgnoreCase))
      {
        return
        [
          new()
          {
            QueryTextDisplay = args,
            Title = Resources.config_title,
            SubTitle = Resources.config_sub_title,
            IcoPath = IconPath["Config"],
            Action = _ =>
            {
              if (!Helper.OpenInShell(WebSearchShortcutStorage.GetPath()))
              {
                Log.Error($"Plugin: {PluginName}\nCannot open {WebSearchShortcutStorage.GetPath()}", typeof(Main));
                return false;
              }
              return true;
            },
            ContextData = WebSearchShortcutStorage.GetPath(),
          }
        ];
      }


      if (!string.IsNullOrEmpty(WebSearchShortcutStorage.LoadError))
      {
        return
        [
          new()
          {
            Title = Resources.error_title,
            SubTitle = $"Error: {WebSearchShortcutStorage.LoadError}",
            IcoPath = IconPath["Warn"],
            ToolTipData = new ToolTipData("Config error", $"{WebSearchShortcutStorage.LoadError}")
          }
        ];
      }

      List<Result> results = [];

      if (string.IsNullOrEmpty(args))
      {
        results.AddRange(WebSearchShortcutStorage.GetRecords().Select(x => GetResultForSelectOrOpen(x, args, query)));
        return results;
      }

      var tokens = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

      if (tokens.Length == 1)
      {
        results.AddRange(WebSearchShortcutStorage.GetRecords(args).Select(x => GetResultForSelectOrOpen(x, args, query)));
      }

      var item = WebSearchShortcutStorage.GetRecord(tokens[0]);
      if (tokens.Length == 2 && item != null)
      {
        results.Add(GetResultForSearch(item, tokens[1], query));
        results.AddRange(SuggestionsCache);
      }

      if (WebSearchShortcutStorage.DefaultItem != null && WebSearchShortcutStorage.GetRecord(tokens[0], true) == null)
      {
        results.Add(GetResultForSearch(WebSearchShortcutStorage.DefaultItem, args, query, true));
        results.AddRange(SuggestionsCache);
      }
      return results;
    }

    public List<Result> Query(Query query, bool delayedExecution)
    {
      if (query?.Search is null || !delayedExecution || !string.IsNullOrEmpty(WebSearchShortcutStorage.LoadError))
      {
        return ResetSuggestionsCache();
      }

      if (string.IsNullOrEmpty(query.Search.Trim()))
      {
        return ResetSuggestionsCache();
      }

      if (query.Search.Trim().Equals("!reload", StringComparison.OrdinalIgnoreCase) || query.Search.Trim().Equals("!config", StringComparison.OrdinalIgnoreCase))
      {
        return ResetSuggestionsCache();
      }

      List<Result> results = [];
      var tokens = query.Search.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

      var item = WebSearchShortcutStorage.GetRecord(tokens[0]);
      var defaultItem = WebSearchShortcutStorage.DefaultItem;

      if (tokens.Length == 1 && defaultItem == null)
      {
        ResetSuggestionsCache();
      }
      else if (tokens.Length == 1 && item != null)
      {
        ResetSuggestionsCache();
      }
      else if (item != null && !string.IsNullOrEmpty(item.SuggestionProvider))
      {
        results = ProcessItem(item, tokens, query, results);
      }
      else if (WebSearchShortcutStorage.GetRecord(tokens[0], true) == null && defaultItem != null && !string.IsNullOrEmpty(defaultItem.SuggestionProvider))
      {
        results = ProcessDefaultItem(defaultItem, tokens, query, results);
      }
      else
      {
        return ResetSuggestionsCache();
      }

      return results;

      List<Result> ResetSuggestionsCache()
      {
        SuggestionsCache = [];
        return [];
      }

      List<Result> ProcessItem(Item item, string[] tokens, Query query, List<Result> results)
      {
        if (string.IsNullOrEmpty(item.SuggestionProvider))
        {
          return results;
        }
        var suggestions = suggestion.QuerySuggestionsAsync(item.SuggestionProvider, tokens[1]).Result;
        if (suggestions.Count == 0)
        {
          return ResetSuggestionsCache();
        }
        var suggestionsRes = suggestions.Select(s => GetResultForSuggestion(item, s, query)).ToList();
        results.AddRange(suggestionsRes);
        SuggestionsCache = [.. results];
        results.Add(GetResultForSearch(item, tokens[1], query));

        return results;
      }

      List<Result> ProcessDefaultItem(Item defaultItem, string[] tokens, Query query, List<Result> results)
      {
        if (string.IsNullOrEmpty(defaultItem.SuggestionProvider))
        {
          return results;
        }
        var suggestions = suggestion.QuerySuggestionsAsync(defaultItem.SuggestionProvider, query.Search).Result;
        if (suggestions.Count == 0)
        {
          return ResetSuggestionsCache();
        }

        var suggestionsRes = suggestions.Select(s => GetResultForSuggestion(defaultItem, s, query, true)).ToList();
        results.AddRange(suggestionsRes);
        SuggestionsCache = [.. results];
        if (tokens.Length == 1)
        {
          results.AddRange(WebSearchShortcutStorage.GetRecords(tokens[0]).Select(x => GetResultForSelectOrOpen(x, tokens[0], query)));
        }
        results.Add(GetResultForSearch(defaultItem, query.Search, query, true));

        return results;
      }
    }

    private List<Result> SuggestionsCache { get; set; } = [];

    private Result GetResultForSelectOrOpen(Item item, string args, Query query)
    {
      string url = item.Url;

      int score = Equals(item.Keyword, args) || Equals(item.Name, args) ? 101 : 100;

      if (!url.Contains("%s"))
      {
        return new Result
        {
          ProgramArguments = url,
          QueryTextDisplay = args,
          IcoPath = item.IconPath ?? IconPath["Search"],
          Title = item.Name,
          SubTitle = $"{Resources.open} {item.Name}",
          Score = score,
          Action = _ => OpenInBrowser(url),
          ToolTipData = new ToolTipData($"{Resources.open} {item.Name} (Enter)", $"{url}"),
          ContextData = item,
        };
      }

      return new Result
      {
        QueryTextDisplay = args,
        IcoPath = item.IconPath ?? IconPath["Search"],
        Title = item.Name,
        SubTitle = Resources.select_subtitle.Replace("%name", item.Name),
        Score = score,
        Action = _ =>
        {
          var newQuery = string.IsNullOrWhiteSpace(query.ActionKeyword)
            ? $"{item.Name} "
            : $"{query.ActionKeyword} {item.Name} ";
          Context!.API.ChangeQuery(newQuery, true);
          return false;
        },
        ContextData = item,
      };
    }

    private static Result GetResultForSearch(Item item, string search, Query query, bool isDefault = false)
    {
      string searchQuery = item.EncodeUrl(search);
      string arguments = item.Url.Replace("%s", searchQuery);
      return new Result
      {
        QueryTextDisplay = query.Search,
        IcoPath = item.IconPath ?? IconPath["Search"],
        Title = isDefault && query.Search.Trim().Length == 0 ? item.Name : $"{item.Name} ⏐  {search}",
        SubTitle = Resources.search_subtitle.Replace("%name", item.Name).Replace("%search", search),
        ProgramArguments = arguments,
        Action = _ => OpenInBrowser(arguments),
        Score = isDefault ? 1001 : 1000,
        // ToolTipData = new ToolTipData("Open (Enter)", $"{arguments}"),
        ContextData = item,
      };
    }

    private static Result GetResultForSuggestion(Item item, SuggestionsItem suggest, Query query, bool isDefault = false)
    {
      var search = isDefault ? query.Search : query.Search.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[1];
      return new Result
      {
        QueryTextDisplay = query.Search.Replace(search, suggest.Title),
        IcoPath = IconPath["Suggestion"],
        Title = suggest.Title,
        SubTitle = suggest.Description,
        Action = _ => OpenInBrowser(item.Url.Replace("%s", item.EncodeUrl(suggest.Title))),
        ContextData = item,
        Score = 99,
      };
    }

    public List<ContextMenuResult> LoadContextMenus(Result result)
    {
      if (result?.ContextData is string)
      {
        return [
          new()
          {
            PluginName = PluginName,
            Title = $"{Resources.open_in_explorer} (Ctrl + Enter)",
            Glyph = "\xe838",
            FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
            Action = _ => Helper.OpenInShell(Settings.StorageDirectoryPath),
            AcceleratorKey = Key.Enter,
            AcceleratorModifiers = ModifierKeys.Control,
          }
        ];
      }
      if (result?.ContextData is null)
      {
        return [];
      }

      var item = (Item)result.ContextData;
      var domain = item.Domain;
      return [
        new()
        {
          PluginName = PluginName,
          Title = $"{Resources.open} {item.Name} (Ctrl + Enter)",
          Glyph = "\xe8a7",
          FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
          Action = _ => OpenInBrowser(domain),
          AcceleratorKey = Key.Enter,
          AcceleratorModifiers = ModifierKeys.Control,
        },
      ];
    }

    /// <summary>
    /// Initialize the plugin with the given <see cref="PluginInitContext"/>.
    /// </summary>
    /// <param name="context">The <see cref="PluginInitContext"/> for this plugin.</param>
    public void Init(PluginInitContext context)
    {
      Log.Info("Init", GetType());

      Context = context ?? throw new ArgumentNullException(nameof(context));
      Context.API.ThemeChanged += OnThemeChanged;
      UpdateIconPath(Context.API.GetCurrentTheme());
    }

    /// <summary>
    /// Creates setting panel.
    /// </summary>
    /// <returns>The control.</returns>
    /// <exception cref="NotImplementedException">method is not implemented.</exception>
    public Control CreateSettingPanel() => throw new NotImplementedException();

    /// <summary>
    /// Updates settings.
    /// </summary>
    /// <param name="settings">The plugin settings.</param>
    public void UpdateSettings(PowerLauncherPluginSettings settings)
    {
      Log.Info("UpdateSettings", GetType());
    }

    public void ReloadData()
    {
      WebSearchShortcutStorage.Load();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      Log.Info("Dispose", GetType());

      Dispose(true);
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Wrapper method for <see cref="Dispose()"/> that dispose additional objects and events form the plugin itself.
    /// </summary>
    /// <param name="disposing">Indicate that the plugin is disposed.</param>
    protected virtual void Dispose(bool disposing)
    {
      if (Disposed || !disposing)
      {
        return;
      }

      if (Context?.API != null)
      {
        Context.API.ThemeChanged -= OnThemeChanged;
      }

      Disposed = true;
    }

    private static void UpdateIconPath(Theme theme)
    {
      bool isLightTheme = theme == Theme.Light || theme == Theme.HighContrastWhite;
      foreach (string key in IconPath.Keys)
      {
        IconPath[key] = IconPath[key].Replace(isLightTheme ? "dark" : "light",
                                              isLightTheme ? "light" : "dark");
      }
    }

    private void OnThemeChanged(Theme currentTheme, Theme newTheme) => UpdateIconPath(newTheme);

    private static bool OpenInBrowser(string url)
    {
      var urls = url.Split(' ').Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
      if (urls.Length > 1)
      {
        var success = true;
        foreach (var u in urls)
        {
          var _success = OpenInBrowser(u);
          if (!_success)
          {
            success = false;
          }
        }
        return success;
      }
      if (!Helper.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, url))
      {
        Log.Error($"Plugin: {PluginName}\nCannot open {BrowserInfo.Path} with arguments {BrowserInfo.ArgumentsPattern} {url}", typeof(Item));
        return false;
      }
      return true;
    }

    public string GetTranslatedPluginTitle() => Resources.plugin_name;

    public string GetTranslatedPluginDescription() => Resources.plugin_description;
  }
}