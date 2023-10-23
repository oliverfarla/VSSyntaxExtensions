using EnvDTE80;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace VSSyntaxExtensions
{
    public class NodeX
    {
        public SyntaxNode Node;
        public bool IsInner;
        public bool? ForceIsInner;

        public NodeX(SyntaxNode node, bool isInner, bool? forceIsInner)
        {
            this.Node = node;
            this.IsInner = isInner;
            this.ForceIsInner = forceIsInner;
        }

        public Span InnerSpan()
        {
            var children = Node.ChildNodes().ToList();
            if (children.Count == 0)
                return OuterSpan();
            var start = children[0].SpanStart;
            var end = children.Last().Span.End;
            return new Span(start, end - start);
        }
        public Span OuterSpan()
        {
            var itemSpan = new Span(Node.SpanStart, Node.Span.End - Node.Span.Start);
            return itemSpan;

        }

        public Span GetSpan()
        {
            if (ForceIsInner == true)
                return InnerSpan();
            if (ForceIsInner == false)
                return OuterSpan();
            if (IsInner)
                return InnerSpan();
            return OuterSpan();
        }

        public NodeX GetParent()
        {
            if (ForceIsInner.HasValue)
            {
                return new NodeX(Node.Parent, ForceIsInner.Value, ForceIsInner);
            }
            if (IsInner && InnerSpan() != OuterSpan())
            {
                return new NodeX(Node, false, ForceIsInner);
            }
            return new NodeX(Node.Parent, true, ForceIsInner);
        }

    }

    public static class Helpers
    {
        public static void OpenDocument(this AsyncPackage package, string path)
        {
            Microsoft.VisualStudio.Shell.VsShellUtilities.OpenDocument(package, path);
        }

        public static void DoWithUndo(AsyncPackage package, Action<IWpfTextView> action)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var serviceProvider = package as System.IServiceProvider;
            var DTE = (EnvDTE.DTE)Package.GetGlobalService(typeof(EnvDTE.DTE));
            try
            {
                DTE.UndoContext.Open("Description of operation");
                Microsoft.VisualStudio.Text.Editor.IWpfTextView textView = serviceProvider.GetTextView();
                action(textView);
            }
            finally
            {
                DTE.UndoContext.Close();
            }
        }

        public static VisualStudioWorkspace GetWorkspace()
        {
            var componentModel = (IComponentModel)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SComponentModel));
            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            return workspace;

        }
        public static IWpfTextView GetTextView(this IServiceProvider serviceProvider)
        {
            IVsTextManager textManager = (IVsTextManager)serviceProvider.GetService(typeof(SVsTextManager));
            textManager.GetActiveView(1, null, out var textView);
            return serviceProvider.GetEditorAdaptersFactoryService().GetWpfTextView(textView);
        }
        public static Microsoft.VisualStudio.Editor.IVsEditorAdaptersFactoryService GetEditorAdaptersFactoryService(this IServiceProvider serviceProvider)
        {
            IComponentModel componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
            return componentModel.GetService<Microsoft.VisualStudio.Editor.IVsEditorAdaptersFactoryService>();
        }


        public static NodeX FindParent(Document document, IWpfTextView textView, NodeX item, Span curr, bool? forceIsInner, List<NodeX> list)
        {
            list.Add(item);
            var itemSpan = item.GetSpan();
            System.Diagnostics.Debug.WriteLine(itemSpan);
            if (itemSpan != curr && itemSpan.Start <= curr.Start && itemSpan.End >= curr.End)
                return item;

            var parent = item.GetParent();
            return FindParent(document, textView, parent, curr, forceIsInner, list);
        }

        public static void SelectNode(IWpfTextView textView, SyntaxNode node, bool includeTrivia, bool includeBraces)
        {
            var sp = includeTrivia ? node.FullSpan.ToSpan() : node.Span.ToSpan();
            if (includeBraces == false)
            {
                if (node is BlockSyntax block && block.CloseBraceToken != null && block.OpenBraceToken != null && block.Statements.Count > 0)
                {
                    var start = block.Statements.First().SpanStart;
                    var end = block.Statements.Last().Span.End;
                    sp = new Span(start, end - start);
                }
                if (node is ParameterListSyntax plist)
                {
                    var start = plist.Parameters.First().SpanStart;
                    var end = plist.Parameters.Last().Span.End;
                    sp = new Span(start, end - start);
                }
                if (node is ArgumentListSyntax argList)
                {
                    var start = argList.Arguments.First().SpanStart;
                    var end = argList.Arguments.Last().Span.End;
                    sp = new Span(start, end - start);
                }
            }
            var sn = new SnapshotSpan(textView.TextSnapshot, sp);
            textView.Caret.MoveTo(new SnapshotPoint(textView.TextSnapshot, sp.End - 1));
            textView.Selection.Clear();
            textView.Selection.Select(sn, false);
        }

        public static void DoSelection(IWpfTextView textView, bool? forceIsInner, bool moveCaret, bool expand)
        {
            var start = textView.Selection.Start.Position.Position;
            var end = textView.Selection.End.Position.Position;
            var curr = new Span(start, end - start); //not sure why it's not +1


            SnapshotPoint caretPosition = textView.Caret.Position.BufferPosition;

            Document document = caretPosition.Snapshot.GetOpenDocumentInCurrentContextWithChanges();

            //var item = document.GetSyntaxRootAsync().Result.
            //        FindToken(caretPosition);
            //.Parent.AncestorsAndSelf().
            //        OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().
            //        FirstOrDefault();


            var item = document.GetSyntaxRootAsync().Result.
                              FindToken(caretPosition).Parent;
            var node = new NodeX(item, false, forceIsInner);
            var nodes = new List<NodeX>();
            var nodex = FindParent(document, textView, node, curr, forceIsInner, nodes);
            var sp = nodex.GetSpan();
            if (expand == false)
            {
                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    var ss = nodes[i].GetSpan();
                    if (ss.Start > curr.Start || ss.End < curr.End)
                    {
                        sp = ss;
                        break;
                    }
                }
                if (sp == nodex.GetSpan())
                    sp = nodes.First().GetSpan();
            }
            var sn = new SnapshotSpan(textView.TextSnapshot, sp);
            if (moveCaret)
                textView.Caret.MoveTo(new SnapshotPoint(textView.TextSnapshot, sp.End - 1));
            textView.Selection.Clear();
            textView.Selection.Select(sn, false);
        }

        public static (Document document, SyntaxNode parent, List<SyntaxNode> argList, int argumentIndex, int parentCount)? GetParentArgList<TNode, TParentNode>(IWpfTextView textView, Func<TParentNode, TNode, List<SyntaxNode>> getArguments) where TNode : SyntaxNode where TParentNode : SyntaxNode
        {


            SnapshotPoint caretPosition = textView.Caret.Position.BufferPosition;

            Document document = caretPosition.Snapshot.GetOpenDocumentInCurrentContextWithChanges();

            //var item = document.GetSyntaxRootAsync().Result.
            //        FindToken(caretPosition);
            //.Parent.AncestorsAndSelf().
            //        OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().
            //        FirstOrDefault();

            if (!document.TryGetSyntaxRoot(out var root))
                return null;

            var token = root.FindToken(caretPosition);
            var argumentRes = token.FindNodeOfType<TNode, TNode>(x => x);
            if (argumentRes == null)
                return null;
            var parentResult = argumentRes.Value.node.FindNodeOfType<TParentNode, List<SyntaxNode>>(x => getArguments(x, argumentRes.Value.result));
            if (parentResult == null)
                return null;

            var index = parentResult.Value.result.IndexOf(argumentRes.Value.result);
            if (index < 0)
                return null;


            return (document, parentResult.Value.node, parentResult.Value.result, index, parentResult.Value.lst.Count);

        }

        public static SyntaxNode FindParentOfType(this SyntaxNode node, bool includeThis, params Type[] types)
        {
            if (node == null)
                return null;
            if (includeThis)
            {
                foreach (var t in types)
                    if (node.GetType().IsAssignableFrom(t))
                        return node;
            }
            return FindParentOfType(node.Parent, true, types);
        }

        public static (List<SyntaxNode> lst, SyntaxNode node, T result)? FindNodeOfType<TNode, T>(this SyntaxToken node, Func<TNode, T> f) where TNode : SyntaxNode
        {
            return FindNodeOfType<TNode, T>(node.Parent, f);
        }
        public static (List<SyntaxNode> lst, SyntaxNode node, T result)? FindNodeOfType<TNode, T>(this SyntaxNode node, Func<TNode, T> f) where TNode : SyntaxNode
        {
            var lst = new List<SyntaxNode>();

            (List<SyntaxNode> lst, TNode node, T result)? Find()
            {
                if (node == null)
                    return null;
                lst.Add(node);
                if (node is TNode t)
                    return (lst, t, f(t));
                node = node.Parent;
                return Find();

            }
            return Find();
        }

        public static Span ToSpan(this Microsoft.CodeAnalysis.Text.TextSpan span) => new Span(span.Start, span.Length);

        public static (Document document, SyntaxNode parent, List<SyntaxNode> argList, int argumentIndex, int parentCount)? GetBestArgList(IWpfTextView textView)
        {
            var arguments = Helpers.GetParentArgList<ArgumentSyntax, ArgumentListSyntax>(textView, (x, _) => x.Arguments.Cast<SyntaxNode>().ToList());
            var parameters = Helpers.GetParentArgList<ParameterSyntax, ParameterListSyntax>(textView, (x, _) => x.Parameters.Cast<SyntaxNode>().ToList());

            var valid = new[] { arguments, parameters }.Where(x => x != null).ToList();
            if (valid.Count == 0)
                return null;

            var best = valid.OrderBy(x => x.Value.parentCount).First().Value;
            return best;

        }

        public static void MoveParameter(IWpfTextView textView, int offset)
        {

            var best = GetBestArgList(textView);
            if (best == null)
                return;


            var (document, parent, argList, paramIndex, parentCount) = best.Value;

            var newIndex = (paramIndex + offset + argList.Count) % argList.Count;

            if (newIndex == paramIndex)
                return;

            var firstIsSource = Math.Min(paramIndex, newIndex) == paramIndex;
            var first = argList[Math.Min(paramIndex, newIndex)];
            var second = argList[Math.Max(paramIndex, newIndex)];

            var t1 = first.GetText().ToString();
            var t2 = second.GetText().ToString();

            var s1 = first.FullSpan.ToSpan();
            var s2 = second.FullSpan.ToSpan();


            textView.TextBuffer.Replace(s2, t1);
            textView.TextBuffer.Replace(s1, t2);
            if (firstIsSource)
            {
                textView.Caret.MoveTo(new SnapshotPoint(textView.TextSnapshot, s2.End - s1.Length));
            }
            else
            {
                textView.Caret.MoveTo(new SnapshotPoint(textView.TextSnapshot, s1.Start));
            }

        }

        public static DTE2 GetDTE2()
        {

            var dte = Package.GetGlobalService(typeof(Microsoft.VisualStudio.Shell.Interop.SDTE)) as DTE2;
            return dte;
        }

        public static void GoToDocumentLine(this AsyncPackage package, string filePath, int lineNum)
        {
            try
            {
                VsShellUtilities.OpenDocument(package, filePath);
                var DTE = GetDTE2();
                foreach (EnvDTE.Document doc in DTE.Documents)
                {
                    if (doc.FullName.ToLower() == filePath.ToLower())
                    {
                        doc.Activate();
                        (DTE.ActiveDocument.Selection as EnvDTE.TextSelection).GotoLine(lineNum);
                        return;
                    }

                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            //DTE.ActiveDocument.Activate();

            //var line = tt.TextSnapshot.GetLineFromLineNumber(item.LineNum - 1);
            //tt.Caret.MoveTo(line.Start);
            //var span = new SnapshotSpan(tt.TextSnapshot, new Microsoft.VisualStudio.Text.Span(line.Start, line.Length));
            //tt.ViewScroller.EnsureSpanVisible(span);


        }
    }
}
