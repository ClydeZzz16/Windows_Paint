using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Wpf_Paint_Lab8_Egmilan;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    

    private void Button_Click_1(object sender, RoutedEventArgs e)
    {

    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {

    }

    private void Button_Click_2(object sender, RoutedEventArgs e)
    {

    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Circle button clicked!");
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
}