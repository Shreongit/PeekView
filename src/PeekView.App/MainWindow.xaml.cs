using Microsoft.Win32;
using PeekView.App.Models;
using PeekView.App.Services.Preview;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PeekView.App;

public partial class MainWindow : Window
{
    private readonly PreviewService _previewService;
    private readonly SemaphoreSlim _pdfRenderLock = new(1, 1);

    private PdfiumDocumentSession? _pdfSession;
    private string? _activePdfPath;

    private Grid? _pdfLayout;
    private ScrollViewer? _pdfDocumentViewer;
    private StackPanel? _pdfPagesPanel;
    private ScrollViewer? _thumbnailViewer;
    private StackPanel? _thumbnailPanel;
    private TextBlock? _pageStatus;
    private Button? _previousPageButton;
    private Button? _nextPageButton;

    private readonly List<Border> _pageHosts = new();
    private readonly HashSet<int> _renderedPages = new();
    private readonly HashSet<int> _pagesBeingRendered = new();
    private readonly Dictionary<int, Button> _thumbnailButtons = new();

    private int _currentPage;
    private double _zoom = 1.0;
    private bool _pdfLayoutActive;

    private const double MinZoom = 0.25;
    private const double MaxZoom = 4.0;
    private const double ZoomStep = 1.15;
    private const double PageSpacing = 18.0;
    private const double PageWidthLimit = 1100.0;

    public MainWindow()
    {
        InitializeComponent();

        var providers = new IPreviewProvider[]
        {
            new FolderPreviewProvider(),
            new ImagePreviewProvider(),
            new PdfPreviewProvider(),
            new DocxPreviewProvider(),
            new ExcelPreviewProvider(),
            new TextPreviewProvider()
        };

        _previewService = new PreviewService(
            new PreviewProviderFactory(providers));

        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        ReleasePreviewResources();
        Hide();
    }

    private void ReleasePreviewResources()
    {
        _pdfSession?.Dispose();
        _pdfSession = null;
        _activePdfPath = null;
        _currentPage = 0;
        _pdfLayoutActive = false;

        if (_pdfLayout?.Parent is Panel parent)
            parent.Children.Remove(_pdfLayout);

        _pdfLayout = null;
        _pdfDocumentViewer = null;
        _pdfPagesPanel = null;
        _thumbnailViewer = null;
        _thumbnailPanel = null;
        _pageStatus = null;
        _previousPageButton = null;
        _nextPageButton = null;

        _pageHosts.Clear();
        _renderedPages.Clear();
        _pagesBeingRendered.Clear();
        _thumbnailButtons.Clear();

        ImageCanvas.Children.Clear();
        ImageCanvas.LayoutTransform = Transform.Identity;
        ImageViewport.Visibility = Visibility.Collapsed;

        PreviewText.Clear();
        PreviewText.Visibility = Visibility.Collapsed;

        FolderPreview.Visibility = Visibility.Collapsed;
        FolderNameText.Text = string.Empty;
        FolderSizeText.Text = string.Empty;
        FolderItemCountText.Text = string.Empty;
        FolderModifiedText.Text = string.Empty;

        EmptyState.Visibility = Visibility.Collapsed;
        ResetZoom();
        Title = "PeekView";
    }

    private void ResetZoom()
    {
        _zoom = 1.0;
        ImageCanvas.LayoutTransform = new ScaleTransform(1, 1);
    }

