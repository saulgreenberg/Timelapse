/*************************************************************************************

   Toolkit for WPF
   Copyright (C) 2007-2019 Xceed Software Inc.
   This program is provided to you under the terms of the Microsoft Public
   License (Ms-PL) as published at https://opensource.org/license/ms-pl-html

   Fork origin: https://github.com/dotnetprojects/WpfExtendedToolkit
   - based on: https://github.com/xceedsoftware/wpftoolkit, Version 3
   This fork: modified for use in Timelapse project
    by Saul Greenberg, 2025 onwards

  ***********************************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace TimelapseWpf.Toolkit
{
  /// <summary>
  /// A TextBox control that combines imprint display with dropdown autocomplete functionality.
  /// The imprint is displayed when ShowImprint is true (inherited from ImprintTextBox).
  /// Autocomplete displays a dropdown list of matching suggestions as the user types.
  /// </summary>
  
  // Details for arrow manipulations on inline
  //1. _inlineMatches - List of all matching autocompletions for the current typed btext
  //   - _inlineMatchIndex - Current position in the matches list
  //   - _userTypedLength - Length of text actually typed by user(excluding autocompleted portion)

  //2. PerformInlineCompletion() method:
  //- Builds a complete list of all matching suggestions(not just finding the first one)
  //- Stores matches in _inlineMatches list for cycling
  //- Tracks the current index position
  //- Prioritizes the most recently used completion if available

  //3. OnPreviewKeyDown() method:
  //- Handles both popup mode and inline mode
  //- For inline mode: When autocompletion is shown with selected text, Up/Down arrow keys now cycle through alternatives
  //- Only activates when there's a selection at the expected position

  //4. CycleInlineCompletion() method:
  //- Handles Up(previous) and Down(next) navigation
  //- Wraps around: At the end of the list, Down goes to the beginning; at the beginning, Up goes to the end
  //- Updates the text while maintaining the user-typed portion and selecting only the autocompleted part

  //5. Example User Experience:
  //When in inline mode(AutocompletionsAsPopup = false):
  //1. Type "A" → shows "A note" with "note" selected
  //2. Press Down → cycles to "Another note"
  //3. Press Down → cycles to "A third note"
  //4. Press Down → wraps back to "A note"
  //5. Press Up → wraps to "A third note"
  //6. Continue pressing Up/Down to cycle through all matches infinitely

  //The feature only activates when there are multiple matches and an autocompletion    
  //is currently displayed with selection.
  public class ImprintAutoCompleteTextBox : ImprintTextBox
  {
    #region Template Parts

    private const string PART_Popup = "PART_Popup";
    private const string PART_Selector = "PART_Selector";

    private Popup _popup;
    private ListBox _listBox;

    #endregion //Template Parts

    #region Properties

    #region Autocompletions

    /// <summary>
    /// Gets or sets the dictionary of autocomplete suggestions.
    /// Keys are the suggestion strings, values are not used (can be null).
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    private Dictionary<string, string> Autocompletions { get; set; }

    #endregion //Autocompletions

    #region FilteredSuggestions

    private static readonly DependencyPropertyKey FilteredSuggestionsPropertyKey =
      DependencyProperty.RegisterReadOnly(
        nameof(FilteredSuggestions),
        typeof(ObservableCollection<string>),
        typeof(ImprintAutoCompleteTextBox),
        new(null));

    public static readonly DependencyProperty FilteredSuggestionsProperty =
      FilteredSuggestionsPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the filtered list of suggestions displayed in the dropdown.
    /// </summary>
    public ObservableCollection<string> FilteredSuggestions
    {
      get => (ObservableCollection<string>)GetValue(FilteredSuggestionsProperty);
      private init => SetValue(FilteredSuggestionsPropertyKey, value);
    }

    #endregion //FilteredSuggestions

    #region IsDropDownOpen

    public static readonly DependencyProperty IsDropDownOpenProperty =
      DependencyProperty.Register(
        nameof(IsDropDownOpen),
        typeof(bool),
        typeof(ImprintAutoCompleteTextBox),
        new(false));

    /// <summary>
    /// Gets or sets whether the dropdown suggestion list is open.
    /// </summary>
    public bool IsDropDownOpen
    {
      get => (bool)GetValue(IsDropDownOpenProperty);
      set => SetValue(IsDropDownOpenProperty, value);
    }

    #endregion //IsDropDownOpen

    #region MaxDropDownHeight

    public static readonly DependencyProperty MaxDropDownHeightProperty =
      DependencyProperty.Register(
        nameof(MaxDropDownHeight),
        typeof(double),
        typeof(ImprintAutoCompleteTextBox),
        new(200.0));

    /// <summary>
    /// Gets or sets the maximum height of the dropdown suggestion list.
    /// </summary>
    public double MaxDropDownHeight
    {
      get => (double)GetValue(MaxDropDownHeightProperty);
      set => SetValue(MaxDropDownHeightProperty, value);
    }

    #endregion //MaxDropDownHeight

    #region AutocompletePopupMaxSize

    public static readonly DependencyProperty AutocompletePopupMaxSizeProperty =
      DependencyProperty.Register(
        nameof(AutocompletePopupMaxSize),
        typeof(int),
        typeof(ImprintAutoCompleteTextBox),
        new(10));

    /// <summary>
    /// Gets or sets the maximum number of suggestions to show in the popup.
    /// If the number of matches exceeds this value, the popup will not be displayed.
    /// Default is 10. Only applicable when AutocompletionsAsPopup is true.
    /// </summary>
    public int AutocompletePopupMaxSize
    {
      get => (int)GetValue(AutocompletePopupMaxSizeProperty);
      set => SetValue(AutocompletePopupMaxSizeProperty, value);
    }

    #endregion //AutocompletePopupMaxSize

    #region AutocompletionsAsPopup

    public static readonly DependencyProperty AutocompletionsAsPopupProperty =
      DependencyProperty.Register(
        nameof(AutocompletionsAsPopup),
        typeof(bool),
        typeof(ImprintAutoCompleteTextBox),
        new(true));

    /// <summary>
    /// Gets or sets whether autocomplete suggestions are shown as a popup dropdown (true)
    /// or as inline completion with text selection (false).
    /// Default is true (popup mode).
    /// </summary>
    public bool AutocompletionsAsPopup
    {
      get => (bool)GetValue(AutocompletionsAsPopupProperty);
      set => SetValue(AutocompletionsAsPopupProperty, value);
    }

    #endregion //AutocompletionsAsPopup

    #endregion //Properties

    #region Private Fields

    // Configuration constants for performance with large datasets
    // MAX_AUTOCOMPLETE_SIZE: Overall maximum entries (user-typed + bulk-loaded)
    // MAX_AUTOCOMPLETE_BULKLOAD_SIZE: Maximum unique values to load during bulk initialization
    //   - If bulk data has <= 1000 unique values: load all of them
    //   - If bulk data has > 1000 unique values: don't load any (user-typed values still work)
    // MAX_INLINE_MATCHES: Limit cycling to first N matches in inline mode
    private const int MAX_AUTOCOMPLETE_SIZE = 2000; // Maximum total entries in dictionary
    private const int MAX_AUTOCOMPLETE_BULKLOAD_SIZE = 1000; // Maximum entries from bulk load
    private const int MAX_INLINE_MATCHES = 20; // Maximum matches to cycle through in inline mode

    private bool _isUpdatingText;
    private bool _suppressTextChanged;
    private string _mostRecentAutocompletion;
    private readonly List<string> _inlineMatches;
    private int _inlineMatchIndex;
    private int _userTypedLength; // Length of text actually typed by user (not autocompleted)

    #endregion //Private Fields

    #region Constructors

    static ImprintAutoCompleteTextBox()
    {
      DefaultStyleKeyProperty.OverrideMetadata(
        typeof(ImprintAutoCompleteTextBox),
        new FrameworkPropertyMetadata(typeof(ImprintAutoCompleteTextBox)));
    }

    public ImprintAutoCompleteTextBox()
    {
      FilteredSuggestions = [];
      _mostRecentAutocompletion = null;
      _inlineMatches = [];
      _inlineMatchIndex = -1;
      _userTypedLength = 0;
      TextChanged += OnTextChangedForAutocomplete;
      PreviewKeyDown += OnPreviewKeyDown;
      LostFocus += OnLostFocusHandler;
    }

    #endregion //Constructors

    #region Base Class Overrides

    public override void OnApplyTemplate()
    {
      base.OnApplyTemplate();

      _popup = GetTemplateChild(PART_Popup) as Popup;
      _listBox = GetTemplateChild(PART_Selector) as ListBox;

      if (_listBox != null)
      {
        _listBox.PreviewMouseLeftButtonDown += OnListBoxPreviewMouseLeftButtonDown;
        _listBox.SelectionChanged += OnListBoxSelectionChanged;
      }
    }

    #endregion //Base Class Overrides

    #region Event Handlers

    private void OnTextChangedForAutocomplete(object sender, TextChangedEventArgs eventArgs)
    {
      if (_suppressTextChanged || _isUpdatingText)
        return;

      string searchText = Text?.TrimStart();

      // Update text if trimming occurred
      if (!string.IsNullOrEmpty(Text) && searchText != Text)
      {
        int cursorPosition = CaretIndex - (Text.Length - searchText.Length);
        if (cursorPosition < 0) cursorPosition = 0;

        _suppressTextChanged = true;
        Text = searchText;
        CaretIndex = cursorPosition;
        _suppressTextChanged = false;
      }

      // Use appropriate autocomplete mode
      if (AutocompletionsAsPopup)
      {
        // Popup mode: filter and show dropdown
        FilterSuggestions(searchText);
      }
      else
      {
        // Inline mode: complete text with selection
        PerformInlineCompletion(eventArgs);
      }

      // Synchronize tooltip
      ToolTip = Text;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
      // Handle popup mode navigation
      if (IsDropDownOpen)
      {
        if (_listBox == null || FilteredSuggestions.Count == 0)
          return;

        switch (e.Key)
        {
          case Key.Down:
            if (_listBox.SelectedIndex < FilteredSuggestions.Count - 1)
            {
              _listBox.SelectedIndex++;
              _listBox.ScrollIntoView(_listBox.SelectedItem);
            }
            e.Handled = true;
            break;

          case Key.Up:
            if (_listBox.SelectedIndex > 0)
            {
              _listBox.SelectedIndex--;
              _listBox.ScrollIntoView(_listBox.SelectedItem);
            }
            e.Handled = true;
            break;

          case Key.Enter:
          case Key.Tab:
            if (_listBox.SelectedItem != null)
            {
              SelectSuggestion(_listBox.SelectedItem.ToString());
            }
            IsDropDownOpen = false;
            e.Handled = (e.Key == Key.Enter);
            break;

          case Key.Escape:
            IsDropDownOpen = false;
            e.Handled = true;
            break;
        }
      }
      // Handle inline mode navigation (when not in popup mode)
      else if (!AutocompletionsAsPopup && _inlineMatches is { Count: > 1 })
      {
        // Only handle Up/Down if we have a current selection (text is selected)
        if (SelectionLength > 0 && SelectionStart == _userTypedLength)
        {
          switch (e.Key)
          {
            case Key.Down:
              CycleInlineCompletion(1); // Move forward
              e.Handled = true;
              break;

            case Key.Up:
              CycleInlineCompletion(-1); // Move backward
              e.Handled = true;
              break;
          }
        }
      }
    }

    private void OnLostFocusHandler(object sender, RoutedEventArgs e)
    {
      // Close dropdown when focus is lost (unless clicking in the dropdown)
      if (_listBox is { IsMouseOver: false })
      {
        IsDropDownOpen = false;
      }
    }

    private void OnListBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      // Keep focus on textbox when clicking listbox
      e.Handled = true;

      var item = GetListBoxItemFromPoint((ListBox)sender, e.GetPosition((ListBox)sender));
      if (item is { Content: string suggestion })
      {
        SelectSuggestion(suggestion);
        IsDropDownOpen = false;
      }

      // Return focus to textbox
      Focus();
    }

    private void OnListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      // Scroll selected item into view
      if (_listBox is { SelectedItem: not null })
      {
        _listBox.ScrollIntoView(_listBox.SelectedItem);
      }
    }

    #endregion //Event Handlers

    #region Public Methods
    // AutoCompletionsAddValues
    // - single value, then just add it unless the dictionary is really large
    // - dictionary (multiple values), then add it if it meets certain conditions
    public void AutoCompletionsAddValuesIfNeeded(string value)
    {
      Autocompletions ??= new();

      // Only add if we haven't hit the maximum size
      // This prevents unbounded memory growth with large datasets
      if (Autocompletions.Count < MAX_AUTOCOMPLETE_SIZE)
      {
        Autocompletions.TryAdd(value, null);
      }
    }

    // Initializes or merges autocomplete suggestions from a bulk-loaded dictionary.
    // but if count is greater than MAX_AUTOCOMPLETE_BULKLOAD_SIZE, it skips to avoid overwhelming autocomplete.
    public void AddToAutocompletions(Dictionary<string, string> autocompletions)
    {
      // If no autocompletions provided or empty, initialize empty dictionary
      if (autocompletions == null || autocompletions.Count == 0)
      {
        Autocompletions ??= new();
        return;
      }

      // Hybrid bulk-load strategy:
      // - Small datasets (<= 1000 unique): Load all values for helpful autocomplete
      // - Large datasets (> 1000 unique): Don't load to avoid overwhelming autocomplete
      //   (User-typed values will still be added via AutoCompletionsAddValueIfNeeded)
      if (autocompletions.Count > MAX_AUTOCOMPLETE_BULKLOAD_SIZE)
      {
        // Too many unique values - skip bulk loading
        // Preserve any existing user-typed values
        Autocompletions ??= new();
        return;
      }

      // If Autocompletions doesn't exist yet, use the provided dictionary directly
      if (Autocompletions == null)
      {
        Autocompletions = autocompletions;
      }
      else
      {
        // Autocompletions already exists (e.g., from previous loads or user input)
        // Merge new values, respecting MAX_AUTOCOMPLETE_SIZE overall limit
        foreach (var kvp in autocompletions)
        {
          if (Autocompletions.Count >= MAX_AUTOCOMPLETE_SIZE)
          {
            break; // Hit overall limit
          }
          Autocompletions.TryAdd(kvp.Key, kvp.Value);
        }
      }
    }

    public Dictionary<string, string> AutoCompletionsGetAsDictionary()
    {
      return Autocompletions;
    }
    #endregion

    #region Private Methods

    /// <summary>
    /// Adaptively determines the minimum number of characters required before showing autocompletions.
    /// Adjusts based on dictionary size to maintain performance with large datasets.
    /// </summary>
    private int GetMinimumCharactersRequired()
    {
      if (Autocompletions == null) return 1;

      int count = Autocompletions.Count;
      if (count < 100) return 0;      // Show immediately for small datasets
      if (count < 500) return 1;      // Require 1 char for medium datasets
      if (count < 2000) return 2;     // Require 2 chars for larger sets
      return 3;                        // Require 3 chars for very large sets
    }

    private void FilterSuggestions(string searchText)
    {
      FilteredSuggestions.Clear();

      if (string.IsNullOrEmpty(searchText) || Autocompletions == null)
      {
        IsDropDownOpen = false;
        return;
      }

      // Check minimum character requirement (adaptive based on dictionary size)
      if (searchText.Length < GetMinimumCharactersRequired())
      {
        IsDropDownOpen = false;
        return;
      }

      // Find all suggestions that start with the search text (case-sensitive)
      var matches = Autocompletions.Keys
        .Where(key => key.StartsWith(searchText, StringComparison.Ordinal))
        .OrderBy(key => key)
        .ToList();

      foreach (var match in matches)
      {
        FilteredSuggestions.Add(match);
      }

      // Open dropdown if we have matches and don't exceed max size
      if (FilteredSuggestions.Count > 0 && FilteredSuggestions.Count <= AutocompletePopupMaxSize)
      {
        IsDropDownOpen = true;
        if (_listBox != null)
        {
          _listBox.SelectedIndex = 0;
        }
      }
      else
      {
        IsDropDownOpen = false;
      }
    }

    private void SelectSuggestion(string suggestion)
    {
      _isUpdatingText = true;
      Text = suggestion;
      CaretIndex = suggestion.Length;
      _isUpdatingText = false;
    }

    private ListBoxItem GetListBoxItemFromPoint(ListBox listBox, Point point)
    {
      var element = listBox.InputHitTest(point) as UIElement;
      while (element != null)
      {
        if (element is ListBoxItem item)
          return item;

        element = System.Windows.Media.VisualTreeHelper.GetParent(element) as UIElement;
      }
      return null;
    }

    private void PerformInlineCompletion(TextChangedEventArgs eventArgs)
    {
      // Only attempt autocomplete if:
      // 1. Text is not empty
      // 2. Caret is not at the beginning (user is actively typing)
      // 3. There are added characters (not just deletions)
      if (!string.IsNullOrEmpty(Text) &&
          CaretIndex > 0 &&
          eventArgs.Changes.Any(change => change.AddedLength > 0))
      {
        int textLength = Text.Length;
        _userTypedLength = textLength;

        // Check minimum character requirement (adaptive based on dictionary size)
        if (textLength < GetMinimumCharactersRequired())
        {
          _inlineMatches.Clear();
          _inlineMatchIndex = -1;
          return;
        }

        // Build list of all matching completions for cycling
        _inlineMatches.Clear();
        _inlineMatchIndex = -1;

        if (Autocompletions != null)
        {
          // Get all matches that start with the typed text
          // Limit to MAX_INLINE_MATCHES to prevent cycling through thousands of entries
          var matches = Autocompletions.Keys
            .Where(UseCompletion)
            .OrderBy(key => key)
            .Take(MAX_INLINE_MATCHES)
            .ToList();

          _inlineMatches.AddRange(matches);

          // If we have the most recent completion in the list, prioritize it
          if (_inlineMatches.Count > 0)
          {
            string autocompletion = null;

            if (UseCompletion(_mostRecentAutocompletion) && _inlineMatches.Contains(_mostRecentAutocompletion))
            {
              // Use most recent completion and set its index
              autocompletion = _mostRecentAutocompletion;
              _inlineMatchIndex = _inlineMatches.IndexOf(_mostRecentAutocompletion);
            }
            else
            {
              // Use first match
              autocompletion = _inlineMatches[0];
              _inlineMatchIndex = 0;
            }

            // Apply the autocomplete suggestion
            if (!string.IsNullOrEmpty(autocompletion))
            {
              _suppressTextChanged = true;
              Text = autocompletion;
              CaretIndex = textLength;
              SelectionStart = textLength;
              SelectionLength = autocompletion.Length - textLength;
              _suppressTextChanged = false;

              _mostRecentAutocompletion = autocompletion;
            }
          }
        }
      }
      else
      {
        // Reset inline match tracking when not actively completing
        _inlineMatches.Clear();
        _inlineMatchIndex = -1;
      }
    }

    /// <summary>
    /// Cycles through inline autocompletion suggestions using Up/Down arrow keys.
    /// </summary>
    /// <param name="direction">1 for Down (next), -1 for Up (previous)</param>
    private void CycleInlineCompletion(int direction)
    {
      if (_inlineMatches == null || _inlineMatches.Count == 0)
        return;

      // Calculate new index with wraparound
      _inlineMatchIndex += direction;

      // Wrap around at the ends
      if (_inlineMatchIndex >= _inlineMatches.Count)
      {
        _inlineMatchIndex = 0; // Wrap to beginning
      }
      else if (_inlineMatchIndex < 0)
      {
        _inlineMatchIndex = _inlineMatches.Count - 1; // Wrap to end
      }

      // Apply the selected completion
      string newCompletion = _inlineMatches[_inlineMatchIndex];

      _suppressTextChanged = true;
      _isUpdatingText = true;

      Text = newCompletion;
      CaretIndex = _userTypedLength;
      SelectionStart = _userTypedLength;
      SelectionLength = newCompletion.Length - _userTypedLength;

      _isUpdatingText = false;
      _suppressTextChanged = false;

      _mostRecentAutocompletion = newCompletion;
    }

    private bool UseCompletion(string completion)
    {
      int textLength = Text.Length;
      if (completion != null &&
          completion.Length > textLength &&
          completion[..textLength].Equals(Text, StringComparison.Ordinal))
      {
        return true;
      }
      return false;
    }

    #endregion //Private Methods
  }

  public class AutocompleteSuggestion(string term)
  {
    public string Term { get; set; } = term;
    public int Frequency { get; set; } = 1;
    public DateTime LastUsed { get; set; } = DateTime.Now;

    public void IncrementUsage()
    {
      Frequency++;
      LastUsed = DateTime.Now;
    }

    // Calculate a ranking score based on frequency and recency
    public double GetRankingScore(DateTime now)
    {
      // Adjust weights as needed
      double frequencyWeight = 0.7;
      double recencyWeight = 0.3;

      // Simple recency calculation: higher score for more recent usage
      // You might use a more sophisticated decay function for recency
      double recencyScore = 1.0 / (1 + (now - LastUsed).TotalHours);

      return (Frequency * frequencyWeight) + (recencyScore * recencyWeight);
    }
  }

  public class AutocompleteService
  {
    private readonly Dictionary<string, AutocompleteSuggestion> _suggestions = new(StringComparer.OrdinalIgnoreCase);

    public void AddOrUpdateSuggestion(string term)
    {
      if (_suggestions.TryGetValue(term, out AutocompleteSuggestion existingSuggestion))
      {
        existingSuggestion.IncrementUsage();
      }
      else
      {
        _suggestions.Add(term, new(term));
      }
    }

    public List<string> GetSuggestions(string input, int maxSuggestions = 5)
    {
      if (string.IsNullOrWhiteSpace(input))
      {
        return [];
      }

      DateTime now = DateTime.Now;

      return _suggestions.Values
          .Where(s => s.Term.StartsWith(input, StringComparison.OrdinalIgnoreCase)) // Prefix matching
          .OrderByDescending(s => s.GetRankingScore(now)) // Rank by score
          .ThenBy(s => s.Term.Length) // Prefer shorter terms among equally ranked
          .ThenBy(s => s.Term) // Alphabetical tie-breaker
          .Take(maxSuggestions)
          .Select(s => s.Term)
          .ToList();
    }
  }
}
