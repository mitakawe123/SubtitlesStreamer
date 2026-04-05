using Microsoft.Playwright;
using SubtitlesStreamer.Domain.Constants;

namespace SubtitlesStreamer.Application.Services.PlaywrightService;

public class PlaywrightService : IPlaywrightService
{
    private readonly IBrowserContext _context;
    private IPage _page;
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

        _context = browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true,
            ViewportSize = null
        }).Result;

        _page = _context.NewPageAsync().Result;
        _page.Close += OnPageClosed;
    }

    public async Task OpenSiteAsync(string url)
    {
        await EnsurePageAliveAsync();
        await _page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });
    }

    public async Task ClickConsentButtonAsync()
    {
        await EnsurePageAliveAsync();
        var consentButton = _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions
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

    private async Task EnsurePageAliveAsync()
    {
        if (_page.IsClosed)
        {
            _popupPage = null; // popup is also gone
            _page = await _context.NewPageAsync();
            _page.Close += OnPageClosed;
        }
    }

    private void OnPageClosed(object? sender, IPage e)
    {
        _popupPage = null;
    }

    private async Task<IPage> GetOrCreatePopupAsync()
    {
        if (_popupPage is { IsClosed: false })
            return _popupPage;

        _popupPage = await _page.RunAndWaitForPopupAsync(async () =>
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