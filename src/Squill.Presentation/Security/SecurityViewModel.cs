using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Core.Security;
using Squill.Data;
using Squill.Presentation.Localization;
using Squill.Presentation.Safety;
using Squill.Presentation.Sessions;
using Squill.Sql.Security;
using Squill.Sql.Transactions;

namespace Squill.Presentation.Security;

/// <summary>
/// The security tab of one PostgreSQL connection: the roles, the privilege matrix of the selected role, the generated GRANT / REVOKE
/// script and its application. Everything is read-only until <see cref="ApplyCommand"/>, which always asks the production
/// confirmation on a production connection.
/// </summary>
public sealed partial class SecurityViewModel : ViewModelBase, IDisposable
{
    private const int Listed = 5;

    private readonly ConnectionProfile _profile;
    private readonly ISecurityReader _reader;
    private readonly IDatabaseConnection _connection;
    private readonly SessionTransaction? _transaction;
    private readonly IDialogService _dialogs;
    private IReadOnlyList<RoleInfo> _roles = [];
    private PrivilegeChangeSet _changes = new("");
    private CancellationTokenSource? _loadCts;
    private int _sequence;
    private bool _disposed;

    /// <summary>Creates the tab of a connected session.</summary>
    /// <param name="session">The session; its connection must implement <see cref="ISecurityReader"/>.</param>
    /// <param name="dialogs">Used for the confirmations and the error messages.</param>
    /// <exception cref="InvalidOperationException">The engine cannot report its roles.</exception>
    public SecurityViewModel(ConnectionSession session, IDialogService dialogs)
        : this(session?.Profile ?? throw new ArgumentNullException(nameof(session)),
            session.Connection as ISecurityReader ?? throw new InvalidOperationException(UiText.Get("security.disabled.engine")),
            session.Connection, session.Transaction, dialogs)
    {
        Session = session;
    }

    internal SecurityViewModel(ConnectionProfile profile, ISecurityReader reader, IDatabaseConnection connection, SessionTransaction? transaction, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(dialogs);
        _profile = profile;
        _reader = reader;
        _connection = connection;
        _transaction = transaction;
        _dialogs = dialogs;
    }

    /// <summary>Raised when the user closes the tab.</summary>
    public event Action<SecurityViewModel>? CloseRequested;

    /// <summary>Raised by <see cref="CopyScriptCommand"/>; the head puts the text on the clipboard.</summary>
    public event Action<string>? CopyRequested;

    /// <summary>Gets the connection session the tab belongs to, for the status bar and the colour stripe.</summary>
    public ConnectionSession? Session { get; }

    /// <summary>Gets the tab title.</summary>
    public string Title => UiText.Format("security.title", _profile.Name);

    /// <summary>Gets the environment of the connection, which the head turns into the tab colour.</summary>
    public ConnectionEnvironment Environment => _profile.Environment;

    /// <summary>Tells whether a session can open the security tab, and says why not.</summary>
    /// <param name="session">A session, or <see langword="null"/> when not connected.</param>
    /// <returns>An empty text when it can, else the localized reason.</returns>
    public static string DisabledReason(ConnectionSession? session) =>
        session is null ? UiText.Get("security.disabled.notConnected")
        : session.Connection is ISecurityReader ? ""
        : UiText.Get("security.disabled.engine");

    // ---- roles ------------------------------------------------------------------------------------------------------

    /// <summary>Gets the group roles that match <see cref="RoleFilter"/>.</summary>
    public ObservableCollection<RoleInfo> GroupRoles { get; } = [];

    /// <summary>Gets the user roles that match <see cref="RoleFilter"/>.</summary>
    public ObservableCollection<RoleInfo> UserRoles { get; } = [];

    /// <summary>Gets the heading of the group list, <c>Rôles de groupe n</c>.</summary>
    [ObservableProperty]
    public partial string GroupsHeading { get; private set; } = "";

    /// <summary>Gets the heading of the user list, <c>Utilisateurs n</c>.</summary>
    [ObservableProperty]
    public partial string UsersHeading { get; private set; } = "";

    /// <summary>Gets or sets the text the role lists are filtered by.</summary>
    [ObservableProperty]
    public partial string RoleFilter { get; set; } = "";

    partial void OnRoleFilterChanged(string value) => RebuildRoleLists();

    /// <summary>Gets the role whose privileges are shown.</summary>
    [ObservableProperty]
    public partial RoleInfo? SelectedRole { get; private set; }

