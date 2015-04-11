using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using GalaSoft.MvvmLight.CommandWpf;
using OEIFormats.FlowCharts;
using OEIFormats.FlowCharts.Conversations;
using OEIFormats.Strings;

namespace PoECV
{
    public class ConversationsViewModel : ObservableObject
    {
        private readonly ICollectionView _conversationsView;
        private string _conversationsFilter = "";
        private ConversationFile _selectedFile;
        private string _selectedMethod;
        private NodeEntry _selectedNode;
        private SearchResult _selectedResult;

        static ConversationsViewModel()
        {
            ConversationFiles =
                new ObservableCollection<ConversationFile>(
                    DirSearch(
                        @"D:\steam\steamapps\common\Pillars of Eternity\PillarsOfEternity_Data\data\conversations")
                        .Select(c => new ConversationFile(c)));
        }

        public ConversationsViewModel()
        {
            ClearParametersCommand = new RelayCommand(ClearParameters);
            SearchCommand = new RelayCommand(SearchMethod);
            _conversationsView = CollectionViewSource.GetDefaultView(ConversationFiles);
            _conversationsView.Filter = o => o.ToString().Contains(_conversationsFilter);
            var calls =
                ConversationFiles.Select(f => ConversationData.Load(f.Path))
                    .SelectMany(c => c.Nodes)
                    .SelectMany(node =>
                    {
                        var scriptCalls = new List<ScriptCallData>();
                        node.Conditionals.GetAllScriptCalls(scriptCalls);
                        return scriptCalls;
                    });
            ConditionalMethods = new Dictionary<string, List<ParameterSelection>>();
            CollectionViewSource.GetDefaultView(ConditionalMethods.Keys)
                .SortDescriptions.Add(new SortDescription("", ListSortDirection.Ascending));
            foreach (var call in calls)
            {
                if (!ConditionalMethods.ContainsKey(call.FullName))
                {
                    ConditionalMethods[call.FullName] = new List<ParameterSelection>();
                }
                var paramList = ConditionalMethods[call.FullName];
                for (var i = 0; i < call.Parameters.Count; i++)
                {
                    if (paramList.Count <= i)
                    {
                        paramList.Add(new ParameterSelection());
                    }
                    string value;
                    var item = call.Parameters[i];
                    if (GuidLookup.Lookup.TryGetValue(item, out value))
                    {
                        paramList[i].Parameters.Add(value);
                    }
                    else
                    {
                        paramList[i].Parameters.Add(item);
                    }
                }
            }
        }

        public RelayCommand ClearParametersCommand { get; private set; }
        public RelayCommand SearchCommand { get; private set; }
        public Dictionary<string, List<ParameterSelection>> ConditionalMethods { get; private set; }
        public string NodeInfo { get; private set; }
        public ObservableCollection<TreeViewItem> Nodes { get; private set; }

        public NodeEntry SelectedNode
        {
            set
            {
                if (_selectedNode == value)
                {
                    return;
                }
                _selectedNode = value;
                NodeInfo = _selectedNode.DetailedInfo;
                OnPropertyChanged("NodeInfo");
            }
        }

        public static ObservableCollection<ConversationFile> ConversationFiles { get; private set; }