    private void ImageViewport_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            ChangeZoom(e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep);
            e.Handled = true;
            return;
        }

        if (_pdfDocumentViewer is not null && _pdfLayoutActive)
        {
            _pdfDocumentViewer.ScrollToVerticalOffset(
                _pdfDocumentViewer.VerticalOffset - e.Delta);
        }
        else
        {
            ImageViewport.ScrollToVerticalOffset(
                ImageViewport.VerticalOffset - e.Delta);
        }

        e.Handled = true;
    }

    private void ChangeZoom(double factor)
    {
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);

        if (_pdfSession is not null && _pdfLayoutActive)
        {
            RebuildPdfPagePlaceholders();
            _ = RenderVisiblePdfPagesAsync();
            return;
        }

        ImageCanvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
    }

    public async Task LoadPreviewAsync(string filePath)
    {
        ReleasePreviewResources();
        Title = $"{Path.GetFileName(filePath)} - PeekView";

        try
        {
            if (string.Equals(
                Path.GetExtension(filePath),
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
            {
                await OpenPdfAsync(filePath);
                return;
            }

            PreviewResult result = await _previewService.PreviewAsync(filePath);

            if (!result.IsSuccess)
            {
                EmptyState.Visibility = Visibility.Visible;

                MessageBox.Show(
                    result.Message ?? "Preview failed.",
                    "PeekView",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (result.IsDirectory)
            {
                FolderNameText.Text = Path.GetFileName(
                    Path.TrimEndingDirectorySeparator(filePath));
                FolderSizeText.Text = $"Size: {FormatSize(result.DirectorySizeBytes)}";
                FolderItemCountText.Text = $"Items: {result.DirectoryItemCount}";
                FolderModifiedText.Text =
                    $"Last modified: {result.DirectoryLastModified:yyyy-MM-dd HH:mm}";
                FolderPreview.Visibility = Visibility.Visible;
            }
            else if (result.Image is not null)
            {
                DisplayPages(new[] { result.Image });
            }
            else if (result.TextContent is not null)
            {
                PreviewText.Text = result.TextContent;
                PreviewText.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyState.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            EmptyState.Visibility = Visibility.Visible;
            MessageBox.Show(
                $"Unable to preview this file:\n{ex.Message}",
                "PeekView",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task OpenPdfAsync(string filePath)
    {
        PdfiumDocumentSession? newSession = null;

        await Task.Run(() =>
        {
            newSession = new PdfiumDocumentSession(filePath);
            if (newSession.PageCount <= 0)
            {
                newSession.Dispose();
                newSession = null;
                throw new InvalidOperationException("This PDF contains no pages.");
            }
        });

        _pdfSession = newSession;
        _activePdfPath = filePath;
        _currentPage = 0;
        _zoom = 1.0;

        BuildPdfLayout();
        BuildPdfPagePlaceholders();
        BuildThumbnailButtons();

        _pdfLayoutActive = true;
        ImageViewport.Visibility = Visibility.Collapsed;
        _pdfLayout!.Visibility = Visibility.Visible;

        await RenderVisiblePdfPagesAsync();
        UpdatePdfNavigation();
    }

    private void BuildPdfLayout()
    {
        if (_pdfLayout is not null)
            return;

        _pdfLayout = new Grid
        {
            Background = Brushes.White,
            Margin = new Thickness(0),
            Visibility = Visibility.Collapsed
        };

        _pdfLayout.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _pdfLayout.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(164) });

        var viewerColumn = new Grid();
        viewerColumn.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        viewerColumn.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });

        _pdfDocumentViewer = new ScrollViewer
        {
            Background = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            CanContentScroll = false,
            Focusable = true
        };
        _pdfDocumentViewer.ScrollChanged += PdfDocumentViewer_ScrollChanged;

        _pdfPagesPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(14, 14, 14, 8)
        };
        _pdfDocumentViewer.Content = _pdfPagesPanel;
        viewerColumn.Children.Add(_pdfDocumentViewer);

        var toolbar = new DockPanel
        {
            Margin = new Thickness(8),
            LastChildFill = false,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _previousPageButton = new Button
        {
            Content = "‹",
            Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 8, 0)
        };
        _previousPageButton.Click += async (_, _) =>
            await NavigatePdfPageAsync(_currentPage - 1);

        _pageStatus = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0)
        };

        _nextPageButton = new Button
        {
            Content = "›",
            Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(8, 0, 0, 0)
        };
        _nextPageButton.Click += async (_, _) =>
            await NavigatePdfPageAsync(_currentPage + 1);

        toolbar.Children.Add(_previousPageButton);
        toolbar.Children.Add(_pageStatus);
        toolbar.Children.Add(_nextPageButton);
        Grid.SetRow(toolbar, 1);
        viewerColumn.Children.Add(toolbar);

        Grid.SetColumn(viewerColumn, 0);
        _pdfLayout.Children.Add(viewerColumn);

        _thumbnailPanel = new StackPanel { Margin = new Thickness(8) };
        _thumbnailViewer = new ScrollViewer
        {
            Content = _thumbnailPanel,
            Background = Brushes.White,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetColumn(_thumbnailViewer, 1);
        _pdfLayout.Children.Add(_thumbnailViewer);

        if (ImageViewport.Parent is Grid contentGrid)
        {
            int index = contentGrid.Children.IndexOf(ImageViewport);
            contentGrid.Children.Insert(Math.Max(0, index), _pdfLayout);
        }
        else
        {
            throw new InvalidOperationException(
                "The PDF layout could not be attached to the main window.");
        }
    }

    private void BuildPdfPagePlaceholders()
    {
        if (_pdfSession is null || _pdfPagesPanel is null)
            return;

        _pdfPagesPanel.Children.Clear();
        _pageHosts.Clear();
        _renderedPages.Clear();
        _pagesBeingRendered.Clear();

        double pageWidth = GetTargetPageWidth();

        for (int pageIndex = 0; pageIndex < _pdfSession.PageCount; pageIndex++)
        {
            double pageHeight = pageWidth * 1.4142 * _zoom;

            var pageNumber = new TextBlock
            {
                Text = $"Page {pageIndex + 1}",
                Foreground = Brushes.Gray,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 5),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var placeholder = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(210, 210, 210)),
                BorderThickness = new Thickness(1),
                Width = pageWidth * _zoom,
                Height = pageHeight,
                Margin = new Thickness(0, 0, 0, PageSpacing),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Grid
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Loading page…",
                            Foreground = Brushes.Gray,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                },
                Tag = pageIndex
            };

            var container = new StackPanel();
            container.Children.Add(pageNumber);
            container.Children.Add(placeholder);

            var host = new Border
            {
                Child = container,
                Tag = pageIndex,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            _pageHosts.Add(host);
            _pdfPagesPanel.Children.Add(host);
        }
    }

    private void BuildThumbnailButtons()
    {
        if (_pdfSession is null || _thumbnailPanel is null)
            return;

        _thumbnailPanel.Children.Clear();
        _thumbnailButtons.Clear();

        for (int pageIndex = 0; pageIndex < _pdfSession.PageCount; pageIndex++)
        {
            int capturedIndex = pageIndex;

            var thumbnailImage = new Image
            {
                Width = 104,
                Height = 132,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var pageLabel = new TextBlock
            {
                Text = (pageIndex + 1).ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = Brushes.Black
            };

            var content = new StackPanel();
            content.Children.Add(thumbnailImage);
            content.Children.Add(pageLabel);

            var button = new Button
            {
                Content = content,
                Tag = pageIndex,
                Padding = new Thickness(5),
                Margin = new Thickness(0, 0, 0, 8),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(215, 215, 215)),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            button.Click += async (_, _) => await NavigatePdfPageAsync(capturedIndex);
            _thumbnailButtons[pageIndex] = button;
            _thumbnailPanel.Children.Add(button);

            _ = RenderThumbnailAsync(capturedIndex, thumbnailImage);
        }
    }

    private async Task RenderThumbnailAsync(int pageIndex, Image target)
    {
        var session = _pdfSession;
        if (session is null)
            return;

        try
        {
            await _pdfRenderLock.WaitAsync();
            BitmapSource bitmap;
            try
            {
                if (!ReferenceEquals(session, _pdfSession))
                    return;

                bitmap = await Task.Run(() => session.RenderPage(pageIndex, 150, 190));
            }
            finally
            {
                _pdfRenderLock.Release();
            }

            if (ReferenceEquals(session, _pdfSession))
                target.Source = bitmap;
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
            or ArgumentException
            or IOException)
        {
            // A failed thumbnail should not prevent the document from opening.
        }
    }

    private async Task NavigatePdfPageAsync(int pageIndex)
    {
        if (_pdfSession is null ||
            pageIndex < 0 ||
            pageIndex >= _pdfSession.PageCount)
            return;

        _currentPage = pageIndex;
        UpdatePdfNavigation();

        if (_pdfDocumentViewer is not null && pageIndex < _pageHosts.Count)
        {
            _pdfDocumentViewer.UpdateLayout();
            _pdfDocumentViewer.ScrollToVerticalOffset(
                _pageHosts[pageIndex].TranslatePoint(
                    new Point(0, 0), _pdfPagesPanel!).Y);
        }

        await RenderVisiblePdfPagesAsync();
    }

    private async void PdfDocumentViewer_ScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (_pdfSession is null || !_pdfLayoutActive)
            return;

        UpdateCurrentPageFromScroll();
        await RenderVisiblePdfPagesAsync();
    }

    private void UpdateCurrentPageFromScroll()
    {
        if (_pdfDocumentViewer is null || _pageHosts.Count == 0)
            return;

        double viewportCenter =
            _pdfDocumentViewer.VerticalOffset + _pdfDocumentViewer.ViewportHeight / 2;

        int closestPage = 0;
        double closestDistance = double.MaxValue;

        for (int i = 0; i < _pageHosts.Count; i++)
        {
            double pageTop = _pageHosts[i].TranslatePoint(
                new Point(0, 0), _pdfPagesPanel!).Y;
            double pageCenter = pageTop + _pageHosts[i].ActualHeight / 2;
            double distance = Math.Abs(pageCenter - viewportCenter);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPage = i;
            }
        }

        _currentPage = closestPage;
        UpdatePdfNavigation();
    }

    private async Task RenderVisiblePdfPagesAsync()
    {
        if (_pdfSession is null ||
            _pdfDocumentViewer is null ||
            _pageHosts.Count == 0)
            return;

        _pdfDocumentViewer.UpdateLayout();

        double top = _pdfDocumentViewer.VerticalOffset;
        double bottom = top + Math.Max(1, _pdfDocumentViewer.ViewportHeight);
        double buffer = Math.Max(500, _pdfDocumentViewer.ViewportHeight);

        for (int i = 0; i < _pageHosts.Count; i++)
        {
            if (_renderedPages.Contains(i) || _pagesBeingRendered.Contains(i))
                continue;

            var host = _pageHosts[i];
            double hostTop = host.TranslatePoint(new Point(0, 0), _pdfPagesPanel!).Y;
            double hostBottom = hostTop + Math.Max(host.ActualHeight, host.DesiredSize.Height);

            if (hostBottom < top - buffer || hostTop > bottom + buffer)
                continue;

            _pagesBeingRendered.Add(i);
            _ = RenderDocumentPageAsync(i);
        }

        await Task.CompletedTask;
    }

    private async Task RenderDocumentPageAsync(int pageIndex)
    {
        var session = _pdfSession;
        if (session is null || pageIndex >= _pageHosts.Count)
            return;

        try
        {
            await _pdfRenderLock.WaitAsync();
            BitmapSource bitmap;
            try
            {
                if (!ReferenceEquals(session, _pdfSession))
                    return;

                double width = await Dispatcher.InvokeAsync(GetTargetPageWidth);
                int renderWidth = Math.Clamp((int)(width * _zoom * 1.5), 400, 1800);
                int renderHeight = (int)(renderWidth * 1.4142);

                bitmap = await Task.Run(() =>
                    session.RenderPage(pageIndex, renderWidth, renderHeight));
            }
            finally
            {
                _pdfRenderLock.Release();
            }

            if (!ReferenceEquals(session, _pdfSession) ||
                pageIndex >= _pageHosts.Count)
                return;

            await Dispatcher.InvokeAsync(() =>
            {
                if (!ReferenceEquals(session, _pdfSession) ||
                    pageIndex >= _pageHosts.Count)
                    return;

                var host = _pageHosts[pageIndex];
                if (host.Child is not StackPanel stack || stack.Children.Count < 2)
                    return;

                var pageBorder = (Border)stack.Children[1];
                pageBorder.Child = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    Width = pageBorder.Width,
                    Height = pageBorder.Height,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                _renderedPages.Add(pageIndex);
            });
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
            or ArgumentException
            or IOException)
        {
            // Keep the page placeholder visible if a page cannot be rendered.
        }
        finally
        {
            _pagesBeingRendered.Remove(pageIndex);
        }
    }

    private void RebuildPdfPagePlaceholders()
    {
        if (_pdfSession is null)
            return;

        double offset = _pdfDocumentViewer?.VerticalOffset ?? 0;
        BuildPdfPagePlaceholders();

        if (_pdfDocumentViewer is not null)
        {
            _pdfDocumentViewer.UpdateLayout();
            _pdfDocumentViewer.ScrollToVerticalOffset(offset);
        }
    }

    private double GetTargetPageWidth()
    {
        double available = (_pdfDocumentViewer?.ActualWidth ?? ImageViewport.ActualWidth) - 70;
        if (available < 300)
            available = 700;

        return Math.Min(PageWidthLimit, available);
    }

    private void UpdatePdfNavigation()
    {
        if (_pageStatus is not null && _pdfSession is not null)
            _pageStatus.Text = $"{_currentPage + 1} / {_pdfSession.PageCount}";

        if (_previousPageButton is not null)
            _previousPageButton.IsEnabled = _currentPage > 0;

        if (_nextPageButton is not null && _pdfSession is not null)
            _nextPageButton.IsEnabled = _currentPage < _pdfSession.PageCount - 1;

        foreach (var pair in _thumbnailButtons)
        {
            pair.Value.BorderBrush = pair.Key == _currentPage
                ? Brushes.DodgerBlue
                : new SolidColorBrush(Color.FromRgb(215, 215, 215));

            pair.Value.BorderThickness = pair.Key == _currentPage
                ? new Thickness(2)
                : new Thickness(1);
        }
    }

    private void DisplayPages(IReadOnlyList<BitmapSource> pages)
    {
        ImageCanvas.Children.Clear();

        foreach (BitmapSource page in pages)
        {
            var image = new Image
            {
                Source = page,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                MaxWidth = 1200
            };

            ImageCanvas.Children.Add(new Border
            {
                Child = image,
                Background = Brushes.White,
                Margin = new Thickness(0, 0, 0, PageSpacing)
            });
        }

        ResetZoom();
        ImageViewport.ScrollToTop();
        ImageViewport.Visibility = Visibility.Visible;
        ImageViewport.Focus();
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private async void OpenImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a file to preview",
            Filter = "All files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
            await LoadPreviewAsync(dialog.FileName);
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                ReleasePreviewResources();
                Hide();
                e.Handled = true;
                break;

            case Key.Up:
                if (_pdfDocumentViewer is not null && _pdfLayoutActive)
                    _pdfDocumentViewer.ScrollToVerticalOffset(_pdfDocumentViewer.VerticalOffset - 60);
                else
                    ImageViewport.ScrollToVerticalOffset(ImageViewport.VerticalOffset - 60);
                e.Handled = true;
                break;

            case Key.Down:
                if (_pdfDocumentViewer is not null && _pdfLayoutActive)
                    _pdfDocumentViewer.ScrollToVerticalOffset(_pdfDocumentViewer.VerticalOffset + 60);
                else
                    ImageViewport.ScrollToVerticalOffset(ImageViewport.VerticalOffset + 60);
                e.Handled = true;
                break;

            case Key.PageUp:
                if (_pdfSession is not null)
                    _ = NavigatePdfPageAsync(_currentPage - 1);
                else
                    ImageViewport.PageUp();
                e.Handled = true;
                break;

            case Key.PageDown:
                if (_pdfSession is not null)
                    _ = NavigatePdfPageAsync(_currentPage + 1);
                else
                    ImageViewport.PageDown();
                e.Handled = true;
                break;

            case Key.Home:
                if (_pdfSession is not null)
                    _ = NavigatePdfPageAsync(0);
                else
                    ImageViewport.ScrollToTop();
                e.Handled = true;
                break;

            case Key.End:
                if (_pdfSession is not null)
                    _ = NavigatePdfPageAsync(_pdfSession.PageCount - 1);
                else
                    ImageViewport.ScrollToBottom();
                e.Handled = true;
                break;

            case Key.Add:
            case Key.OemPlus:
                ChangeZoom(ZoomStep);
                e.Handled = true;
                break;

            case Key.Subtract:
            case Key.OemMinus:
                ChangeZoom(1.0 / ZoomStep);
                e.Handled = true;
                break;
        }
    }
}
