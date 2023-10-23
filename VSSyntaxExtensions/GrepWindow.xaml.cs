using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace VSSyntaxExtensions
{
    public partial class GrepWindow : DialogWindow
    {
        public GrepWindow()
        {
            InitializeComponent();
            this.Loaded += GrepWindow_Loaded;
        }

        private void GrepWindow_Loaded(object sender, RoutedEventArgs e)
        {
            textbox.Focus();
            //Style style = new Style(typeof(ListBoxItem));
            //style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Green));
            //style.Setters.Add(new Setter(TextBlock.TextProperty, "Green"));
            //style.Triggers.Add(new DataTrigger() { Value = 1, Binding = new Binding("IsSelected") });
            //Resources.Add(typeof(TextBlock), style);

            //list.ItemContainerStyle = style;
            Load();
        }

        public AsyncPackage package;
        public List<string> FilePaths = new List<string>();
        public List<DoGrep.GrepResult> currItems = new List<DoGrep.GrepResult>();

        int idx = 0;


        public void Load()
        {

            if (list.Items.Count > 0)
            {
                idx = (idx + list.Items.Count) % list.Items.Count;
                list.SelectedIndex = idx;
            }
            textbox.Focus();
        }

        List<CancellationTokenSource> currCancles = new List<CancellationTokenSource>();
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var maxResults = 50;
            var str = textbox.Text;
            if (str.Length < 3)
                return;
            foreach (var c in this.currCancles)
                c.Cancel();
            this.currCancles.Clear();
            var cts = new CancellationTokenSource();
            currCancles.Add(cts);
            this.list.Items.Clear();
            currItems.Clear();
            Task.Run(() =>
            {
                DoGrep.DoSearch2(this.FilePaths, str, r =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (cts.Token.IsCancellationRequested)
                            return;
                        if (currItems.Count >= maxResults)
                            return;
                        currItems.Add(r);
                        this.list.Items.Add((r.FilePath + " : " + r.LineNum).PadRight(120) + " --- " + r.Text);
                    });
                }, cts.Token);
                //var results = DoGrep.DoSearch(this.FilePaths, str, 20, cts.Token);
                //Dispatcher.Invoke(() =>
                //{
                //    foreach (var r in results)
                //    {
                //        this.list.Items.Add(r.FileName + " --- " + r.LineNum + " --- " + r.Text);
                //    }
                //});

            });
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
                var item = currItems[idx];
                

                package.GoToDocumentLine(item.FilePath, item.LineNum);

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