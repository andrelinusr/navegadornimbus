using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Drawing.Imaging; // para ImageFormat (usado na conversão Icon->PNG)

namespace Nimbus
{
    public partial class MainWindow : Window
    {
        // HttpClient singleton (reutilizar em toda a app)
        private readonly HttpClient _httpClient = new HttpClient();

        // Campo para armazenar a referência do TextBox de pesquisa
        private TextBox _urlTextBox;

        public MainWindow()
        {
            InitializeComponent();
            tabControl.Loaded += TabControl_Loaded;

            // configura User-Agent padrão do HttpClient
            try
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT) Nimbus/1.0");
            }
            catch { /* ignorar se falhar */ }

            // Ajusta a aba inicial para ter o mesmo header/behavior das abas dinâmicas
            SetupInitialTabWrapper();
        }

        // Wrapper para chamar o async sem deixar o construtor async
        private void SetupInitialTabWrapper() => _ = SetupInitialTabAsync();

        // Ajusta a aba inicial criada no XAML (InitialTab) para ter header e handlers similares às abas dinâmicas
        private async Task SetupInitialTabAsync()
        {
            try
            {
                // 'InitialTab' é o nome do TabItem definido no seu XAML (verifique se bate)
                if (this.FindName("InitialTab") is TabItem initialTab &&
                    initialTab.Content is WebView2 initialBrowser)
                {
                    // Cria o mesmo header usado nas abas dinâmicas
                    var iconImage = new System.Windows.Controls.Image
                    {
                        Width = 16,
                        Height = 16,
                        Margin = new Thickness(2, 0, 0, 0),
                        Source = new BitmapImage(new Uri("pack://application:,,,/Assets/POV_1_.ico"))
                    };

                    var titleBlock = new TextBlock
                    {
                        Text = "Nova Aba",
                        Margin = new Thickness(5, 0, 0, 0),
                        FontSize = 12,
                        Width = 120,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var closeButton = new Button
                    {
                        Content = "X",
                        Width = 20,
                        Height = 20,
                        Margin = new Thickness(5, 0, 0, 0)
                    };
                    closeButton.Click += CloseTabButton_Click;

                    var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    headerPanel.Children.Add(iconImage);
                    headerPanel.Children.Add(titleBlock);
                    headerPanel.Children.Add(closeButton);

                    // Aplica o header novo na aba inicial (substitui o header do XAML)
                    initialTab.Header = headerPanel;
                    initialTab.Margin = new Thickness(0); // garante margem igual às abas criadas

                    // Guarda referência do Image no Tag do WebView2 (igual comportamento das abas dinâmicas)
                    initialBrowser.Tag = iconImage;

                    // Assina NavigationCompleted (usa o mesmo handler que as abas dinâmicas)
                    initialBrowser.NavigationCompleted -= Browser_NavigationCompleted;
                    initialBrowser.NavigationCompleted += Browser_NavigationCompleted;

                    // Inicializa o CoreWebView2 e registra o fallback GetFaviconAsync (como nas abas dinâmicas)
                    try
                    {
                        await initialBrowser.EnsureCoreWebView2Async();

                        if (initialBrowser.CoreWebView2 != null)
                        {
                            // adiciona o handler de fallback para GetFaviconAsync (não remove handlers anteriores)
                            initialBrowser.CoreWebView2.FaviconChanged += async (s, e) =>
                            {
                                try
                                {
                                    using (var stream = await initialBrowser.CoreWebView2.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png))
                                    {
                                        if (stream != null)
                                        {
                                            var bitmap = new BitmapImage();
                                            bitmap.BeginInit();
                                            bitmap.StreamSource = stream;
                                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                            bitmap.EndInit();
                                            bitmap.Freeze();

                                            Dispatcher.Invoke(() => iconImage.Source = bitmap);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine("InitialTab GetFaviconAsync failed: " + ex.Message);
                                }
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("SetupInitialTab EnsureCoreWebView2Async failed: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SetupInitialTabAsync error: " + ex.Message);
            }
        }

        private void TabControl_Loaded(object sender, RoutedEventArgs e)
        {
            _urlTextBox = FindUrlTextBox(tabControl);
            if (_urlTextBox == null)
            {
                MessageBox.Show("UrlTextBox não foi encontrado!");
            }
        }

        // Método auxiliar para encontrar o TextBox com Name "UrlTextBox" na árvore visual
        private TextBox FindUrlTextBox(DependencyObject parent)
        {
            if (parent == null)
                return null;

            if (parent is TextBox tb && tb.Name == "UrlTextBox")
                return tb;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = FindUrlTextBox(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        // Evento para fechar a janela
        private void Window_Closed(object sender, EventArgs e)
        {
            // Limpeza, se necessário
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = GetCurrentBrowser();
            if (browser != null && browser.CanGoBack)
                browser.GoBack();
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = GetCurrentBrowser();
            if (browser != null && browser.CanGoForward)
                browser.GoForward();
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentBrowser()?.Reload();
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = GetCurrentBrowser();
            if (browser != null)
            {
                browser.Source = new Uri("https://nimbus-navegadorpaginainicial-4bcada32.base44.app/");
            }
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = GetCurrentBrowser();
            if (browser != null && _urlTextBox != null)
            {
                string inputUrl = _urlTextBox.Text.Trim();
                // Se a URL não contém http:// ou https://, adiciona https://
                if (!inputUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !inputUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    inputUrl = "https://" + inputUrl;
                }
                if (Uri.TryCreate(inputUrl, UriKind.Absolute, out Uri uri))
                {
                    browser.Source = uri;
                }
                else
                {
                    MessageBox.Show("URL inválida!");
                }
            }
        }

        private void UrlTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                GoButton_Click(this, new RoutedEventArgs());
        }

        private void CloseTabButton_Click(object sender, RoutedEventArgs e)
        {
            var headerPanel = (sender as Button)?.Parent as StackPanel;
            if (headerPanel?.Parent is TabItem tab)
            {
                if (tab.Content is WebView2 browser)
                {
                    try { browser.Dispose(); } catch { }
                }
                tabControl.Items.Remove(tab);
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tabControl.SelectedItem is TabItem selectedTab)
            {
                // Se a aba selecionada for a "NewTabItem", cria uma nova aba
                if (selectedTab.Name == "NewTabItem")
                {
                    CreateNewTab("https://nimbus-navegadorpaginainicial-4bcada32.base44.app/");
                }
                else
                {
                    // Se for uma aba comum, atualiza o UrlTextBox com a URL da aba
                    var browser = selectedTab.Content as WebView2;
                    if (browser != null && _urlTextBox != null)
                    {
                        _urlTextBox.Text = browser.Source?.ToString() ?? "";
                    }
                }
            }
        }

        // ---------- CREATE NEW TAB (com Tag do Image para uso posterior) ----------
        private async void CreateNewTab(string url)
        {
            var browser = new WebView2();

            // Ícone padrão inicial (pack URI)
            System.Windows.Controls.Image iconImage = new System.Windows.Controls.Image
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(2, 0, 0, 0),
                Source = new BitmapImage(new Uri("pack://application:,,,/Assets/POV_1_.ico"))
            };

            // Título da aba
            TextBlock titleBlock = new TextBlock
            {
                Text = "Nova Aba",
                Margin = new Thickness(5, 0, 0, 0),
                FontSize = 12,
                Width = 120,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            Button closeButton = new Button
            {
                Content = "X",
                Width = 20,
                Height = 20,
                Margin = new Thickness(5, 0, 0, 0)
            };
            closeButton.Click += CloseTabButton_Click;

            StackPanel headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(iconImage);
            headerPanel.Children.Add(titleBlock);
            headerPanel.Children.Add(closeButton);

            var newTab = new TabItem
            {
                Header = headerPanel,
                Content = browser,
                Margin = new Thickness(0)
            };

            // Salva referência do Image no Tag do browser (para ser usado nos handlers)
            browser.Tag = iconImage;

            // Assina NavigationCompleted (atualiza título e tenta favicon via JS+download)
            browser.NavigationCompleted += Browser_NavigationCompleted;

            // Insere a nova aba antes da última (NewTabItem) e seleciona
            int insertIndex = Math.Max(0, tabControl.Items.Count - 1);
            tabControl.Items.Insert(insertIndex, newTab);
            tabControl.SelectedItem = newTab;

            // Inicializa CoreWebView2 e registra o FaviconChanged (fallback adicional)
            try
            {
                await browser.EnsureCoreWebView2Async();

                if (browser.CoreWebView2 != null)
                {
                    // fallback: se o engine tiver favicon pronto, esse evento vai ajudar
                    browser.CoreWebView2.FaviconChanged += async (s, e) =>
                    {
                        try
                        {
                            using (var stream = await browser.CoreWebView2.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png))
                            {
                                if (stream != null)
                                {
                                    var bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.StreamSource = stream;
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.EndInit();
                                    bitmap.Freeze();

                                    Dispatcher.Invoke(() => iconImage.Source = bitmap);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("GetFaviconAsync fallback failed: " + ex.Message);
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("EnsureCoreWebView2Async failed: " + ex.Message);
            }

            // Navega para URL
            try
            {
                browser.Source = new Uri(url);
            }
            catch
            {
                // ignore
            }
        }
        // -------------------------------------------------------------------------------

        private WebView2 GetCurrentBrowser()
        {
            return tabControl.SelectedItem is TabItem tab ? tab.Content as WebView2 : null;
        }

        // Main handler que atualiza título e tenta obter favicon
        private async void Browser_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            var browser = (WebView2)sender;

            if (browser == null || browser.CoreWebView2 == null)
                return;

            try
            {
                Debug.WriteLine("NavigationCompleted: " + browser.Source);

                // Encontrar a TabItem dona deste browser
                TabItem ownerTab = null;
                foreach (TabItem t in tabControl.Items)
                {
                    if (t.Content == browser)
                    {
                        ownerTab = t;
                        break;
                    }
                }

                // Atualiza só o TextBlock do header (mantendo Image e botão)
                if (ownerTab?.Header is StackPanel headerPanel)
                {
                    foreach (var child in headerPanel.Children)
                    {
                        if (child is TextBlock titleBlock)
                        {
                            string docTitle = browser.CoreWebView2.DocumentTitle;
                            titleBlock.Text = string.IsNullOrEmpty(docTitle) ? "Nova Aba" :
                                              (docTitle.Length > 25 ? docTitle.Substring(0, 22) + "..." : docTitle);
                            break;
                        }
                    }
                }

                // Atualiza URL na barra se aba selecionada
                if (tabControl.SelectedItem == ownerTab && _urlTextBox != null)
                {
                    _urlTextBox.Text = browser.Source?.ToString() ?? "";
                }

                // 1) tenta pegar favicon declarado no HTML via JS (primeira tentativa)
                string faviconHref = null;
                try
                {
                    string script = @"
                        (function() {
                            try {
                                var links = document.querySelectorAll('link[rel~=""icon""]');
                                if (links.length > 0) return links[0].href;
                                return '';
                            } catch (e) { return ''; }
                        })();
                    ";
                    string result = await browser.ExecuteScriptAsync(script);
                    Debug.WriteLine("ExecuteScriptAsync result: " + result);

                    if (!string.IsNullOrWhiteSpace(result) && result != "null")
                    {
                        // normalmente o resultado é uma string JSON com aspas -> "\"https://...\""
                        faviconHref = result.Trim().Trim('"');
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ExecuteScriptAsync failed: " + ex.Message);
                }

                // 2) fallback para /favicon.ico se não encontrou via JS
                if (string.IsNullOrWhiteSpace(faviconHref))
                {
                    try
                    {
                        var uri = new Uri(browser.Source.ToString());
                        faviconHref = uri.Scheme + "://" + uri.Host + "/favicon.ico";
                        Debug.WriteLine("Using fallback favicon: " + faviconHref);
                    }
                    catch { }
                }
                else
                {
                    Debug.WriteLine("Favicon href found: " + faviconHref);
                }

                // 3) resolve href relativo se necessário e obtém bytes
                byte[] imageBytes = null;
                if (!string.IsNullOrWhiteSpace(faviconHref))
                {
                    try
                    {
                        if (faviconHref.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            // data URI
                            var comma = faviconHref.IndexOf(',');
                            if (comma > 0)
                            {
                                var meta = faviconHref.Substring(5, comma - 5);
                                var dataPart = faviconHref.Substring(comma + 1);
                                if (meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                                {
                                    imageBytes = Convert.FromBase64String(dataPart);
                                }
                                else
                                {
                                    imageBytes = Encoding.UTF8.GetBytes(Uri.UnescapeDataString(dataPart));
                                }
                            }
                        }
                        else
                        {
                            Uri favUri;
                            if (!Uri.TryCreate(faviconHref, UriKind.Absolute, out favUri))
                            {
                                var baseUri = new Uri(browser.Source.ToString());
                                favUri = new Uri(baseUri, faviconHref);
                            }

                            Debug.WriteLine("Downloading favicon: " + favUri);
                            imageBytes = await _httpClient.GetByteArrayAsync(favUri);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error fetching favicon bytes: " + ex.Message);
                        imageBytes = null;
                    }
                }

                // 4) se não obteve bytes via download, tenta GetFaviconAsync do CoreWebView2 (fallback)
                if ((imageBytes == null || imageBytes.Length == 0) && browser.CoreWebView2 != null)
                {
                    try
                    {
                        using (var s = await browser.CoreWebView2.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png))
                        {
                            if (s != null)
                            {
                                using (var ms = new MemoryStream())
                                {
                                    await s.CopyToAsync(ms);
                                    imageBytes = ms.ToArray();
                                    Debug.WriteLine("Got favicon bytes from GetFaviconAsync");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("GetFaviconAsync failed: " + ex.Message);
                    }
                }

                // 5) aplica o favicon no Image do header (se existir)
                if (imageBytes != null && imageBytes.Length > 0 && ownerTab?.Header is StackPanel headerPanelStack)
                {
                    System.Windows.Controls.Image iconImage = null;
                    if (browser.Tag is System.Windows.Controls.Image tagImg)
                        iconImage = tagImg;
                    else
                    {
                        foreach (var child in headerPanelStack.Children)
                        {
                            if (child is System.Windows.Controls.Image im) { iconImage = im; break; }
                        }
                    }

                    if (iconImage != null)
                    {
                        bool loaded = false;

                        // tentativa 1: carregar via BitmapImage a partir do MemoryStream
                        try
                        {
                            using (var ms = new MemoryStream(imageBytes))
                            {
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.StreamSource = ms;
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.EndInit();
                                bitmap.Freeze();

                                Dispatcher.Invoke(() => iconImage.Source = bitmap);
                                loaded = true;
                                Debug.WriteLine("Favicon loaded via BitmapImage");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("BitmapImage load failed: " + ex.Message);
                        }

                        // tentativa 2: se falhar (provavelmente .ico), converter usando System.Drawing.Icon
                        if (!loaded)
                        {
                            try
                            {
                                using (var ms2 = new MemoryStream(imageBytes))
                                using (var icon = new System.Drawing.Icon(ms2))
                                using (var bmp = icon.ToBitmap())
                                using (var bmpStream = new MemoryStream())
                                {
                                    bmp.Save(bmpStream, ImageFormat.Png);
                                    bmpStream.Seek(0, SeekOrigin.Begin);

                                    var bitmap2 = new BitmapImage();
                                    bitmap2.BeginInit();
                                    bitmap2.StreamSource = bmpStream;
                                    bitmap2.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap2.EndInit();
                                    bitmap2.Freeze();

                                    Dispatcher.Invoke(() => iconImage.Source = bitmap2);
                                    loaded = true;
                                    Debug.WriteLine("Favicon loaded via Icon->Bitmap conversion");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("ICO conversion failed: " + ex.Message);
                            }
                        }

                        if (!loaded)
                        {
                            Debug.WriteLine("Failed to load favicon, leaving default.");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Header Image not found to apply favicon.");
                    }
                }
                else
                {
                    Debug.WriteLine("No favicon bytes available.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in Browser_NavigationCompleted: " + ex.ToString());
            }
        }

        // Helper que encontra o WebView2 a partir do CoreWebView2 (se precisar)
        private WebView2 GetBrowserFromCore(CoreWebView2 core)
        {
            foreach (TabItem tab in tabControl.Items)
            {
                if (tab.Content is WebView2 browser &&
                    browser.CoreWebView2 == core)
                {
                    return browser;
                }
            }
            return null;
        }
    }
}
