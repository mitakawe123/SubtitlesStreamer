using Microsoft.Playwright;
using SubtitlesStreamer.Domain.Constants;

namespace SubtitlesStreamer.Application.Services.PlaywrightService;

public class PlaywrightService : IPlaywrightService
{
    private readonly IPage _page;
    private IPage? _popupPage = null;
    
    public PlaywrightService()
    {
        var playwright = Playwright.CreateAsync().Result;
        var browser = playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions
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
            }).Result;

        var context = browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true,
            ViewportSize = null
        }).Result;
        
        _page = context.NewPageAsync().Result;
    }
    
    public async Task OpenSiteAsync(string url)
    {
        await _page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });
    }

    public async Task ClickConsentButtonAsync()
    {
        var consentButton = _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions {
            NameRegex = Regexes.ConsentButton
        });

        await consentButton.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 15000 // handles slow GDPR dialogs
        });

        await consentButton.ClickAsync();
        await _page.WaitForTimeoutAsync(2_000);    
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
        """, new { text = translatedResult, duration = 1500 });
    }

    private async Task<IPage> GetOrCreatePopupAsync()
    {
        if (_popupPage is { IsClosed: false })
            return _popupPage;

        // Step 1: just open the popup
        _popupPage = await _page.RunAndWaitForPopupAsync(async () =>
        {
            await _page.EvaluateAsync("""
                () => {
                    window.open('', 'myReusablePopup', 'width=600,height=200');
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

                // Define renderer on THIS window (popup window)
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