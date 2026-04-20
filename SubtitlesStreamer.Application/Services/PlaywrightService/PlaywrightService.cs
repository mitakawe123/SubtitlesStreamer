using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using SubtitlesStreamer.Domain.Constants;

namespace SubtitlesStreamer.Application.Services.PlaywrightService;

public class PlaywrightService : IPlaywrightService
{
    private IPage? _page;
    private IPage? _popupPage;
    private bool _initialized = false;
    
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            ExecutablePath = "/usr/bin/brave-browser",
            Headless = false,
            Args =
            [
                "--start-fullscreen",
                "--start-maximized",
                "--autoplay-policy=no-user-gesture-required",
                "--disable-features=PreloadMediaEngagementData,AutoplayIgnoreWebAudio,MediaEngagementBypassAutoplayPolicies"
            ]
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true,
            ViewportSize = null
        });

        _page = await context.NewPageAsync()
                ?? throw new InvalidOperationException("Cannot create a playwright page");
        _page.Close += OnPageClosed;
        _initialized = true;
    }
    
    public async Task OpenSiteAsync(string url)
    {
        await _page!.GotoAsync(url, new PageGotoOptions 
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });
        await ClickConsentButtonAsync();
    }

    public async Task ShowTranslatePopupTextAsync(string translatedResult)
    {
        var popup = await GetOrCreatePopupAsync();

        await popup.EvaluateAsync("""
            (args) => {
                if (window.__subtitleRenderer) {
                    window.__subtitleRenderer.show(args.text, args.duration);
                }
            }
        """, new { text = translatedResult, duration = 3000 });
    }
    
    private async Task ClickConsentButtonAsync()
    {
        var consentButton = _page!.GetByRole(AriaRole.Button, new PageGetByRoleOptions
        {
            NameRegex = Regexes.ConsentButton
        });

        await consentButton.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 15000
        });

        await consentButton.ClickAsync();
        await _page.WaitForTimeoutAsync(2_000);
    }

    private void OnPageClosed(object? sender, IPage e)
    {
        _popupPage = null;
    }

    private async Task<IPage> GetOrCreatePopupAsync()
    {
        if (_popupPage is { IsClosed: false })
            return _popupPage;

        _popupPage = await _page!.RunAndWaitForPopupAsync(async () =>
        {
            await _page.EvaluateAsync("""
                () => {
                    window.open('', 'myReusablePopup', 'width=800,height=400');
                }
            """);
        });

        await _popupPage.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        await _popupPage.EvaluateAsync("""
            () => {
                document.body.style.backgroundColor = '#000';
                document.body.style.margin = '0';

                let container = document.getElementById('text');
                if (!container) {
                    container = document.createElement('div');
                    container.id = 'text';
                    container.style.fontSize = '22px';
                    container.style.color = '#fff';
                    container.style.textAlign = 'center';
                    container.style.marginTop = '40px';
                    container.style.transition = 'opacity 0.4s';
                    container.style.opacity = '0';
                    document.body.appendChild(container);
                }

                window.__subtitleRenderer = {
                    show(text, duration = 3000) {
                        container.textContent = text;
                        container.style.opacity = '1';
                        clearTimeout(window.__subtitleTimer);
                        window.__subtitleTimer = setTimeout(() => {
                            container.style.opacity = '0';
                            setTimeout(() => container.textContent = '', 400);
                        }, duration);
                    }
                };
            }
        """);

        _popupPage.Close += (_, _) => _popupPage = null;

        return _popupPage;
    }
}