using Microsoft.Playwright;
using SubtitlesStreamer.Domain.Constants;

namespace SubtitlesStreamer.Application.Services.PlaywrightService;

public class PlaywrightService : IPlaywrightService
{
    private readonly IPage _page;
    
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
        var popup = await CreatePopupTranslateAsync();
        await popup.EvaluateAsync("""
                                      (args) => {
                                          if (window.__subtitleRenderer) {
                                              window.__subtitleRenderer.show(args.text, args.duration);
                                          } else if (window.__myPopup && window.__myPopup.__subtitleRenderer) {
                                              // fallback to popup context
                                              window.__myPopup.__subtitleRenderer.show(args.text, args.duration);
                                          }
                                      }
                                  """, new { text = translatedResult, duration = 1500 });    
    }
    
    private async Task<IPage> CreatePopupTranslateAsync()
    {
        var popup = await _page.RunAndWaitForPopupAsync(async () =>
        {
            // Open the popup and immediately run JS inside the main page context
            await _page.EvaluateAsync("""
                                         () => {
                                             if (!window.__myPopup || window.__myPopup.closed) {
                                                 window.__myPopup = window.open('', 'myReusablePopup', 'width=600,height=200');

                                                 // Wait for the popup document to be available
                                                 const doc = window.__myPopup.document;

                                                 // Create container
                                                 let container = doc.getElementById('text');
                                                 if (!container) {
                                                     container = doc.createElement('div');
                                                     container.id = 'text';
                                                     container.style.fontSize = '22px';
                                                     container.style.textAlign = 'center';
                                                     container.style.marginTop = '40px';
                                                     container.style.transition = 'opacity 0.4s';
                                                     container.style.opacity = '0';
                                                     doc.body.appendChild(container);
                                                 }

                                                 // Define subtitle renderer inside the popup context
                                                 window.__myPopup.__subtitleRenderer = {
                                                     show(text, duration = 3000) {
                                                         container.textContent = text;
                                                         container.style.opacity = '1';
                                                         setTimeout(() => {
                                                             container.style.opacity = '0';
                                                             setTimeout(() => container.textContent = '', 400);
                                                         }, duration);
                                                     }
                                                 };
                                             }
                                         }
                                     """);
        });

        await popup.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        return popup;
    }
}