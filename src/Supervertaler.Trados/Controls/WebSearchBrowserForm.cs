using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Supervertaler.Trados.Core;

namespace Supervertaler.Trados.Controls
{
    /// <summary>
    /// Embedded-mode web results: a resizable window with one WebView2 tab per
    /// enabled resource.
    ///
    /// <para><b>One window, reused.</b> A second search re-navigates the existing
    /// tabs rather than opening another window — which is the whole reason
    /// embedded mode exists, since browser mode cannot avoid piling up a new
    /// window per search. Tabs are only rebuilt when the enabled resource set
    /// actually changes; otherwise each view is simply pointed at a new URL,
    /// which also keeps scroll position and sign-in state per site.</para>
    ///
    /// <para>Still to come: lazy loading (tabs currently initialise eagerly),
    /// ad/tracker blocking, zoom, and a per-tab "open in browser" escape.</para>
    /// </summary>
    public class WebSearchBrowserForm : Form
    {
        private readonly TabControl _tabs;
        private readonly Label _status;

        private CoreWebView2Environment _environment;

        /// <summary>The resource ids currently laid out, in order — compared
        /// against the next search to decide rebuild vs re-navigate.</summary>
        private List<string> _layoutIds = new List<string>();

        public WebSearchBrowserForm()
        {
            Text = "SuperSearch";
            Size = new Size(1200, 850);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(600, 400);
            try { Icon = IconHelper.AppIcon; } catch { /* icon is cosmetic */ }

            _tabs = new TabControl { Dock = DockStyle.Fill };
            Controls.Add(_tabs);

            _status = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0),
                ForeColor = Color.FromArgb(110, 110, 110)
            };
            Controls.Add(_status);
        }

        /// <summary>
        /// Points the window at a new search. Creates the environment and tabs on
        /// first use, re-navigates them afterwards. Returns false only when the
        /// WebView2 environment could not be created at all, in which case the
        /// caller should fall back to the user's browser.
        /// </summary>
        public async Task<bool> ShowResultsAsync(string query, IList<WebSearchTarget> targets)
        {
            if (_environment == null)
            {
                _environment = await WebView2Support.CreateEnvironmentAsync();
                if (_environment == null) return false;
            }

            Text = $"SuperSearch — {query}";

            var ids = targets.Select(t => t.Resource.Id).ToList();
            if (!ids.SequenceEqual(_layoutIds, StringComparer.OrdinalIgnoreCase))
            {
                RebuildTabs(targets);
                _layoutIds = ids;
            }

            await NavigateTabsAsync(targets);

            _status.Text = $"{targets.Count} resource(s) · “{query}” · WebView2 {WebView2Support.RuntimeVersion}";
            if (_tabs.TabPages.Count > 0) _tabs.SelectedIndex = 0;
            return true;
        }

        private void RebuildTabs(IList<WebSearchTarget> targets)
        {
            foreach (TabPage page in _tabs.TabPages)
            {
                foreach (Control c in page.Controls) c.Dispose();
            }
            _tabs.TabPages.Clear();

            foreach (var target in targets)
            {
                var page = new TabPage(target.Resource.Name) { UseVisualStyleBackColor = true };
                page.Controls.Add(new WebView2 { Dock = DockStyle.Fill });
                _tabs.TabPages.Add(page);
            }
        }

        private async Task NavigateTabsAsync(IList<WebSearchTarget> targets)
        {
            for (int i = 0; i < _tabs.TabPages.Count && i < targets.Count; i++)
            {
                var view = _tabs.TabPages[i].Controls.OfType<WebView2>().FirstOrDefault();
                if (view == null) continue;

                try
                {
                    // Sharing one environment across every view means one cookie
                    // jar: signing in on one tab carries to the rest.
                    if (view.CoreWebView2 == null)
                        await view.EnsureCoreWebView2Async(_environment);

                    view.CoreWebView2.Navigate(targets[i].Url);
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Log("WebView2",
                        $"Tab '{targets[i].Resource.Name}' failed: {ex.GetType().Name}: {ex.Message}");
                    ShowTabError(_tabs.TabPages[i], targets[i], ex);
                }
            }
        }

        private static void ShowTabError(TabPage page, WebSearchTarget target, Exception ex)
        {
            page.Controls.Clear();
            var label = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = $"{target.Resource.Name} could not be loaded.\n\n{ex.Message}\n\n"
                     + "Right-click the Web button in SuperSearch to open results in your browser instead."
            };
            page.Controls.Add(label);
        }

        /// <summary>
        /// Brings the window up and in front — restoring it first if minimised.
        ///
        /// <para>Called <i>after</i> navigation rather than at Show() time, and
        /// this ordering is the fix for a real bug: WebView2 initialisation
        /// creates child windows in a separate browser process, and that process
        /// takes the foreground as it starts. On a cold start it lost the race
        /// and we stayed in front; on a warm start it won, and the window
        /// appeared behind Trados.</para>
        /// </summary>
        public void BringToFrontHard()
        {
            try
            {
                if (WindowState == FormWindowState.Minimized)
                    WindowState = FormWindowState.Normal;
                Show();
                EnsureOwnedByHost();
                Activate();
                ForegroundWindow.Force(Handle);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Log("WebView2", $"Could not raise the window: {ex.Message}");
            }
        }

        /// <summary>
        /// Makes Trados the owner of this window, once, as soon as it has a handle.
        ///
        /// <para>Forcing the foreground alone was not enough, and could not be:
        /// <c>Navigate()</c> returns as soon as the request is queued, so the page
        /// is still loading when we raise the window, and the WebView2 browser
        /// process takes the foreground whenever it finishes — after us. Rather
        /// than trying to win that race, ownership removes it: the window manager
        /// keeps an owned window above its owner regardless of who has focus, so
        /// it can no longer disappear behind Trados.</para>
        /// </summary>
        private void EnsureOwnedByHost()
        {
            if (_ownerSet || !IsHandleCreated) return;
            var host = ForegroundWindow.HostMainWindow();
            if (host == IntPtr.Zero) return;
            _ownerSet = ForegroundWindow.SetOwner(Handle, host);
        }

        private bool _ownerSet;

        /// <summary>
        /// Closing hides rather than disposes, so the next search reuses the
        /// warm environment and the signed-in sessions instead of paying the
        /// several-second WebView2 cold start again.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
