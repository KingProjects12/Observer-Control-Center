using System.Windows.Controls;

namespace Observer.ControlCenter;

public partial class MainWindow
{
    // Temporary actions are separate from the displayed report page.
    // Independent scopes handle a menu closing while a nested dialog remains open.
    private readonly List<NavigationAction> _navigationActions = new();

    internal IDisposable BeginNavigationAction(Button button)
    {
        var scope = new NavigationAction(this, button);
        _navigationActions.Add(scope);
        RefreshNavigationSelection();
        return scope;
    }

    private void RefreshNavigationSelection()
    {
        Button selected = _navigationActions.Count > 0 ? _navigationActions[^1].Button :
            _page switch { "traffic" => NavTraffic, "connections" => NavConnections, _ => NavOverview };
        foreach (Button item in new[] { NavOverview, NavAccess, NavConnections, NavTraffic, NavNat,
            NavVpn, NavCollectTraffic, NavReports, NavImport, NavTools, NavHelp })
        {
            string? value = ReferenceEquals(item, selected) ? "selected" : null;
            if (!Equals(item.Tag, value)) item.Tag = value;
        }
    }

    private sealed class NavigationAction(MainWindow owner, Button button) : IDisposable
    {
        internal Button Button { get; } = button;
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner._navigationActions.Remove(this);
            owner.RefreshNavigationSelection();
        }
    }
}
