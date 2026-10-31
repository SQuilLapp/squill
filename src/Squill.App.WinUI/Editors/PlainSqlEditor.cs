using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Squill.Core;
using Squill.Presentation.Editor;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Windows.System;

namespace Squill.App.WinUI.Editors;

/// <summary>
/// Interim <see cref="ISqlEditor"/> over a multi-line <see cref="TextBox"/>: no highlighting, completion in a popup,
/// diagnostics as a count. It stays as the fallback when LoomEditor is not available.
/// </summary>
public sealed class PlainSqlEditor : ISqlEditor
{
    private const double LineHeight = 20;
    private const double CharWidth = 8;
    private const double Padding = 10;

    private readonly TextBox _box;
    private readonly TextBlock _diagnosticsLabel;
    private readonly Popup _popup;
    private readonly ListView _list;
    private ISqlCompletionProvider? _completion;
    private DatabaseDialect _dialect;
    private IReadOnlyList<CompletionItem> _items = [];
    private bool _applying;

    /// <summary>Initializes a new instance of the <see cref="PlainSqlEditor"/> class with an empty script.</summary>
    public PlainSqlEditor()
    {
        _box = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code, Consolas"),
            FontSize = 14,
            IsSpellCheckEnabled = false,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_box, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(_box, ScrollBarVisibility.Auto);
        _diagnosticsLabel = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 18, 6),
            IsHitTestVisible = false,
            Opacity = 0.7,
        };
        _list = new ListView { MaxHeight = 220, MinWidth = 220, IsItemClickEnabled = true };
        _list.ItemClick += (_, e) => Accept(_items[_list.Items.IndexOf(e.ClickedItem)]);
        _popup = new Popup { Child = new Border { Child = _list, BorderThickness = new Thickness(1), Background = _box.Background } };
        Control = new Grid { Children = { _box, _diagnosticsLabel, _popup } };

        _box.TextChanged += OnTextChanged;
        _box.SelectionChanged += (_, _) => CaretChanged?.Invoke(this, EventArgs.Empty);
        _box.PreviewKeyDown += OnPreviewKeyDown;
        _box.LostFocus += (_, _) => Hide();
    }

    /// <summary>Gets the element to place in a view.</summary>
    public FrameworkElement Control { get; }

    /// <inheritdoc />
    public string Text
    {
        // TextBox stores line breaks as a single '\r', so offsets stay valid after the replacement.
        get => _box.Text.Replace('\r', '\n');
        set => _box.Text = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <inheritdoc />
    public int CaretOffset
    {
        get => _box.SelectionStart + _box.SelectionLength;
        set => _box.Select(Math.Clamp(value, 0, _box.Text.Length), 0);
    }

    /// <inheritdoc />
    public TextRange Selection
    {
        get => new(_box.SelectionStart, _box.SelectionLength);
        set
        {
            var start = Math.Clamp(value.Start, 0, _box.Text.Length);
            _box.Select(start, Math.Clamp(value.Length, 0, _box.Text.Length - start));
        }
    }

    /// <inheritdoc />
    public event EventHandler? TextChanged;

    /// <inheritdoc />
    public event EventHandler? CaretChanged;

    /// <inheritdoc />
    public void SetDialect(DatabaseDialect d) => _dialect = d;

    /// <inheritdoc />
    public void SetDiagnostics(IReadOnlyList<SqlDiagnostic> diagnostics)
    {
        var errors = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        var warnings = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
        _diagnosticsLabel.Text = errors + warnings == 0 ? "" : $"{errors} error(s), {warnings} warning(s)";
    }

    /// <inheritdoc />
    public void SetCompletionProvider(ISqlCompletionProvider provider) => _completion = provider;

    /// <inheritdoc />
    /// <remarks>A plain text box has no per-position hover surface, so the provider is not used.</remarks>
    public void SetHoverProvider(Func<SqlRequest, HoverInfo?> provider)
    {
        // Intentionally empty: see remarks.
    }

    /// <inheritdoc />
    public void FocusEditor() => _box.Focus(FocusState.Programmatic);

    /// <summary>Asks the completion provider for items at the caret and keeps the ones matching the typed prefix.</summary>
    /// <returns>The filtered items; empty when no provider is set.</returns>
    public IReadOnlyList<CompletionItem> RequestCompletions()
    {
        if (_completion is null)
        {
            return [];
        }

        var caret = CaretOffset;
        var text = Text;
        var items = _completion.GetCompletions(new SqlRequest(text, caret, _dialect, null));
        if (items.Count == 0)
        {
            return items;
        }

        var start = items[0].ReplaceRange.Start;
        return CompletionFilter.Filter(items, start >= 0 && start <= caret ? text[start..caret] : "");
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        TextChanged?.Invoke(this, EventArgs.Empty);
        var caret = CaretOffset;
        if (!_applying && caret > 0 && caret <= _box.Text.Length && IsIdentifierChar(_box.Text[caret - 1]))
        {
            ShowCompletion();
        }
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '.';

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (e.Key == VirtualKey.Space && ctrl)
        {
            e.Handled = true;
            ShowCompletion();
        }
        else if (_popup.IsOpen)
        {
            e.Handled = HandlePopupKey(e.Key);
        }
    }

    private bool HandlePopupKey(VirtualKey key)
    {
        switch (key)
        {
            case VirtualKey.Down:
            case VirtualKey.Up:
                var step = key == VirtualKey.Down ? 1 : -1;
                _list.SelectedIndex = Math.Clamp(_list.SelectedIndex + step, 0, _items.Count - 1);
                _list.ScrollIntoView(_list.SelectedItem);
                return true;
            case VirtualKey.Enter:
            case VirtualKey.Tab:
                Accept(_items[Math.Max(_list.SelectedIndex, 0)]);
                return true;
            case VirtualKey.Escape:
                Hide();
                return true;
            default:
                return false;
        }
    }

    private void ShowCompletion()
    {
        _items = RequestCompletions();
        if (_items.Count == 0)
        {
            Hide();
            return;
        }

        _list.ItemsSource = _items.Select(i => i.Detail is null ? i.Label : $"{i.Label}  {i.Detail}").ToArray();
        _list.SelectedIndex = 0;
        var text = _box.Text;
        var caret = Math.Min(CaretOffset, text.Length);
        var lineStart = caret == 0 ? 0 : text.LastIndexOf('\r', caret - 1) + 1;
        var line = text.AsSpan(0, caret).Count('\r');
        // shortcut: position ignores scrolling and non-fixed fonts, fine for an interim editor replaced by LoomEditor.
        _popup.XamlRoot = _box.XamlRoot;
        _popup.HorizontalOffset = Padding + ((caret - lineStart) * CharWidth);
        _popup.VerticalOffset = Padding + ((line + 1) * LineHeight);
        _popup.IsOpen = true;
    }

    private void Accept(CompletionItem item)
    {
        var caret = CaretOffset;
        var start = Math.Clamp(item.ReplaceRange.Start, 0, caret);
        _applying = true;
        try
        {
            _box.Select(start, caret - start);
            _box.SelectedText = item.InsertText;
        }
        finally
        {
            _applying = false;
        }

        Hide();
        _box.Focus(FocusState.Programmatic);
    }

    private void Hide() => _popup.IsOpen = false;
}