        public ConversationFile SelectedFile
        {
            get { return _selectedFile; }
            set
            {
                if (_selectedFile == value)
                {
                    return;
                }
                _selectedFile = value;
                var dir = value.Path;
                var data = ConversationData.Load(dir);
                var text =
                    StringTableFile.Load(
                        dir.Replace(@"\conversations\", @"\localized\en\text\conversations\")
                            .Replace(".conversation", ".stringtable"));
                var item = AddLinks(0, data, text);
                Nodes = new ObservableCollection<TreeViewItem> {item};
                OnPropertyChanged("Nodes");
                OnPropertyChanged();
            }
        }

        public string ConversationsFilter
        {
            get { return _conversationsFilter; }
            set
            {
                if (_conversationsFilter == value)
                {
                    return;
                }
                _conversationsFilter = value;
                _conversationsView.Refresh();
            }
        }

        public string SelectedMethod
        {
            get { return _selectedMethod; }
            set
            {
                if (_selectedMethod != value)
                {
                    _selectedMethod = value;
                    ParameterList = ConditionalMethods[_selectedMethod];
                    OnPropertyChanged("ParameterList");
                }
            }
        }

        public List<ParameterSelection> ParameterList { get; set; }
        public List<SearchResult> SearchResults { get; private set; }

        public SearchResult SelectedResult
        {
            get { return _selectedResult; }
            set
            {
                if (_selectedResult != value)
                {
                    _selectedResult = value;
                    SelectedFile = _selectedResult.File;
                    SelectNode(_selectedResult.Id, Nodes.First());
                }
            }
        }

        private void ClearParameters()
        {
            foreach (var parameterSelection in ParameterList)
            {
                parameterSelection.Selection = null;
            }
        }

        private void SearchMethod()
        {
            if (SelectedMethod == null)
            {
                return;
            }
            var parameters = ParameterList;
            var results = new List<SearchResult>();
            foreach (var file in ConversationFiles)
            {
                foreach (var node in ConversationData.Load(file.Path).Nodes)
                {
                    foreach (var conds in node.Conditionals.Components)
                    {
                        var conditionals = new List<ScriptCallData>();
                        conds.GetAllScriptCalls(conditionals);
                        foreach (var call in
                            conditionals.Where(c => c.FullName == SelectedMethod)
                                .Select(c => c.Parameters))
                        {
                            var flag = true;
                            for (var i = 0; i < call.Count; i++)
                            {
                                var selection = parameters[i].Selection;
                                if (selection != null)
                                {
                                    string value;
                                    if (GuidLookup.Lookup.TryGetValue(selection, out value))
                                    {
                                        flag = flag && value == call[i];
                                    }
                                    else
                                    {
                                        flag = flag && selection == call[i];
                                    }
                                    if (!flag)
                                    {
                                        break;
                                    }
                                }
                            }
                            if (flag)
                            {
                                results.Add(new SearchResult(file, node.NodeID));
                            }
                        }
                    }
                }
            }
            SearchResults = results;
            OnPropertyChanged("SearchResults");
        }

        private TreeViewItem AddLinks(int nodeId, ConversationData data, StringTableFile text,
            bool isBankChild = false, HashSet<int> addedNodes = null)
        {
            if (addedNodes == null)
            {
                addedNodes = new HashSet<int>();
            }
            var node = data.GetNodeByID(nodeId);
            var nodeText = text.Entries.FirstOrDefault(t => t.ID == nodeId);
            var wasAlreadyAdded = addedNodes.Contains(nodeId);
            var nodeEntry = new NodeEntry(node, nodeText, !wasAlreadyAdded, isBankChild);
            var item = new TreeViewItem {Header = nodeEntry};
            if (nodeEntry.Trigger != null)
            {
                item.KeyDown += HandleTrigger;
                return item;
            }
            if (wasAlreadyAdded)
            {
                item.FontStyle = FontStyles.Italic;
                item.KeyDown += HandleJump;
                return item;
            }
            addedNodes.Add(nodeId);
            var bank = node as BankNode;
            if (bank != null)
            {
                foreach (var child in bank.ChildNodeIDs)
                {
                    item.Items.Add(AddLinks(child, data, text, true, addedNodes));
                }
            }
            foreach (var link in node.Links)
            {
                item.Items.Add(AddLinks(link.ToNodeID, data, text, false, addedNodes));
            }
            return item;
        }

        private void HandleTrigger(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space)
            {
                return;
            }
            e.Handled = true;
            var item = (TreeViewItem) sender;
            var entry = (NodeEntry) item.Header;
            SelectedFile = entry.Trigger.File;
            SelectNode(entry.Trigger.Id, Nodes.First());
        }

        private void HandleJump(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space)
            {
                return;
            }
            e.Handled = true;
            var item = (TreeViewItem) sender;
            var entry = (NodeEntry) item.Header;
            SelectNode(entry.ID, Nodes.First());
        }

        private void SelectNode(int node, TreeViewItem item)
        {
            foreach (var treeViewItem in item.Items)
            {
                var treeItem = (TreeViewItem) treeViewItem;
                var entry = (NodeEntry) treeItem.Header;
                if (entry.ID == node && entry.IsMain)
                {
                    ExpandParentNodes(treeItem);
                    treeItem.IsSelected = true;
                }
                else
                {
                    SelectNode(node, treeItem);
                }
            }
        }

        private void ExpandParentNodes(TreeViewItem node)
        {
            var parent = node.Parent as TreeViewItem;
            if (parent != null)
            {
                parent.IsExpanded = true;
                ExpandParentNodes(parent);
            }
        }

        private static IEnumerable<string> DirSearch(string dir)
        {
            foreach (var f in Directory.GetFiles(dir))
            {
                yield return f;
            }
            foreach (var ff in Directory.GetDirectories(dir).SelectMany(DirSearch))
            {
                yield return ff;
            }
        }
    }
}