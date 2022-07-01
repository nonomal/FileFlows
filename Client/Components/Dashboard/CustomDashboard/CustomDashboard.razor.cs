using FileFlows.Client.Components.Dialogs;
using FileFlows.Plugin;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FileFlows.Client.Components.Dashboard;

/// <summary>
/// Custom dashboard allows the user to rearrange and customize the dashboard
/// </summary>
public partial class CustomDashboard : IDisposable
{
    const string ApiUrl = "/api/worker";
    [Inject] public IJSRuntime jSRuntime { get; set; }
    [CascadingParameter] public Blocker Blocker { get; set; }
    [CascadingParameter] Editor Editor { get; set; }
    [CascadingParameter] private Pages.Dashboard Dashboard { get; set; }

    private Guid _ActiveDashboardUid;

    [Parameter]
    public Guid ActiveDashboardUid
    {
        get => _ActiveDashboardUid;
        set
        {
            if (_ActiveDashboardUid == value)
                return;
            _ActiveDashboardUid = value;
            _ = this.LoadDashboard();
        }
    }

    public IJSObjectReference jsFunctions;

    private readonly List<PortletUiModel> Portlets = new List<PortletUiModel>();
    private IJSObjectReference jsCharts;
    
    protected override async Task OnInitializedAsync()
    {
        jsCharts = await jSRuntime.InvokeAsync<IJSObjectReference>("import", $"./scripts/Charts/FFChart.js");
    }
    
    private async Task LoadDashboard()
    {
        var portletsResponse = await HttpHelper.Get<List<PortletUiModel>>("/api/dashboard/" + ActiveDashboardUid + "/portlets");
        if (portletsResponse.Success == false)
            return;
        this.Portlets.Clear();
        if(portletsResponse.Data?.Any() == true)
            this.Portlets.AddRange(portletsResponse.Data);
        var dotNetObjRef = DotNetObjectReference.Create(this);
        await jsCharts.InvokeVoidAsync($"initDashboard",  this.Portlets, dotNetObjRef); 
    }
    
    public void Dispose()
    {
        _ = jsCharts?.InvokeVoidAsync("disposeAll");
    }
    
    
    /// <summary>
    /// Opens the full library file editor for a completed library file
    /// </summary>
    /// <param name="libraryFileUid">The UID of the library file</param>
    [JSInvokable]
    public async Task OpenFileViewer(Guid libraryFileUid)
    {
        await Helpers.LibraryFileEditor.Open(Blocker, Editor, libraryFileUid);
    }
    
    /// <summary>
    /// Opens a log for a executing library file
    /// </summary>
    /// <param name="libraryFileUid">The UID of the library file</param>
    /// <param name="libraryFileName">the filename of the library file</param>
    [JSInvokable]
    public async Task OpenLog(Guid libraryFileUid, string libraryFileName)
    {
        Blocker.Show();
        string log = string.Empty;
        string url = $"{ApiUrl}/{libraryFileUid}/log?lineCount=200";
        try
        {
            var logResult = await GetLog(url);
            if (logResult.Success == false || string.IsNullOrEmpty(logResult.Data))
            {
                Toast.ShowError( Translater.Instant("Pages.Dashboard.ErrorMessages.LogFailed"));
                return;
            }
            log = logResult.Data;
        }
        finally
        {
            Blocker.Hide();
        }

        List<ElementField> fields = new List<ElementField>();
        fields.Add(new ElementField
        {
            InputType = FormInputType.LogView,
            Name = "Log",
            Parameters = new Dictionary<string, object> {
                { nameof(Components.Inputs.InputLogView.RefreshUrl), url },
                { nameof(Components.Inputs.InputLogView.RefreshSeconds), 3 },
            }
        });

        await Editor.Open("Pages.Dashboard", libraryFileName, fields, new { Log = log }, large: true, readOnly: true);
    }

    private async Task<RequestResult<string>> GetLog(string url)
    {
#if (DEMO)
        return new RequestResult<string>
        {
            Success = true,
            Data = @"2021-11-27 11:46:15.0658 - Debug -> Executing part:
2021-11-27 11:46:15.1414 - Debug -> node: VideoFile
2021-11-27 11:46:15.8442 - Info -> Video Information:
ffmpeg version 4.1.8 Copyright (c) 2000-2021 the FFmpeg developers
built with gcc 9 (Ubuntu 9.3.0-17ubuntu1~20.04)
configuration: --disable-debug --disable-doc --disable-ffplay --enable-shared --enable-avresample --enable-libopencore-amrnb --enable-libopencore-amrwb --enable-gpl --enable-libass --enable-fontconfig --enable-libfreetype --enable-libvidstab --enable-libmp3lame --enable-libopus --enable-libtheora --enable-libvorbis --enable-libvpx --enable-libwebp --enable-libxcb --enable-libx265 --enable-libxvid --enable-libx264 --enable-nonfree --enable-openssl --enable-libfdk_aac --enable-postproc --enable-small --enable-version3 --enable-libbluray --enable-libzmq --extra-libs=-ldl --prefix=/opt/ffmpeg --enable-libopenjpeg --enable-libkvazaar --enable-libaom --extra-libs=-lpthread --enable-libsrt --enable-nvenc --enable-cuda --enable-cuvid --enable-libnpp --extra-cflags='-I/opt/ffmpeg/include -I/opt/ffmpeg/include/ffnvcodec -I/usr/local/cuda/include/' --extra-ldflags='-L/opt/ffmpeg/lib -L/usr/local/cuda/lib64 -L/usr/local/cuda/lib32/'
libavutil      56. 22.100 / 56. 22.100
libavcodec     58. 35.100 / 58. 35.100
libavformat    58. 20.100 / 58. 20.100
libavdevice    58.  5.100 / 58.  5.100
libavfilter     7. 40.101 /  7. 40.101
libavresample   4.  0.  0 /  4.  0.  0
libswscale      5.  3.100 /  5.  3.100
libswresample   3.  3.100 /  3.  3.100
libpostproc    55.  3.100 / 55.  3.100"
        };
#endif
        return await HttpHelper.Get<string>(url);
    }

    /// <summary>
    /// Cancels an runner
    /// </summary>
    /// <param name="runnerUid">The UID of the runner</param>
    /// <param name="libraryFileUid">The UID of the library file</param>
    /// <param name="name">the filename for the confirm prompt</param>
    [JSInvokable]
    public async Task CancelRunner(Guid runnerUid, Guid libraryFileUid, string name)
    {

        if (await Confirm.Show("Labels.Cancel",
                Translater.Instant("Pages.Dashboard.Messages.CancelMessage", new {RelativeFile = name})) == false)
            return;
        
        Blocker.Show();
        try
        {
            await HttpHelper.Delete($"{ApiUrl}/{runnerUid}?libraryFileUid={libraryFileUid}");
            await Task.Delay(1000);
        }
        finally
        {
            Blocker.Hide();
        }
    }
}