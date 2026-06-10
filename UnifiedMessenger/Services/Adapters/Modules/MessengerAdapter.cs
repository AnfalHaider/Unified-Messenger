using Microsoft.Web.WebView2.Core;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Adapters.Modules;

public sealed class MessengerAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "messenger-adapter.js";

    public override string PlatformId => "messenger";

    protected override void RegisterNavigationHooks(CoreWebView2 coreWebView, MessengerInstance instance)
    {
        base.RegisterNavigationHooks(coreWebView, instance);

        coreWebView.HistoryChanged += (_, _) =>
        {
            _ = ExecutePublishBadgeAsync(coreWebView);
        };
    }
}
