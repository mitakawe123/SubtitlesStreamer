using Microsoft.Playwright;
using SubtitlesStreamer.Domain.Constants;

namespace SubtitlesStreamer.Application.Services.PlaywrightService;

public class PlaywrightService : IPlaywrightService
{
    private IPage? _page;
    private bool _initialized;
    private bool _overlayInjected;

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

        _initialized = true;
    }

    public async Task OpenSiteAsync(string url)
    {
        await _page!.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });
        await ClickConsentButtonAsync();
        await InjectOverlayAsync();
    }

    public async Task UpdateLiveTextAsync(string text)
    {
        await EnsureOverlayAsync();
        await _page!.EvaluateAsync("(text) => window.__subtitleRenderer?.live(text)", text);
    }

    public async Task CommitTextAsync(string text, int duration)
    {
        await EnsureOverlayAsync();
        await _page!.EvaluateAsync(
            "([text, duration]) => window.__subtitleRenderer?.commit(text, duration)",
            new object[] { text, duration });
    }

    private async Task EnsureOverlayAsync()
    {
        if (!_overlayInjected)
            await InjectOverlayAsync();
    }

    private async Task InjectOverlayAsync()
    {
        await _page!.EvaluateAsync("""
            () => {
                const existing = document.getElementById('subtitle-overlay');
                if (existing) existing.remove();

                const overlay = document.createElement('div');
                overlay.id = 'subtitle-overlay';
                overlay.style.cssText = `
                    position: fixed;
                    bottom: 60px;
                    left: 50%;
                    transform: translateX(-50%);
                    background: rgba(0, 0, 0, 0.75);
                    color: #fff;
                    font-size: 26px;
                    font-family: Arial, sans-serif;
                    font-weight: 500;
                    padding: 10px 24px;
                    border-radius: 8px;
                    z-index: 2147483647;
                    max-width: 80%;
                    text-align: center;
                    pointer-events: none;
                    opacity: 1;
                    white-space: pre-wrap;
                    text-shadow: 1px 1px 2px rgba(0,0,0,0.8);
                `;
                document.body.appendChild(overlay);

                let fadeTimer = null;
                let clearTimer = null;

                function cancelPendingTimers() {
                    if (fadeTimer !== null)  { clearTimeout(fadeTimer);  fadeTimer = null; }
                    if (clearTimer !== null) { clearTimeout(clearTimer); clearTimer = null; }
                }

                function showInstant(text) {
                    cancelPendingTimers();
                    overlay.style.transition = 'none';
                    overlay.style.opacity = '1';
                    overlay.textContent = text;
                }

                window.__subtitleRenderer = {
                    live(text) {
                        showInstant(text);
                    },

                    commit(text, duration) {
                        showInstant(text);

                        fadeTimer = setTimeout(() => {
                            fadeTimer = null;
                            overlay.style.transition = 'opacity 1.5s ease-in';
                            overlay.style.opacity = '0';

                            clearTimer = setTimeout(() => {
                                clearTimer = null;
                                overlay.textContent = '';
                                overlay.style.transition = 'none';
                                overlay.style.opacity = '1';
                            }, 1500);
                        }, duration);
                    }
                };
            }
            """);

        _overlayInjected = true;
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
}