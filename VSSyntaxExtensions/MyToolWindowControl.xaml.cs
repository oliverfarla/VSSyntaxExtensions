using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace VSSyntaxExtensions
{
    public partial class MyToolWindowControl : DialogWindow
    {
        public MyToolWindowControl()
        {
            InitializeComponent();
            this.Loaded += MyToolWindowControl_Loaded;
        }

        private void MyToolWindowControl_Loaded(object sender, RoutedEventArgs e)
        {
            textbox.Focus();
            Style style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Green));
            style.Setters.Add(new Setter(TextBlock.TextProperty, "Green"));
            style.Triggers.Add(new DataTrigger() { Value = 1, Binding = new Binding("IsSelected") });
            Resources.Add(typeof(TextBlock), style);

            list.ItemContainerStyle = style;
            Load();
        }

        public AsyncPackage package;
        public List<string> FilePaths = new List<string>();

        int idx = 0;


        public void Load()
        {
            if (list == null)
                return;
            var curr = FilePaths.Where(x => x.ToLower().Contains(textbox.Text.ToLower())).ToList();
            list.ItemsSource = curr;
            if (curr.Count > 0)
            {
                idx = (idx + curr.Count) % curr.Count;
                list.SelectedIndex = idx;
            }
            textbox.Focus();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Load();
        }

        private void Grid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                DoItem();
            }
            textbox.Focus();
        }

        private void DoItem()
        {

            if (list.Items.Count > 0)
            {
                var idx = 0;
                if (list.SelectedIndex >= 0 && list.SelectedIndex < list.Items.Count)
                {
                    idx = list.SelectedIndex;
                }
                package.OpenDocument((string)list.Items[idx]);
            }
            this.Close();
        }

        private void list_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DoItem();
        }

        private void Grid_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                this.Close();
            }
            if (e.Key == System.Windows.Input.Key.Down)
            {
                idx += 1;
                Load();
            }
            if (e.Key == System.Windows.Input.Key.Up)
            {
                idx -= 1;
                Load();
            }
            textbox.Focus();

        }
    }
}