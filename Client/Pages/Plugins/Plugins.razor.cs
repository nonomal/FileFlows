namespace FileFlows.Client.Pages
{
    using System.Linq;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    using Radzen;
    using Radzen.Blazor;
    using FileFlows.Client.Components;
    using FileFlows.Shared.Helpers;
    using FileFlows.Shared;
    using FileFlows.Shared.Models;

    public partial class Plugins : ListPage<PluginInfoModel>
    {
        public override string ApiUrl => "/api/plugin";

        private PluginBrowser PluginBrowser { get; set; }

        protected override void OnInitialized()
        {
            _ = Load();
        }

        protected override string DeleteMessage => "Pages.Plugins.Messages.DeletePlugins";

        async Task Add()
        {
#if (!DEMO)
            bool result = await PluginBrowser.Open();
            if (result)
                await PluginsUpdated();
#endif
        }

        async Task Update()
        {
#if (!DEMO)
            var plugin = Table.GetSelected()?.FirstOrDefault();
            if (plugin == null)
                return;
            Blocker.Show();
            this.StateHasChanged();
            Data.Clear();
            try
            {
                var result = await HttpHelper.Post($"{ApiUrl}/update/{plugin.Uid}");
                if(result.Success)
                    await PluginsUpdated();
            }
            finally
            {
                Blocker.Hide();
                this.StateHasChanged();
            }
#endif
        }

        async Task PluginsUpdated()
        {
            await App.Instance.LoadLanguage();
            await this.Load();
        }

        private PluginInfo EditingPlugin = null;

        async Task<bool> SaveSettings(ExpandoObject model)
        {
#if (!DEMO)
            Blocker.Show();
            this.StateHasChanged();

            try
            {
                var pluginResult = await HttpHelper.Post<PluginInfo>($"{ApiUrl}/{EditingPlugin.Uid}/settings", model);
                if (pluginResult.Success == false)
                {
                    NotificationService.Notify(NotificationSeverity.Error, Translater.Instant("ErrorMessages.SaveFailed"));
                    return false;
                }
                return true;
            }
            finally
            {
                Blocker.Hide();
                this.StateHasChanged();
            }
#else
            return true;
#endif
        }

        public override async Task<bool> Edit(PluginInfoModel plugin)
        {
#if (!DEMO)
            if (plugin?.HasSettings != true)
                return false;
            Blocker.Show();
            this.StateHasChanged();
            Data.Clear();

            try
            {
                var pluginResult = await HttpHelper.Get<PluginInfo>($"{ApiUrl}/{plugin.Uid}");
                if (pluginResult.Success == false)
                    return false;
                plugin.Settings = pluginResult.Data.Settings;
                plugin.Fields = pluginResult.Data.Fields;
            }
            finally
            {
                Blocker.Hide();
                this.StateHasChanged();
            }
            this.EditingPlugin = plugin;
            var result = await Editor.Open("Plugins." + plugin.Name, plugin.Name, plugin.Fields, plugin.Settings,
                saveCallback: SaveSettings);
            return false; // we dont need to reload the list
#else
            return false;
#endif
        }
    }

}