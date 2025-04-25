using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using Microsoft.Win32;
using Xceed.Wpf.Toolkit;

namespace Wpf_Paint_Lab8_Egmilan;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Canvas.PreviewMouseLeftButtonDown += Canvas_PreviewMouseLeftButtonDown;
        Canvas.PreviewMouseMove += Canvas_PreviewMouseMove;
        Canvas.PreviewMouseLeftButtonUp += Canvas_PreviewMouseLeftButtonUp;

        //zoom
        Canvas.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
        Canvas.MouseMove += Canvas_MouseMove;
        Canvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
        this.Loaded += (s, e) => InitializeZoom();
        ZoomTransform.ScaleX = 1;
        ZoomTransform.ScaleY = 1;
        Loaded += MainWindow_Loaded;

        // Set default ink attributes
        Canvas.DefaultDrawingAttributes.Width = 2;
        Canvas.DefaultDrawingAttributes.Height = 2;
        Canvas.DefaultDrawingAttributes.Color = Colors.Black;

        //redo and undo
        SaveState();

        // Handle drawing and erasing
        Canvas.StrokeCollected += Canvas_StrokeCollected;
        Canvas.Strokes.StrokesChanged += Strokes_Changed;

        UpdateButtonStates();

        //brushes
        Canvas.DefaultDrawingAttributes = _normalPenAttributes;
        //UpdateHighlighterColor(_currentHighlighterColor);
    }

    private Point selectionStart;
    private Rectangle selectionRect;
    private bool isSelecting = false;

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Circle button clicked!");
    }


    private void CutMenuItem_Click(object sender, RoutedEventArgs e)
    {

    }

    private void MenuItem_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Button button)
        {
            button.Background = Brushes.LightBlue;
            button.Foreground = Brushes.Black;
            button.Cursor = Cursors.Hand;
        }
    }

    private void MenuItem_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Button button)
        {
            button.Background = Brushes.Transparent;
            button.Cursor = Cursors.Arrow;
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        EditPopup.IsOpen = !EditPopup.IsOpen;
    }

    private void SelectionButton_Click(object sender, RoutedEventArgs e)
    {
        SelectionPopup.IsOpen = !SelectionPopup.IsOpen;
    }

    private void FilePopup_Click(object sender, RoutedEventArgs e)
    {
        FilePopup.IsOpen = !FilePopup.IsOpen;
    }

    private void FlipPopup_Click(object sender, RoutedEventArgs e)
    {
        FlipPopup.IsOpen = !FlipPopup.IsOpen;
    }

    private void RotatePopup_Click(object sender, RoutedEventArgs e)
    {
        RotatePopup.IsOpen = !RotatePopup.IsOpen;
    }
    private void BrushPopup_Click(object sender, RoutedEventArgs e)
    {
        BrushPopup.IsOpen = !BrushPopup.IsOpen;
    }
    private void ShapePopup_Click(object sender, RoutedEventArgs e)
    {
        ShapePopup.IsOpen = !ShapePopup.IsOpen;
    }
    private void ViewPopup_Click(object sender, RoutedEventArgs e)
    {
        ViewPopup.IsOpen = !ViewPopup.IsOpen;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Ask the user where to save the image
        // Generate automatic filename with timestamp
        string autoFilename = $"Drawing_{DateTime.Now:yyyyMMdd_HHmmss}";

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG Image|*.png|JPEG Image|*.jpg",
            Title = "Save Canvas",
            FileName = autoFilename,  // Set default filename
            AddExtension = true,
            DefaultExt = ".png"  // Default to PNG format
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                // Get the bounds of ALL content
                Rect bounds = VisualTreeHelper.GetDescendantBounds(Canvas);
                if (bounds.IsEmpty) return;

                // Create render target
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    (int)bounds.Width,
                    (int)bounds.Height,
                    96, 96, PixelFormats.Pbgra32);

                // Create temporary visual with transform compensation
                DrawingVisual dv = new DrawingVisual();
                using (DrawingContext dc = dv.RenderOpen())
                {
                    dc.PushTransform(new ScaleTransform(
                        1 / ZoomTransform.ScaleX,
                        1 / ZoomTransform.ScaleY));
                    dc.DrawRectangle(new VisualBrush(Canvas), null, bounds);
                }

                // Render and save
                rtb.Render(dv);

                BitmapEncoder encoder = saveDialog.FilterIndex == 1
                    ? new PngBitmapEncoder()
                    : new JpegBitmapEncoder();

                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using (FileStream fs = new FileStream(saveDialog.FileName, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                //MessageBox.Show($"Canvas saved as: {Path.GetFileName(saveDialog.FileName)}");
                System.Windows.MessageBox.Show("✅ Your drawing has been saved successfully!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);

            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"❌ Save failed: {ex.Message}\n\nPlease check:\n• Available disk space\n• File permissions\n• File format compatibility",
                               "Save Error",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }
    }

    private void ShareButton_Click(object sender, RoutedEventArgs e)
    {
        string filePath = "C:\\Users\\You\\Documents\\Drawing.png"; // Replace with your actual saved file path

        if (File.Exists(filePath))
        {
            Clipboard.SetText(filePath);
            System.Windows.MessageBox.Show("📋 File path copied to clipboard! Share it anywhere.", "Shared", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            System.Windows.MessageBox.Show("⚠️ File not found. Please save your work first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public enum EditingMode
    {
        Select,
        free,
        Pen,
        Eraser
    }
    public void onselectClicked(object sender, RoutedEventArgs args)
    {
        SetEditingMode(EditingMode.free);
        BrushPopup.IsOpen = false;
    }
    public void onOpenClicked(object sender, RoutedEventArgs args)
    {
        if (EraserThicknessSlider.Visibility == Visibility.Visible)
        {
            EraserThicknessSlider.Visibility = Visibility.Collapsed;
        }

        if (PencilThickness.Visibility == Visibility.Collapsed)
        {
            PencilThickness.Visibility = Visibility.Visible;
        }
        else
            PencilThickness.Visibility = Visibility.Collapsed;

        if (PencilThickness.Visibility == Visibility.Visible || EraserThicknessSlider.Visibility == Visibility.Visible)
            Rect1.Visibility = Visibility.Visible;
        else
            Rect1.Visibility = Visibility.Collapsed;

        SetEditingMode(EditingMode.Pen);
    }
    public void onEraserClicked(object sender, RoutedEventArgs args)
    {

        if (PencilThickness.Visibility == Visibility.Visible)
        {
            PencilThickness.Visibility = Visibility.Collapsed;
        }

        if (EraserThicknessSlider.Visibility == Visibility.Collapsed)
        {
            EraserThicknessSlider.Visibility = Visibility.Visible;
        }
        else
            EraserThicknessSlider.Visibility = Visibility.Collapsed;
        if (PencilThickness.Visibility == Visibility.Visible || EraserThicknessSlider.Visibility == Visibility.Visible)
            Rect1.Visibility = Visibility.Visible;
        else
            Rect1.Visibility = Visibility.Collapsed;

        SetEditingMode(EditingMode.Eraser);
    }

    public void SetEditingMode(EditingMode mode)
    {
        switch (mode)
        {
            case EditingMode.Select:
                Canvas.EditingMode = InkCanvasEditingMode.None;
                break;
            case EditingMode.free:
                Canvas.EditingMode = InkCanvasEditingMode.Select;
                break;
            case EditingMode.Pen:
                Canvas.EditingMode = InkCanvasEditingMode.Ink;
                UpdatePenColor();
                break;
            case EditingMode.Eraser:
                Canvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                break;
            default:
                double size = EraserThicknessSlider.Value;
                Canvas.EraserShape = new EllipseStylusShape(size, size);
                //UpdateEraserCursor(size);
                break;

        }
    }
    private void PositionSelectionRectangle(Rectangle rect, double x, double y)
    {
        if (rect == null || Canvas == null) return;

        try
        {
            // Ensure the rectangle is in the visual tree
            if (!Canvas.Children.Contains(rect))
            {
                Canvas.Children.Add(rect);
            }

            // Set position with validation - NOTE THE STATIC CALLS
            if (!double.IsNaN(x) && !double.IsInfinity(x))
            {
                InkCanvas.SetLeft(rect, Math.Max(0, x)); // Static call
            }

            if (!double.IsNaN(y) && !double.IsInfinity(y))
            {
                InkCanvas.SetTop(rect, Math.Max(0, y)); // Static call
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Positioning failed: {ex.Message}");
            Canvas.Children.Remove(rect);
        }
    }
    private void Canvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Canvas.EditingMode != InkCanvasEditingMode.None ||
        !(Keyboard.Modifiers == ModifierKeys.None))
            return;


        selectionStart = e.GetPosition(Canvas);
        selectionRect = new Rectangle
        {
            Stroke = Brushes.DodgerBlue,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = new SolidColorBrush(Color.FromArgb(40, 30, 144, 255))
        };

        PositionSelectionRectangle(selectionRect, selectionStart.X, selectionStart.Y);
        isSelecting = true;
        e.Handled = true;
    }

    private void Canvas_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!isSelecting) return;

        var currentPos = e.GetPosition(Canvas);

        // Update position first
        PositionSelectionRectangle(selectionRect,
            Math.Min(selectionStart.X, currentPos.X),
            Math.Min(selectionStart.Y, currentPos.Y));

        // Then update dimensions
        UpdateSelectionRectangleDimensions(currentPos);
    }

    private void Canvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isSelecting || !IsValidRectangle(selectionRect)) return;

        try
        {
            var endPoint = e.GetPosition(Canvas);
            if (!IsValidPosition(endPoint)) return;

            // Final dimension update
            UpdateSelectionRectangleDimensions(endPoint);

            // Create selection area (with boundary checks)
            var selectionArea = new Rect(
                Math.Max(0, Math.Min(selectionStart.X, endPoint.X)),
                Math.Max(0, Math.Min(selectionStart.Y, endPoint.Y)),
                Math.Min(Canvas.ActualWidth, selectionRect.Width),
                Math.Min(Canvas.ActualHeight, selectionRect.Height)
            );

            // Remove rectangle and process selection
            Canvas.Children.Remove(selectionRect);
            ProcessSelection(selectionArea);
        }
        finally
        {
            selectionRect = null;
            isSelecting = false;
        }
    }

    private void ProcessSelection(Rect area)
    {
        if (Canvas?.Strokes == null) return;

        var strokesInArea = Canvas.Strokes
            .Where(s => s != null && s.GetBounds().IntersectsWith(area))
            .ToList();

        if (strokesInArea.Count > 0)
        {
            Canvas.Select(new StrokeCollection(strokesInArea));
        }
    }

    private void RectangleSelectButton2_Click(object sender, RoutedEventArgs e)
    {
        Canvas.EditingMode = InkCanvasEditingMode.None; // Important to allow custom selection
        Canvas.Cursor = Cursors.Cross;
        currentShape = ""; // Disable shape drawing
        SelectToolButton.Background = Brushes.LightGray;
        Canvas.Focus();
    }

    private bool IsValidRectangle(Rectangle rect)
    {
        return rect != null &&
               Canvas != null &&
               Canvas.Children.Contains(rect);
    }

    private bool IsValidPosition(Point point)
    {
        return !double.IsNaN(point.X) &&
               !double.IsInfinity(point.X) &&
               !double.IsNaN(point.Y) &&
               !double.IsInfinity(point.Y);
    }

    private void UpdateSelectionRectangleDimensions(Point currentPos)
    {
        if (selectionRect == null) return;

        try
        {
            double width = Math.Abs(currentPos.X - selectionStart.X);
            double height = Math.Abs(currentPos.Y - selectionStart.Y);

            // Validate dimensions before assigning
            if (!double.IsNaN(width) && !double.IsInfinity(width))
            {
                selectionRect.Width = Math.Max(0, width); // Ensure non-negative
            }

            if (!double.IsNaN(height) && !double.IsInfinity(height))
            {
                selectionRect.Height = Math.Max(0, height); // Ensure non-negative
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Dimension update failed: {ex.Message}");
            // Safely clean up if something went wrong
            if (selectionRect != null && Canvas.Children.Contains(selectionRect))
            {
                Canvas.Children.Remove(selectionRect);
            }
            selectionRect = null;
            isSelecting = false;
        }
    }
    public void Menu_SelectArea_Click(object sender, RoutedEventArgs e)
    {
        if (Canvas?.Strokes == null || Canvas.Strokes.Count == 0)
            return;

        try
        {
            var visibleBounds = new Rect(0, 0, Canvas.ActualWidth, Canvas.ActualHeight);
            var allStrokes = Canvas.Strokes
                .Where(s => s != null && s.GetBounds().IntersectsWith(visibleBounds))
                .ToList();

            if (allStrokes.Count > 0)
            {
                Canvas.Select(new StrokeCollection(allStrokes));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Select All failed: {ex.Message}");
        }
    }
    public void Menu_Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Canvas == null) return;

        try
        {
            // Delete strokes first
            var selectedStrokes = Canvas.GetSelectedStrokes();
            if (selectedStrokes?.Count > 0)
            {
                var strokesToDelete = selectedStrokes.ToList(); // Create concrete copy
                foreach (var stroke in strokesToDelete.Where(s => s != null))
                {
                    Canvas.Strokes?.Remove(stroke);
                }
                return;
            }

            // If no strokes, delete elements
            var selectedElements = Canvas.GetSelectedElements()?.OfType<UIElement>().ToList();
            if (selectedElements?.Count > 0 && Canvas.Children != null)
            {
                foreach (var element in selectedElements.Where(el => el != null))
                {
                    Canvas.Children.Remove(element);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Delete failed: {ex.Message}");
        }
    }

    private void FreeFormSelectButton_Click(object sender, RoutedEventArgs e)
    {
        // Set to free-form selection mode
        Canvas.EditingMode = InkCanvasEditingMode.Select; // Built-in lasso select
        Canvas.Cursor = Cursors.Cross;

        // Reset rectangle selection state
        if (selectionRect != null && Canvas.Children.Contains(selectionRect))
        {
            Canvas.Children.Remove(selectionRect);
        }
        isSelecting = false;

        // Visual feedback
        SelectToolButton.Background = Brushes.LightGray;
    }
    private void PenThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Canvas != null)
        {
            double thickness = e.NewValue;

            Canvas.DefaultDrawingAttributes.Width = thickness;
            Canvas.DefaultDrawingAttributes.Height = thickness;
        }
    }

    private void EraserThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        double thickness = e.NewValue;

        if (Canvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
        {
            Canvas.EraserShape = new RectangleStylusShape(thickness, thickness);
        }
    }
    // crop button
    private bool isCropping = false;
    private Point cropStartPoint;
    private Rectangle cropRectangle;

    private Stack<StrokeCollection> undoStack = new Stack<StrokeCollection>();

    private void CropButton_Click(object sender, RoutedEventArgs e)
    {
        if (isCropping)
        {
            CancelCrop();
            return;
        }

        // Save current state for undo
        undoStack.Push(new StrokeCollection(Canvas.Strokes));

        isCropping = true;
        Canvas.Cursor = Cursors.Cross;
        ((Button)sender).Background = Brushes.LightGray;
    }

    private void UndoLastCrop()
    {
        if (undoStack.Count > 0)
        {
            Canvas.Strokes = undoStack.Pop();
        }
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!isCropping || Canvas == null) return;

        try
        {
            cropStartPoint = e.GetPosition(Canvas);

            // Create crop rectangle
            cropRectangle = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(40, 255, 0, 0))
            };

            // Position rectangle with validation
            if (cropRectangle != null && !double.IsNaN(cropStartPoint.X) && !double.IsNaN(cropStartPoint.Y))
            {
                InkCanvas.SetLeft(cropRectangle, cropStartPoint.X);
                InkCanvas.SetTop(cropRectangle, cropStartPoint.Y);
                Canvas.Children.Add(cropRectangle);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Crop start failed: {ex.Message}");
            cropRectangle = null;
        }
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isCropping || Canvas == null || cropRectangle == null) return;

        try
        {
            Point currentPos = e.GetPosition(Canvas);
            double width = currentPos.X - cropStartPoint.X;
            double height = currentPos.Y - cropStartPoint.Y;

            // Validate dimensions before setting
            if (!double.IsNaN(width) && !double.IsInfinity(width) &&
                !double.IsNaN(height) && !double.IsInfinity(height))
            {
                cropRectangle.Width = Math.Abs(width);
                cropRectangle.Height = Math.Abs(height);

                // Validate positions before setting
                if (!double.IsNaN(currentPos.X) && !double.IsNaN(currentPos.Y))
                {
                    InkCanvas.SetLeft(cropRectangle, Math.Min(cropStartPoint.X, currentPos.X));
                    InkCanvas.SetTop(cropRectangle, Math.Min(cropStartPoint.Y, currentPos.Y));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Crop move failed: {ex.Message}");
        }
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isCropping || Canvas == null || cropRectangle == null) return;

        try
        {
            Point endPoint = e.GetPosition(Canvas);
            if (double.IsNaN(endPoint.X) || double.IsNaN(endPoint.Y)) return;

            Rect cropArea = new Rect(
                Math.Min(cropStartPoint.X, endPoint.X),
                Math.Min(cropStartPoint.Y, endPoint.Y),
                Math.Abs(endPoint.X - cropStartPoint.X),
                Math.Abs(endPoint.Y - cropStartPoint.Y));

            ApplyCrop(cropArea);
        }
        finally
        {
            // Clean up in all cases
            if (cropRectangle != null && Canvas.Children.Contains(cropRectangle))
            {
                Canvas.Children.Remove(cropRectangle);
            }
            cropRectangle = null;
            isCropping = false;
            Canvas.Cursor = Cursors.Arrow;
        }
    }
    private void ApplyCrop(Rect cropArea)
    {
        // 1. Get all strokes that intersect with the crop area
        StrokeCollection strokesToKeep = new StrokeCollection(
            Canvas.Strokes.Where(stroke => stroke.GetBounds().IntersectsWith(cropArea)));

        // 2. Create a new InkCanvas or clear the existing one
        Canvas.Strokes.Clear();

        // 3. Add only the strokes within the crop area
        foreach (Stroke stroke in strokesToKeep)
        {
            // Optional: Adjust stroke positions relative to crop area
            Stroke newStroke = stroke.Clone();
            Matrix transform = new Matrix();
            transform.Translate(-cropArea.X, -cropArea.Y);
            newStroke.Transform(transform, false);
            Canvas.Strokes.Add(newStroke);
        }

        // 4. If you have other elements (images, shapes), crop them similarly
    }

    private void CancelCrop()
    {
        if (cropRectangle != null)
        {
            Canvas.Children.Remove(cropRectangle);
            cropRectangle = null;
        }
        isCropping = false;
        Canvas.Cursor = Cursors.Arrow;
    }
    // end crop

    //zoom
    private ScaleTransform _zoomTransform;

    private void InitializeZoom()
    {
        // Get the transform from XAML or create new if missing
        _zoomTransform = ZoomContainer.RenderTransform as ScaleTransform ?? new ScaleTransform();
        ZoomContainer.RenderTransform = _zoomTransform;
        ZoomContainer.RenderTransformOrigin = new Point(0.5, 0.5); // Center zoom
    }


    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize the zoom transform
        _zoomTransform = (ScaleTransform)ZoomContainer.LayoutTransform;

        // Alternative if above doesn't work:
        // _zoomTransform = this.FindName("ZoomTransform") as ScaleTransform;
        // or create new if null:
        // _zoomTransform = new ScaleTransform();
        // ZoomContainer.LayoutTransform = _zoomTransform;

        // Set initial zoom
        UpdateZoom();
    }

    private void UpdateZoom()
    {
        if (_zoomTransform == null) return; // Safety check

        double zoomValue = ZoomSlider.Value;
        _zoomTransform.ScaleX = zoomValue / 100;
        _zoomTransform.ScaleY = zoomValue / 100;
        ZoomValueText.Text = $"{zoomValue}%";

        // Adjust stroke thickness when zooming
        if (Canvas != null && Canvas.DefaultDrawingAttributes != null)
        {
            Canvas.DefaultDrawingAttributes.Width = 2 * (100 / zoomValue);
            Canvas.DefaultDrawingAttributes.Height = 2 * (100 / zoomValue);
        }
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateZoom();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        ZoomSlider.Value = Math.Min(ZoomSlider.Value + 25, ZoomSlider.Maximum);
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        ZoomSlider.Value = Math.Max(ZoomSlider.Value - 25, ZoomSlider.Minimum);
    }

    //flip

    private void FlipHorizontal_Click(object sender, RoutedEventArgs e)
    {
        // Toggle horizontal flip
        FlipTransform.ScaleX *= -1;
        FlipTransform.CenterX = Canvas.ActualWidth / 2; // Flip around center
    }

    private void FlipVertical_Click(object sender, RoutedEventArgs e)
    {
        // Toggle vertical flip
        FlipTransform.ScaleY *= -1;
        FlipTransform.CenterY = Canvas.ActualHeight / 2; // Flip around center
    }

    //Rotate

    private void Rotate90_Click(object sender, RoutedEventArgs e)
    {
        RotateTransform.Angle += 90; // Rotate clockwise by 90°
        AdjustScrollViewer(); // Fix scroll position after rotation
    }
    private void RotateRight90_Click(object sender, RoutedEventArgs e)
    {
        RotateTransform.Angle += 90; // Rotate clockwise by 90°
        AdjustScrollViewer(); // Fix scroll position after rotation
    }

    private void RotateLeft90_Click(object sender, RoutedEventArgs e)
    {
        RotateTransform.Angle += -90; // Rotate clockwise by 90°
        AdjustScrollViewer(); // Fix scroll position after rotation
    }

    private void Rotate180_Click(object sender, RoutedEventArgs e)
    {
        RotateTransform.Angle += 180; // Rotate by 180°
        AdjustScrollViewer();
    }
    private void AdjustScrollViewer()
    {
        // Reset scroll to ensure content is visible
        ScrollViewer.ScrollToHorizontalOffset(0);
        ScrollViewer.ScrollToVerticalOffset(0);
    }

    // Update rotation center when canvas size changes
    private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RotateTransform.CenterX = Canvas.ActualWidth / 2;
        RotateTransform.CenterY = Canvas.ActualHeight / 2;
    }

    //Undo and redo

    private readonly Stack<StrokeCollection> _undoStack = new Stack<StrokeCollection>();
    private readonly Stack<StrokeCollection> _redoStack = new Stack<StrokeCollection>();
    private StrokeCollection _currentStrokes = new StrokeCollection();
    private bool _isProcessingAction = false;

    private void Canvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        SaveState();
    }

    private void Strokes_Changed(object sender, StrokeCollectionChangedEventArgs e)
    {
        // This handles both additions and deletions
        if (!_isProcessingAction && (e.Added.Count > 0 || e.Removed.Count > 0))
        {
            SaveState();
        }
    }

    private void SaveState()
    {
        _undoStack.Push(new StrokeCollection(Canvas.Strokes));
        _redoStack.Clear();
        UpdateButtonStates();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count > 1) // Keep at least one state (the empty one)
        {
            _isProcessingAction = true;
            try
            {
                _redoStack.Push(_undoStack.Pop());
                Canvas.Strokes = new StrokeCollection(_undoStack.Peek());
            }
            finally
            {
                _isProcessingAction = false;
                UpdateButtonStates();
            }
        }
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {

        if (_redoStack.Count > 0)
        {
            _isProcessingAction = true;
            try
            {
                var state = _redoStack.Pop();
                _undoStack.Push(new StrokeCollection(state));
                Canvas.Strokes = new StrokeCollection(state);
            }
            finally
            {
                _isProcessingAction = false;
                UpdateButtonStates();
            }
        }
    }

    private void UpdateButtonStates()
    {
        // Undo availability
        bool canUndo = _undoStack.Count > 1;
        UndoButton.IsEnabled = canUndo;
        UndoButton.Opacity = canUndo ? 1.0 : 0.4;

        // Redo availability
        bool canRedo = _redoStack.Count > 0;
        RedoButton.IsEnabled = canRedo;
        RedoButton.Opacity = canRedo ? 1.0 : 0.4;
    }

    //import to canvas

    /*private void ImportImageBtn_Click(object sender, RoutedEventArgs e)
    {
        // Create OpenFileDialog
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
            Title = "Select an Image File"
        };

        // Show dialog
        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                // Load the image
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(openFileDialog.FileName);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                // Set the image as canvas background
                CanvasBackgroundImage.Source = bitmap;

                // Adjust canvas size to match image
                Canvas.Width = bitmap.PixelWidth;
                Canvas.Height = bitmap.PixelHeight;

                // Center the image in the view
                CenterContent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading image: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }*/

    /*private void ImportImageBtn_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog();
        openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";

        if (openFileDialog.ShowDialog() == true)
        {
            
            BitmapImage bitmap = new BitmapImage(new Uri(openFileDialog.FileName));
            Canvas.Background = new ImageBrush(bitmap);
            CenterContent();
        }
    }

    private void CenterContent()
    {
        // Wait for layout to update
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (CanvasBackgroundImage.Source != null)
            {
                // Calculate center position
                double xOffset = (ScrollViewer.ViewportWidth - Canvas.ActualWidth) / 2;
                double yOffset = (ScrollViewer.ViewportHeight - Canvas.ActualHeight) / 2;

                // Apply scroll position
                ScrollViewer.ScrollToHorizontalOffset(Math.Max(0, xOffset));
                ScrollViewer.ScrollToVerticalOffset(Math.Max(0, yOffset));
            }
        }), System.Windows.Threading.DispatcherPriority.Render);
    }*/

    private void ImportImageBtn_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog();
        openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                // Load the image efficiently
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(openFileDialog.FileName);
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // Load immediately
                bitmap.EndInit();

                // Create an ImageBrush with proper stretching
                ImageBrush brush = new ImageBrush(bitmap)
                {
                    Stretch = Stretch.Uniform, // Maintain aspect ratio while fitting
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                // Set as canvas background
                Canvas.Background = brush;

                // Adjust canvas size to match image dimensions
                Canvas.Width = bitmap.PixelWidth;
                Canvas.Height = bitmap.PixelHeight;

                // Center the content in the scroll viewer
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    CenterContent();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading image: {ex.Message}", "Error",
                             MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CenterContent()
    {
        if (Canvas.Background is ImageBrush brush && brush.ImageSource is BitmapImage bitmap)
        {
            // Calculate proper offsets to center
            double xOffset = (ScrollViewer.ViewportWidth - Canvas.ActualWidth * ZoomTransform.ScaleX) / 2;
            double yOffset = (ScrollViewer.ViewportHeight - Canvas.ActualHeight * ZoomTransform.ScaleY) / 2;

            // Apply scroll position
            ScrollViewer.ScrollToHorizontalOffset(Math.Max(0, xOffset));
            ScrollViewer.ScrollToVerticalOffset(Math.Max(0, yOffset));
        }
    }

    //opening file
    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Ink Files (*.ink)|*.ink|Images (*.png,*.jpg)|*.png;*.jpg|All Files|*.*",
            Title = "Open Drawing"
        };

        if (dialog.ShowDialog() == true)
        {
            bool success = OpenFile(dialog.FileName);
            if (success)
            {
                // Update recent files list, etc.
                // AddToRecentFiles(dialog.FileName);
            }
        }
    }
    public bool OpenFile(string filePath)
    {
        // 1. Validate path
        if (string.IsNullOrWhiteSpace(filePath))
        {
            System.Windows.MessageBox.Show("No file specified", "Error",
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // 2. Check file exists
        if (!File.Exists(filePath))
        {
            System.Windows.MessageBox.Show($"File not found: {filePath}", "Error",
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // 3. Get extension safely
        string extension;
        try
        {
            extension = System.IO.Path.GetExtension(filePath).ToLower();
        }
        catch (ArgumentException ex)
        {
            System.Windows.MessageBox.Show($"Invalid file path: {ex.Message}", "Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        // 4. Clear canvas safely
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Canvas.Strokes.Clear();

                // Remove all non-stroke children
                var children = Canvas.Children.OfType<UIElement>()
                                    .Where(x => !(x is InkPresenter)).ToList();
                foreach (var child in children)
                {
                    Canvas.Children.Remove(child);
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Canvas clear failed: {ex}");
        }

        // 5. Load based on file type
        try
        {
            switch (extension)
            {
                case ".ink":
                    return LoadInkStrokes(filePath);

                case ".png":
                case ".jpg":
                case ".jpeg":
                    return LoadImageFile(filePath);

                default:
                    System.Windows.MessageBox.Show($"Unsupported file type: {extension}", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error loading {filePath}: {ex.Message}", "Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool LoadInkStrokes(string path)
    {
        try
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var strokes = new StrokeCollection(fs);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Canvas.Strokes = strokes;
                });
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Stroke load error: {ex}");
            return false;
        }
    }

    private bool LoadImageFile(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();

            Application.Current.Dispatcher.Invoke(() =>
            {
                var image = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    Width = bitmap.PixelWidth,
                    Height = bitmap.PixelHeight
                };

                Canvas.Children.Add(image);
                InkCanvas.SetLeft(image, 0);
                InkCanvas.SetTop(image, 0);
            });

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Image load error: {ex}");
            return false;
        }
    }

    //color
    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Background is Brush brush)
        {
            TopColorButton.Background = brush;
            UpdatePenColor();
        }
    }
    private void UpdatePenColor()
    {
        if (TopColorButton.Background is SolidColorBrush brush)
        {
            DrawingAttributes penAttributes = new DrawingAttributes
            {
                Color = brush.Color,
                Width = PencilThickness.Value,
                Height = PencilThickness.Value,
                FitToCurve = true
            };

            Canvas.DefaultDrawingAttributes = penAttributes;
        }
    }

    private void ColorPickerControl_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
    {
        if (Canvas != null && e.NewValue.HasValue)
        {
            Color selectedColor = e.NewValue.Value;

            // Set ink color
            Canvas.DefaultDrawingAttributes.Color = selectedColor;

            // Set BottomColorButton background
            TopColorButton.Background = new SolidColorBrush(selectedColor);
        }
    }
    //fill

    private void PaintBucket_Click(object sender, RoutedEventArgs e)
    {
        // Get the color from TopColorButton
        SolidColorBrush selectedBrush = TopColorButton.Background as SolidColorBrush;

        if (selectedBrush != null)
        {
            // Change the entire canvas background to simulate a fill
            Canvas.Background = selectedBrush;
        }
    }
    private void Canvas_MouseLeftButtonDown2(object sender, MouseButtonEventArgs e)
    {
        // Get the fill color from TopColorButton
        SolidColorBrush selectedBrush = TopColorButton.Background as SolidColorBrush;

        if (selectedBrush != null)
        {
            // Simulate a fill by drawing a filled rectangle at click position
            Point clickPoint = e.GetPosition(Canvas);

            Rectangle fillRect = new Rectangle
            {
                Width = 50, // You can change this to simulate fill size
                Height = 50,
                Fill = selectedBrush,
                Stroke = null
            };

            InkCanvas.SetLeft(fillRect, clickPoint.X - 25); // Center it around click
            InkCanvas.SetTop(fillRect, clickPoint.Y - 25);

            Canvas.Children.Add(fillRect);
        }
    }
    //color picker
    private bool isColorPickerActive = false;

    private void EnableColorPicker_Click(object sender, RoutedEventArgs e)
    {
        isColorPickerActive = true;
        Canvas.EditingMode = InkCanvasEditingMode.None;  // Disable drawing
    }

    private async void Canvas_MouseLeftButtonDown3(object sender, MouseButtonEventArgs e)
    {
        if (isColorPickerActive)
        {
            Point position = e.GetPosition(Canvas);

            // Asynchronous call to run the color picking logic on the UI thread
            await Dispatcher.InvokeAsync(() =>
            {
                Canvas.Measure(new Size(Canvas.ActualWidth, Canvas.ActualHeight));
                Canvas.Arrange(new Rect(new Size(Canvas.ActualWidth, Canvas.ActualHeight)));
                Canvas.UpdateLayout();

                // Capture the current content of the Canvas
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    (int)Canvas.ActualWidth, (int)Canvas.ActualHeight,
                    96d, 96d,
                    PixelFormats.Default);
                rtb.Render(Canvas);

                // Pick the color at the point of the mouse click
                CroppedBitmap cb = new CroppedBitmap(rtb, new Int32Rect((int)position.X, (int)position.Y, 1, 1));
                byte[] pixels = new byte[4];
                cb.CopyPixels(pixels, 4, 0);
                Color pickedColor = Color.FromArgb(255, pixels[2], pixels[1], pixels[0]);

                // Update the TopColorButton background to the picked color
                TopColorButton.Background = new SolidColorBrush(pickedColor);

                // Update the pen or highlighter color based on the current drawing mode
                if (_isHighlighterMode)
                {
                    // Update highlighter color
                    _highlighterAttributes.Color = Color.FromArgb(150, pickedColor.R, pickedColor.G, pickedColor.B);
                    Canvas.DefaultDrawingAttributes = _highlighterAttributes;
                }
                else
                {
                    // Update normal pen color
                    _normalPenAttributes.Color = pickedColor;
                    Canvas.DefaultDrawingAttributes = _normalPenAttributes;
                }

                // Deactivate the color picker and return to drawing mode
                isColorPickerActive = false;
                Canvas.EditingMode = InkCanvasEditingMode.Ink;
            });
        }
    }

    //new file
    private void NewFileButton_Click(object sender, RoutedEventArgs e)
    {
        // Check if current canvas has unsaved changes
        if (Canvas.Strokes.Count > 0 || Canvas.Children.Count > 0)
        {
            var result = System.Windows.MessageBox.Show("Current drawing has unsaved changes. Create new file anyway?",
                                       "Warning",
                                       MessageBoxButton.YesNo,
                                       MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return; // User canceled
            }
        }

        CreateNewFile();
    }

    public void CreateNewFile()
    {
        try
        {
            // Clear the canvas on UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                Canvas.Strokes.Clear();

                // Remove all non-stroke elements
                var elementsToRemove = Canvas.Children
                    .OfType<UIElement>()
                    .Where(x => !(x is InkPresenter))
                    .ToList();

                foreach (var element in elementsToRemove)
                {
                    Canvas.Children.Remove(element);
                }

                // Reset canvas properties if needed
                Canvas.DefaultDrawingAttributes.Color = Colors.Black;
                Canvas.DefaultDrawingAttributes.Width = 2;
                Canvas.DefaultDrawingAttributes.Height = 2;

                // Update window title
                this.Title = "Paint App - New Drawing";

                // Reset zoom/pan if you have those features
                // ResetZoomAndPan();
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error creating new file: {ex.Message}", "Error",
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    //brush
    private bool _isHighlighterMode = false;
    private readonly DrawingAttributes _highlighterAttributes = new DrawingAttributes
    {
        Color =   Color.FromArgb(150, 255, 255, 0), // Semi-transparent yellow
        Width = 15,
        Height = 5,  // Flat tip shape
        StylusTip = StylusTip.Rectangle,
        FitToCurve = true
    };

    private readonly DrawingAttributes _normalPenAttributes = new DrawingAttributes
    {
        Color = Colors.Black,
        Width = 2,
        Height = 2,
        StylusTip = StylusTip.Ellipse
    };     
    

    private void HighlighterButton_Click(object sender, RoutedEventArgs e)
    {
        BrushPopup.IsOpen = false;
        _isHighlighterMode = !_isHighlighterMode;

        Canvas.DefaultDrawingAttributes = _isHighlighterMode
            ? _highlighterAttributes
            : _normalPenAttributes;

        HighlighterButton.Background = _isHighlighterMode
            ? Brushes.LightGreen
            : Brushes.LightYellow;
    }

    private void UpdateHighlighterColor(Color baseColor)
    {
        _highlighterAttributes.Color = Color.FromArgb(150, baseColor.R, baseColor.G, baseColor.B);
    }


    // Optional: Change highlighter color
    private void ChangeHighlighterColor(Color color)
    {
        _highlighterAttributes.Color = color;
        if (_isHighlighterMode)
        {
            Canvas.DefaultDrawingAttributes = _highlighterAttributes;
        }
    }

    //shapes

    private string currentShape = "";  // Keeps track of the selected shape
    private Shape tempShape;          // Temporary shape being drawn
    private Point startPoint;         // Starting point of the shape

    // Button Click Handlers
    private void RectangleButton_Click(object sender, RoutedEventArgs e)
    {
        currentShape = "Rectangle";
        Canvas.EditingMode = InkCanvasEditingMode.None;
        Canvas.Cursor = Cursors.Cross; // Optional visual cue
    }

    private void CircleButton_Click(object sender, RoutedEventArgs e)
    {
        currentShape = "Circle";
        Canvas.EditingMode = InkCanvasEditingMode.None;
        Canvas.Cursor = Cursors.Cross;
    }

    private void LineButton_Click(object sender, RoutedEventArgs e)
    {
        currentShape = "Line";
        Canvas.EditingMode = InkCanvasEditingMode.None;
        Canvas.Cursor = Cursors.Cross;
    }

    private void SquareButton_Click(object sender, RoutedEventArgs e)
    {
        currentShape = "Square";
        Canvas.EditingMode = InkCanvasEditingMode.None;
        Canvas.Cursor = Cursors.Cross;
    }

    // Mouse Events for Drawing on the InkCanvas
    private void DrawingCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        startPoint = e.GetPosition(Canvas);

        switch (currentShape)
        {
            case "Rectangle":
                tempShape = new Rectangle
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 2
                };
                break;

            case "Circle":
                tempShape = new Ellipse
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 2
                };
                break;

            case "Line":
                tempShape = new Line
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 2,
                    X1 = startPoint.X,
                    Y1 = startPoint.Y
                };
                break;

            case "Square":
                tempShape = new Rectangle
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 2
                };
                break;
        }

        if (tempShape != null)
        {
            Canvas.Children.Add(tempShape);  // Add the shape to the InkCanvas
        }
    }

    private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (tempShape == null || e.LeftButton != MouseButtonState.Pressed) return;

        var currentPoint = e.GetPosition(Canvas);

        switch (currentShape)
        {
            case "Rectangle":
            case "Square":
                var rect = tempShape as Rectangle;
                var width = Math.Abs(currentPoint.X - startPoint.X);
                var height = currentShape == "Square" ? width : Math.Abs(currentPoint.Y - startPoint.Y);

                InkCanvas.SetLeft(rect, Math.Min(currentPoint.X, startPoint.X));
                InkCanvas.SetTop(rect, Math.Min(currentPoint.Y, startPoint.Y));
                rect.Width = width;
                rect.Height = height;
                break;

            case "Circle":
                var ellipse = tempShape as Ellipse;
                var diameter = Math.Abs(currentPoint.X - startPoint.X);
                InkCanvas.SetLeft(ellipse, Math.Min(currentPoint.X, startPoint.X));
                InkCanvas.SetTop(ellipse, Math.Min(currentPoint.Y, startPoint.Y));
                ellipse.Width = diameter;
                ellipse.Height = diameter;
                break;

            case "Line":
                var line = tempShape as Line;
                line.X2 = currentPoint.X;
                line.Y2 = currentPoint.Y;
                break;
        }
    }

    private void DrawingCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        tempShape = null;  // Finalize the shape
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

}
