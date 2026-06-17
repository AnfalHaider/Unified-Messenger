using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class CommandCenterPanel : UserControl
{
    private ApplicationServices? _services;

    public CommandCenterPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ApplicationServiceProvider.IsInitialized)
        {
            _services = ApplicationServiceProvider.Current;
        }

        Render();
    }

    public void Render()
    {
        if (_services is null)
        {
            return;
        }

        var grouping = GroupToggle.IsOn ? OversightGrouping.ByLocation : OversightGrouping.ByInstance;
        var instances = _services.Registry.Instances.Where(instance => instance.IsProfessional).ToList();
        var snapshot = _services.Oversight.BuildSnapshot(grouping, instances);

        SubtitleText.Text = grouping == OversightGrouping.ByLocation
            ? "Rolled up by location"
            : "Per account · group into locations in Workspace management (Ctrl+K)";

        if (snapshot.TotalUrgent > 0 || snapshot.TotalDropped > 0)
        {
            AttentionText.Text = snapshot.AttentionSummary;
            AttentionBanner.Visibility = Visibility.Visible;
        }
        else
        {
            AttentionBanner.Visibility = Visibility.Collapsed;
        }

        CardsHost.Children.Clear();
        if (snapshot.Entities.Count == 0)
        {
            CardsHost.Children.Add(new TextBlock
            {
                Text = "No professional accounts yet — add one to see oversight here.",
                Foreground = Brush("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            return;
        }

        if (grouping == OversightGrouping.ByLocation)
        {
            var instanceSnapshot = _services.Oversight.BuildSnapshot(OversightGrouping.ByInstance, instances);
            var byInstanceId = instanceSnapshot.Entities
                .GroupBy(entity => entity.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var location in snapshot.Entities)
            {
                var members = location.MemberInstanceIds
                    .Where(byInstanceId.ContainsKey)
                    .Select(id => byInstanceId[id])
                    .ToList();
                CardsHost.Children.Add(BuildExpander(location, members));
            }
        }
        else
        {
            foreach (var entity in snapshot.Entities)
            {
                CardsHost.Children.Add(BuildRow(entity));
            }
        }
    }

    private Expander BuildExpander(OversightEntityHealth location, IReadOnlyList<OversightEntityHealth> members)
    {
        var content = new StackPanel { Spacing = 6, Padding = new Thickness(8, 4, 4, 4) };
        if (members.Count == 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = "No accounts in this location.",
                FontSize = 12,
                Foreground = Brush("TextFillColorSecondaryBrush")
            });
        }

        foreach (var member in members)
        {
            content.Children.Add(BuildRow(member));
        }

        return new Expander
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Header = BuildRowContent(location),
            Content = content
        };
    }

    private Border BuildRow(OversightEntityHealth entity)
    {
        var clickable = entity.Kind == OversightEntityKind.Instance;
        var border = new Border
        {
            Background = Brush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Brush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(0.5),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 11, 14, 11),
            Child = BuildRowContent(entity, clickable)
        };

        if (clickable)
        {
            border.Tapped += (_, _) => _services?.Navigation.OpenInstance(entity.Key);
        }

        return border;
    }

    private StackPanel BuildRowContent(OversightEntityHealth entity, bool clickable = false)
    {
        var statusBrush = entity.OnTimePercent >= 90
            ? Brush("SystemFillColorSuccessBrush")
            : entity.OnTimePercent >= 70
                ? Brush("SystemFillColorCautionBrush")
                : Brush("SystemFillColorCriticalBrush");
        var secondary = Brush("TextFillColorSecondaryBrush");
        var danger = Brush("SystemFillColorCriticalBrush");

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            VerticalAlignment = VerticalAlignment.Center
        };

        row.Children.Add(new Ellipse
        {
            Width = 9,
            Height = 9,
            Fill = entity.IsStale ? danger : Brush("SystemFillColorSuccessBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });

        row.Children.Add(new TextBlock
        {
            Text = entity.Kind == OversightEntityKind.Location
                ? $"{entity.DisplayName}  ({entity.AccountCount})"
                : entity.DisplayName,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 170,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        row.Children.Add(new TextBlock
        {
            Text = $"{entity.OnTimePercent}% on time",
            Foreground = statusBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 110
        });

        row.Children.Add(new TextBlock
        {
            Text = $"{entity.UrgentCount} urgent",
            Foreground = entity.UrgentCount > 0 ? danger : secondary,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 80
        });

        row.Children.Add(new TextBlock
        {
            Text = $"{entity.DroppedCount} dropped",
            Foreground = entity.DroppedCount > 0 ? danger : secondary,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 90
        });

        row.Children.Add(new TextBlock
        {
            Text = entity.IsStale ? "stale — reconnect" : "synced",
            FontSize = 12,
            Foreground = secondary,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (clickable)
        {
            row.Children.Add(new FontIcon
            {
                Glyph = "",
                FontSize = 13,
                Foreground = secondary,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        return row;
    }

    private static Brush Brush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);

    private void OnGroupToggled(object sender, RoutedEventArgs e) => Render();

    private void OnRefresh(object sender, RoutedEventArgs e) => Render();
}
