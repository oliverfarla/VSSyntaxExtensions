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
using System.Windows.Input;
using System.Windows.Media;

namespace VSSyntaxExtensions
{
    public partial class GrepWindow : DialogWindow
    {
        public AsyncPackage package;
        public List<string> FilePaths = new List<string>();
        private List<DoGrep.GrepResult> currItems = new List<DoGrep.GrepResult>();
        private int currIndex = 0;
        private List<CancellationTokenSource> currTasks = new List<CancellationTokenSource>();
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
            reload();
        }




        public void reload(int offset = 0)
        {
            currIndex += offset;

            if (list.Items.Count > 0)
            {
                currIndex = (currIndex + list.Items.Count) % list.Items.Count;
                list.SelectedIndex = currIndex;
            }
            textbox.Focus();
        }

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
            foreach (var c in this.currTasks)
                c.Cancel();

            this.currTasks.Clear();
            this.list.Items.Clear();
            this.currItems.Clear();

            var cts = new CancellationTokenSource();
            this.currTasks.Add(cts);
            _ = Task.Run(() =>
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



        private void ChooseCurrItem()
        {

            reload();
            if (list.Items.Count > 0)
            {
                var item = currItems[currIndex];
                package.GoToDocumentLine(item.FilePath, item.LineNum);
            }
            this.Close();
        }

        private void Dofzf()
        {
            var tmpFile = System.IO.Path.GetTempFileName() + ".txt";
            System.IO.File.WriteAllLines(tmpFile, FilePaths);
            var result = DoGrep.DoFZF(tmpFile, textbox.Text);
            var item = DoGrep.GrepResult.Create(result);
            System.IO.File.Delete(tmpFile);
            if (item == null)
                return;
            package.GoToDocumentLine(item.FilePath, item.LineNum);
            this.Close();


        }
        private void list_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ChooseCurrItem();
        }


        //the key up/down stuff is pretty hacky, enter has to be keydown because of how the command is launched, and using key down for arrows doesn't seem to work with the textbox focus
        private void Grid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl))
                    Dofzf();
                else
                    ChooseCurrItem();

            }
            textbox.Focus();
        }
        private void Grid_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
                this.Close();
            if (e.Key == System.Windows.Input.Key.Down)
                reload(+1);
            if (e.Key == System.Windows.Input.Key.Up)
                reload(-1);

            textbox.Focus();

        }
    }
}