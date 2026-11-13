using System.Globalization;

namespace Squill.Presentation.Editing;

/// <summary>The choices of the popup of a boolean cell: true, false, NULL, and the default value when the column has one.</summary>
public sealed class ChoicePicker
{
    private readonly List<(string? Text, string Label)> _choices;

    /// <summary>Lists the choices and finds the one that matches the text in the editor.</summary>
    /// <param name="current">The text in the editor.</param>
    /// <param name="canDefault">Whether the column has a default value to offer.</param>
    public ChoicePicker(string current, bool canDefault)
    {
        ArgumentNullException.ThrowIfNull(current);
        _choices = [.. CellEditorKinds.BooleanChoices.Select(c => ((string?)c.Text, c.Label))];
        if (canDefault)
            _choices.Add((null, CellEditorKinds.DefaultLabel));
        SelectedIndex = _choices.FindIndex(c => string.Equals(c.Text, current.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Gets the label of each choice.</summary>
    public IReadOnlyList<string> Labels => [.. _choices.Select(c => c.Label)];

    /// <summary>Gets the index of the choice that matches the editor text, or -1.</summary>
    public int SelectedIndex { get; }

    /// <summary>Gets the text a choice puts in the editor.</summary>
    /// <param name="index">The index of the choice.</param>
    /// <returns>The text, or <see langword="null"/> for the default value.</returns>
    public string? TextAt(int index) => _choices[index].Text;
}

/// <summary>The state of the popup of a date or date-time cell: the lists the boxes show and the selection in each, with the day list following the month and the year.</summary>
public sealed class MomentPicker
{
    private readonly CellEditorKind _kind;
    private readonly int _firstYear;
    private int _year;
    private int _month;
    private int _day;

    /// <summary>Starts from the date in the editor, or from now when it holds none.</summary>
    /// <param name="kind">Date, or date with a time of day.</param>
    /// <param name="current">The text in the editor.</param>
    public MomentPicker(CellEditorKind kind, string current)
    {
        _kind = kind;
        var start = CellEditorKinds.ParseMoment(current);
        _firstYear = Math.Max(1, start.Year - 100);
        _year = start.Year;
        _month = start.Month;
        _day = start.Day;
        Hour = start.Hour;
        Minute = start.Minute;
        Second = start.Second;
        Months = [.. CultureInfo.CurrentCulture.DateTimeFormat.MonthNames.Take(12)];
        Years = [.. Enumerable.Range(_firstYear, Math.Min(9999, start.Year + 100) - _firstYear + 1).Select(y => y.ToString(CultureInfo.InvariantCulture))];
        Hours = Numbers(0, 23, "00");
        Sixty = Numbers(0, 59, "00");
    }

    /// <summary>Gets whether the popup also picks the time of day.</summary>
    public bool HasTime => _kind == CellEditorKind.DateTime;

    /// <summary>Gets the days of the month shown, 1 to its length.</summary>
    public IReadOnlyList<string> Days => Numbers(1, DateTime.DaysInMonth(_year, _month), "0");

    /// <summary>Gets the names of the months.</summary>
    public IReadOnlyList<string> Months { get; }

    /// <summary>Gets the years offered, a hundred either side of the start.</summary>
    public IReadOnlyList<string> Years { get; }

    /// <summary>Gets the hours, 00 to 23.</summary>
    public IReadOnlyList<string> Hours { get; }

    /// <summary>Gets the values 00 to 59, for the minutes and the seconds.</summary>
    public IReadOnlyList<string> Sixty { get; }

    /// <summary>Gets or sets the selected day as an index into <see cref="Days"/>.</summary>
    public int DayIndex
    {
        get => Math.Min(_day, DateTime.DaysInMonth(_year, _month)) - 1;
        set => _day = value + 1;
    }

    /// <summary>Gets or sets the selected month as an index into <see cref="Months"/>; the day list follows.</summary>
    public int MonthIndex
    {
        get => _month - 1;
        set => _month = value + 1;
    }

    /// <summary>Gets or sets the selected year as an index into <see cref="Years"/>; the day list follows.</summary>
    public int YearIndex
    {
        get => _year - _firstYear;
        set => _year = _firstYear + value;
    }

    /// <summary>Gets or sets the selected hour, also its index in <see cref="Hours"/>.</summary>
    public int Hour { get; set; }

    /// <summary>Gets or sets the selected minute, also its index in <see cref="Sixty"/>.</summary>
    public int Minute { get; set; }

    /// <summary>Gets or sets the selected second, also its index in <see cref="Sixty"/>.</summary>
    public int Second { get; set; }

    /// <summary>Gets how many boxes the popup shows: day, month and year, then hour, minute and second for a date-time.</summary>
    public int BoxCount => HasTime ? 6 : 3;

    /// <summary>Gets the items of a box.</summary>
    /// <param name="box">0 day, 1 month, 2 year, 3 hour, 4 minute, 5 second.</param>
    /// <returns>The items, in order.</returns>
    public IReadOnlyList<string> ItemsOf(int box) => box switch { 0 => Days, 1 => Months, 2 => Years, 3 => Hours, _ => Sixty };

    /// <summary>Gets the selected index of a box.</summary>
    /// <param name="box">The box, numbered as in <see cref="ItemsOf"/>.</param>
    /// <returns>The index into <see cref="ItemsOf"/>.</returns>
    public int IndexOf(int box) => box switch { 0 => DayIndex, 1 => MonthIndex, 2 => YearIndex, 3 => Hour, 4 => Minute, _ => Second };

    /// <summary>Records the choice made in a box.</summary>
    /// <param name="box">The box, numbered as in <see cref="ItemsOf"/>.</param>
    /// <param name="index">The index the user picked.</param>
    /// <returns><see langword="true"/> when the day list changed length, so the day box must show <see cref="Days"/> again.</returns>
    public bool Select(int box, int index)
    {
        switch (box)
        {
            case 0: DayIndex = index; break;
            case 1: MonthIndex = index; break;
            case 2: YearIndex = index; break;
            case 3: Hour = index; break;
            case 4: Minute = index; break;
            default: Second = index; break;
        }

        return box is 1 or 2;
    }

    /// <summary>Gets the text to put in the editor for the selection.</summary>
    public string Text => CellEditorKinds.FormatMoment(_kind, _year, _month, DayIndex + 1, Hour, Minute, Second);

    private static string[] Numbers(int from, int to, string format) =>
        [.. Enumerable.Range(from, to - from + 1).Select(i => i.ToString(format, CultureInfo.InvariantCulture))];
}