    /// <summary>Gets the line under a role in the list: <c>n membres</c> for a group, <c>LOGIN</c> and <c>SUPERUSER</c> badges for a user.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The localized text.</returns>
    public static string RoleSubtitle(RoleInfo role)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (role.IsGroup)
            return UiText.Format("security.members", role.Members.Count);
        return string.Join(" ", Badges(role).Where(b => b != "INHERIT" && b != "NOLOGIN"));
    }

    private static IEnumerable<string> Badges(RoleInfo role)
    {
        yield return role.CanLogin ? "LOGIN" : "NOLOGIN";
        if (role.Inherits)
            yield return "INHERIT";
        if (role.IsSuperuser)
            yield return "SUPERUSER";
    }

    /// <summary>Gets the badges of the header: <c>NOLOGIN</c>, <c>INHERIT</c>, <c>SUPERUSER</c> as the role says.</summary>
    public IReadOnlyList<string> RoleBadges => SelectedRole is { } r ? [.. Badges(r)] : [];

    /// <summary>Gets the subtitle of the header, <c>Rôle de groupe . membre de x . 2 membres : a, b</c>.</summary>
    public string RoleSubtitleText
    {
        get
        {
            if (SelectedRole is not { } r)
                return "";
            var parts = new List<string> { UiText.Get(r.IsGroup ? "security.kind.group" : "security.kind.user") };
            if (r.MemberOf.Count > 0)
                parts.Add(UiText.Format("security.memberOf", string.Join(", ", r.MemberOf)));
            if (r.Members.Count > 0)
                parts.Add(UiText.Format("security.membersList", r.Members.Count, string.Join(", ", r.Members)));
            return string.Join(" · ", parts);
        }
    }

    private void RebuildRoleLists()
    {
        var shown = _roles.Where(r => RoleFilter.Length == 0 || r.Name.Contains(RoleFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        Fill(GroupRoles, shown.Where(r => r.IsGroup));
        Fill(UserRoles, shown.Where(r => !r.IsGroup));
        GroupsHeading = UiText.Format("security.groups", _roles.Count(r => r.IsGroup));
        UsersHeading = UiText.Format("security.users", _roles.Count(r => !r.IsGroup));
    }

    private static void Fill(ObservableCollection<RoleInfo> target, IEnumerable<RoleInfo> source)
    {
        target.Clear();
        foreach (var role in source)
            target.Add(role);
    }

    // ---- schemas ----------------------------------------------------------------------------------------------------

    /// <summary>Gets the user schemas the matrix can show.</summary>
    public IReadOnlyList<string> AvailableSchemas { get; private set; } = [];

    /// <summary>Gets the schemas the matrix shows; the default schema at first.</summary>
    public IReadOnlyList<string> SelectedSchemas { get; private set; } = [];

    /// <summary>Gets the text of the schema selector, <c>Schéma : public, analytics</c>.</summary>
    public string SchemaSelectorText => UiText.Format("security.schemas", string.Join(", ", SelectedSchemas));

    // ---- matrix -----------------------------------------------------------------------------------------------------

    /// <summary>Gets the matrix rows of the selected role and schemas.</summary>
    public ObservableCollection<PrivilegeRowViewModel> Rows { get; } = [];

    private IReadOnlyList<ObjectPrivileges> _privileges = [];

    /// <summary>Gets or sets whether inherited cells are drawn; when off they look empty and cannot be toggled.</summary>
    [ObservableProperty]
    public partial bool ShowInherited { get; set; } = true;

    partial void OnShowInheritedChanged(bool value) => RebuildRows();

    /// <summary>Gets or sets the message of the last failed read or apply, empty when none.</summary>
    [ObservableProperty]
    public partial string Error { get; set; } = "";

    /// <summary>Gets a value indicating whether a read is running.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    private void RebuildRows()
    {
        Rows.Clear();
        var role = SelectedRole?.Name ?? "";
        foreach (var p in _privileges)
            Rows.Add(new PrivilegeRowViewModel(p, _changes, role, ShowInherited));
    }

    private void ChangesChanged()
    {
        RebuildRows();
        OnPropertyChanged(nameof(ChangeCount));
        OnPropertyChanged(nameof(ChangesText));
        OnPropertyChanged(nameof(ScriptText));
        OnPropertyChanged(nameof(StatusText));
        ApplyCommand.NotifyCanExecuteChanged();
        DiscardChangesCommand.NotifyCanExecuteChanged();
        CopyScriptCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Toggles one cell of the matrix; the head calls it on a click or <c>Espace</c>.</summary>
    /// <param name="row">Index into <see cref="Rows"/>.</param>
    /// <param name="column">Index of the privilege, in <see cref="PrivilegeKind"/> order.</param>
    /// <returns>What happened, or <see langword="null"/> when the cell does not exist or no role is selected.</returns>
    public ToggleResult? ToggleCell(int row, int column)
    {
        if (SelectedRole is null || row < 0 || row >= Rows.Count || column < 0 || column >= Rows[row].Cells.Count)
            return null;
        var item = Rows[row];
        var kind = item.Cells[column].Kind;
        var result = _changes.Toggle(item.Privileges.Key, kind, item.Privileges.StateOf(kind)); // inherited stays inert, shown or not
        if (result != ToggleResult.InheritedCannotRevoke)
            ChangesChanged();
        return result;
    }

    /// <summary>Toggles a whole row; the head calls it on <c>Maj+Espace</c>.</summary>
    /// <param name="row">Index into <see cref="Rows"/>.</param>
    public void ToggleRow(int row)
    {
        if (SelectedRole is null || row < 0 || row >= Rows.Count)
            return;
        _changes.ToggleRow(Rows[row].Privileges.Key, Rows[row].Privileges.States);
        ChangesChanged();
    }

    // ---- pending changes and script ---------------------------------------------------------------------------------

    /// <summary>Gets the number of pending changes.</summary>
    public int ChangeCount => _changes.Count;

    /// <summary>Gets the counter of the script panel and the status bar, <c>2 changements</c>.</summary>
    public string ChangesText => UiText.Format(ChangeCount == 1 ? "security.changes.one" : "security.changes.other", ChangeCount);

    /// <summary>Gets the generated GRANT / REVOKE script; empty without pending changes.</summary>
    public string ScriptText => SelectedRole is { } r ? PostgresPrivilegeScript.Build(r.Name, _changes, _profile.Name).Text : "";

    /// <summary>Gets the status bar text.</summary>
    public string StatusText => ChangeCount > 0
        ? UiText.Format(ChangeCount == 1 ? "security.unapplied.one" : "security.unapplied.other", ChangeCount)
        : UiText.Format("security.connectedAs", _profile.UserName ?? "", _profile.Name, _profile.Database ?? "");

    /// <summary>Gets the server version for the status bar.</summary>
    public string ServerVersion => _connection.ServerVersion;

    private bool HasChanges() => ChangeCount > 0;

    /// <summary>Puts the generated script on the clipboard.</summary>
    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void CopyScript() => CopyRequested?.Invoke(ScriptText);

    /// <summary>Drops every pending change.</summary>
    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void DiscardChanges()
    {
        _changes.Clear();
        ChangesChanged();
    }

    // ---- loading ----------------------------------------------------------------------------------------------------

    /// <summary>Reads the roles and schemas, selects nothing yet and shows the default schema.</summary>
    /// <returns>A task finishing when the lists are filled.</returns>
    public async Task LoadAsync()
    {
        var (token, id) = NextRead();
        try
        {
            var roles = await _reader.ReadRolesAsync(token);
            var schemas = await _reader.ListSchemasAsync(token);
            if (id != _sequence)
                return;
            _roles = roles;
            AvailableSchemas = schemas;
            SelectedSchemas = schemas.Contains("public") ? ["public"] : [.. schemas.Take(1)];
            OnPropertyChanged(nameof(AvailableSchemas));
            OnPropertyChanged(nameof(SelectedSchemas));
            OnPropertyChanged(nameof(SchemaSelectorText));
            RebuildRoleLists();
            Error = "";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (id == _sequence)
                Error = ex.Message;
        }
        finally
        {
            Finish(id);
        }
    }

    /// <summary>Selects a role and loads its privileges; asks first when changes are pending.</summary>
    /// <param name="role">The role to show.</param>
    /// <returns><see langword="false"/> when the user kept the pending changes.</returns>
    public async Task<bool> SelectRoleAsync(RoleInfo? role)
    {
        if (!await ConfirmDiscardAsync())
            return false;
        _changes = new PrivilegeChangeSet(role?.Name ?? "");
        SelectedRole = role;
        OnPropertyChanged(nameof(RoleBadges));
        OnPropertyChanged(nameof(RoleSubtitleText));
        await ReloadPrivilegesAsync();
        return true;
    }

    /// <summary>Changes the schemas the matrix shows; asks first when changes are pending.</summary>
    /// <param name="schemas">The schemas to show.</param>
    /// <returns><see langword="false"/> when the user kept the pending changes.</returns>
    public async Task<bool> SetSchemasAsync(IReadOnlyList<string> schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);
        if (!await ConfirmDiscardAsync())
            return false;
        _changes = new PrivilegeChangeSet(SelectedRole?.Name ?? "");
        SelectedSchemas = schemas;
        OnPropertyChanged(nameof(SelectedSchemas));
        OnPropertyChanged(nameof(SchemaSelectorText));
        await ReloadPrivilegesAsync();
        return true;
    }

    private async ValueTask<bool> ConfirmDiscardAsync()
    {
        if (ChangeCount == 0)
            return true;
        return await _dialogs.ConfirmAsync(UiText.Get("security.pending.title"), UiText.Format("security.pending.message", ChangeCount));
    }

    private async Task ReloadPrivilegesAsync()
    {
        var (token, id) = NextRead();
        try
        {
            IReadOnlyList<ObjectPrivileges> privileges = [];
            if (SelectedRole is { } role && SelectedSchemas.Count > 0)
                privileges = await _reader.ReadPrivilegesAsync(role.Name, SelectedSchemas, token);
            if (id != _sequence)
                return; // a newer selection superseded this read
            _privileges = privileges;
            Error = "";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (id != _sequence)
                return;
            _privileges = [];
            Error = ex.Message;
        }
        finally
        {
            Finish(id);
        }

        if (id == _sequence)
            ChangesChanged();
    }

    private (CancellationToken Token, int Id) NextRead()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        IsLoading = true;
        return (_loadCts.Token, ++_sequence);
    }

    private void Finish(int id)
    {
        if (id == _sequence)
            IsLoading = false;
    }

    // ---- apply ------------------------------------------------------------------------------------------------------

    /// <summary>Runs the script one statement at a time in one scope, after the production confirmation on a production connection.</summary>
    /// <returns>A task finishing when the apply and the reload that follows are done.</returns>
    [RelayCommand(CanExecute = nameof(HasChanges))]
    private async Task ApplyAsync()
    {
        if (SelectedRole is not { } role || _changes.Count == 0)
            return;
        var script = PostgresPrivilegeScript.Build(role.Name, _changes, _profile.Name);
        if (_profile.Environment == ConnectionEnvironment.Production && !await ConfirmProductionAsync(script))
            return;
        StatementScope? scope = null;
        using var operation = _transaction?.BeginOperation();
        try
        {
            scope = await SqlRunner.BeginScopeAsync(_connection, _profile.Dialect, _transaction, Title, CancellationToken.None);
            foreach (var statement in script.Statements)
                scope.Observe(statement, await SqlRunner.ExecuteAsync(_connection, statement, [], CancellationToken.None));
            await scope.CompleteAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            if (scope is not null)
                await scope.FailAsync();
            Error = ex.Message; // the pending changes stay, so the user can fix the cause and retry
            await _dialogs.ShowErrorAsync(UiText.Get("security.apply.error"), ex.Message);
            return;
        }

        _changes.Clear();
        await ReloadPrivilegesAsync();
    }

    private async Task<bool> ConfirmProductionAsync(PrivilegeScript script)
    {
        var listed = script.Statements.Take(Listed).Select(s => new ConfirmedStatement(s, null, StatementKind.Other)).ToList();
        if (script.Statements.Count > Listed)
            listed.Add(new ConfirmedStatement(UiText.Format("prod.more", script.Statements.Count - Listed), null, StatementKind.Empty));
        var request = new ProductionConfirmationRequest(
            ConfirmationKind.SchemaChange, _profile.Name, _profile.Database ?? "", listed, null,
            ProductionGuard.RequiredText(_profile), false, false,
            UiText.Get("security.confirm.title"), UiText.Format("security.confirm.message", SelectedRole?.Name ?? ""), UiText.Get("security.confirm.button"));
        return (await _dialogs.ConfirmProductionAsync(request)).Confirmed;
    }

    /// <summary>Asks the workspace to close the tab.</summary>
    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this);

    /// <summary>Abandons a read in flight.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }
}
