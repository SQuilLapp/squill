using Squill.Core;
using Squill.Core.Transactions;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;
using Squill.Sql.Transactions;

namespace Squill.Presentation.Safety;

/// <summary>Builds the localized production confirmation of a run or of a commit.</summary>
internal static class ProductionRequestBuilder
{
    private const int Listed = 5;

    /// <summary>Builds the confirmation of a run.</summary>
    internal static ProductionConfirmationRequest ForRun(ConnectionProfile profile, IReadOnlyList<string> statements, ProductionRequirement requirement, TransactionMode mode, long? estimate)
    {
        var traits = requirement.Traits;
        var single = statements.Count == 1 ? traits[0] : null;
        var figure = statements.Count == 1 ? estimate : null;
        string title, message;
        if (single is { Kind: StatementKind.Delete, NoWhere: true })
        {
            title = UiText.Format("prod.title.deleteAll", single.Target ?? UiText.Get("prod.target.unknown"));
            message = UiText.Get("prod.message.deleteAll");
        }
        else if (single is { Kind: StatementKind.Update, NoWhere: true })
        {
            title = UiText.Format("prod.title.modifyAll", single.Target ?? UiText.Get("prod.target.unknown"));
            message = UiText.Get("prod.message.modifyAll");
        }
        else if (statements.Count == 1)
        {
            title = UiText.Get("prod.title.runOne");
            message = UiText.Get("prod.message.run");
        }
        else
        {
            title = UiText.Format("prod.title.runMany", statements.Count);
            message = UiText.Get("prod.message.run");
        }

        var label = figure is { } rows && single is { Kind: StatementKind.Delete or StatementKind.Update }
            ? UiText.FormatPlural(single.Kind == StatementKind.Delete ? "prod.confirm.delete" : "prod.confirm.modify", Clamp(rows))
            : UiText.Get("prod.confirm.run");
        if (figure is { } known)
            message += " " + UiText.FormatPlural("prod.message.figure", Clamp(known));
        if (mode == TransactionMode.Manual)
            message += " " + UiText.Get("prod.message.manual");

        var listed = statements.Take(Listed).Select((s, i) => new ConfirmedStatement(s, traits[i].Target, traits[i].Kind)).ToList();
        if (statements.Count > Listed)
            listed.Add(new ConfirmedStatement(UiText.Format("prod.more", statements.Count - Listed), null, StatementKind.Empty));
        var option = traits.All(t => !t.ImplicitCommit && !t.CannotRunInTransaction);
        return new ProductionConfirmationRequest(
            ConfirmationKind.Run, profile.Name, DatabaseName(profile), listed, figure,
            requirement.RequiresTyping ? ProductionGuard.RequiredText(profile) : null,
            option, option && mode == TransactionMode.Manual, title, message, label);
    }

    /// <summary>Builds the confirmation of a <c>Valider</c>.</summary>
    internal static ProductionConfirmationRequest ForCommit(ConnectionProfile profile, IReadOnlyList<PendingStatement> journal, int pendingCount)
    {
        var listed = journal.Take(Listed).Select(p => new ConfirmedStatement(p.Text, null, StatementKind.Empty)).ToList();
        if (journal.Count > Listed)
            listed.Add(new ConfirmedStatement(UiText.Format("prod.more", journal.Count - Listed), null, StatementKind.Empty));
        return new ProductionConfirmationRequest(
            ConfirmationKind.Commit, profile.Name, DatabaseName(profile), listed, null, null, false, false,
            UiText.Get("prod.title.commit"),
            UiText.FormatPlural("prod.message.commit", pendingCount),
            UiText.Get("prod.confirm.commit"));
    }

    private static string DatabaseName(ConnectionProfile profile) =>
        profile.Dialect == DatabaseDialect.Sqlite ? ProductionGuard.RequiredText(profile) : profile.Database ?? "";

    private static int Clamp(long rows) => (int)Math.Min(rows, int.MaxValue);
}
