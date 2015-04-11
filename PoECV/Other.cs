using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using AlphanumComparator;
using GalaSoft.MvvmLight.CommandWpf;
using JetBrains.Annotations;
using OEIFormats.FlowCharts;
using OEIFormats.FlowCharts.Conversations;
using OEIFormats.Strings;

namespace PoECV
{
    public class SearchResult
    {
        public SearchResult(ConversationFile file, int id)
        {
            File = file;
            Id = id;
        }

        public ConversationFile File { get; private set; }
        public int Id { get; private set; }

        public override string ToString()
        {
            return string.Format("{0} in {1}", Id, File);
        }
    }

    public class ParameterSelection : ObservableObject
    {
        private string _selection;

        public ParameterSelection()
        {
            Parameters = new SortedSet<string>(new AlphanumComparator<string>());
            ClearCommand = new RelayCommand(Clear);
        }

        public RelayCommand ClearCommand { get; private set; }
        public SortedSet<string> Parameters { get; private set; }

        public string Selection
        {
            get { return _selection; }
            set
            {
                if (_selection != value)
                {
                    _selection = value;
                    OnPropertyChanged();
                }
            }
        }

        private void Clear()
        {
            Selection = null;
        }
    }

    public class ConversationFile
    {
        public readonly string Path;

        public ConversationFile(string path)
        {
            Path = path;
        }

        public override string ToString()
        {
            return Path.Substring(88, Path.Length - 88 - 13);
        }
    }

    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public class TriggerNode
    {
        public TriggerNode(ConversationFile file, int id)
        {
            File = file;
            Id = id;
        }

        public ConversationFile File { get; private set; }
        public int Id { get; private set; }
    }

    public static class GuidLookup
    {
        public static Dictionary<string, string> Lookup = new Dictionary<string, string>();

        static GuidLookup()
        {
            var list = new Dictionary<string, string>
            {
                {"Edér", "b1a7e800-0000-0000-0000-000000000000"},
                {"Durance", "b1a7e801-0000-0000-0000-000000000000"},
                {"Aloth", "b1a7e803-0000-0000-0000-000000000000"},
                {"Kana Rua", "b1a7e804-0000-0000-0000-000000000000"},
                {"Sagani", "b1a7e805-0000-0000-0000-000000000000"},
                {"Pallegina", "b1a7e806-0000-0000-0000-000000000000"},
                {"Grieving Mother", "b1a7e807-0000-0000-0000-000000000000"},
                {"Hiravias", "b1a7e808-0000-0000-0000-000000000000"},
                {"Calisca", "b1a7e809-0000-0000-0000-000000000000"},
                {"Heodan", "b1a7e810-0000-0000-0000-000000000000"}
            };
            foreach (var entry in list)
            {
                Lookup[entry.Key] = entry.Value;
                Lookup[entry.Value] = entry.Key;
            }
        }
    }

    public class NodeEntry
    {
        private readonly bool _isBankChild;
        private readonly FlowChartNode _node;
        private readonly StringTableFile.Entry _text;
        public readonly bool IsMain;

        public NodeEntry(FlowChartNode node, StringTableFile.Entry text, bool isMain,
            bool isBankChild)
        {
            _node = node;
            _text = text;
            IsMain = isMain;
            _isBankChild = isBankChild;
            var triggerNode = _node as TriggerConversationNode;
            if (triggerNode != null)
            {
                var separators = new[] {Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar};
                var convoPath = string.Join(@"\",
                    triggerNode.ConversationFilename.Split(separators).Skip(1));
                var convoSearch =
                    ConversationsViewModel.ConversationFiles.Where(
                        f => f.Path.IndexOf(convoPath, StringComparison.OrdinalIgnoreCase) >= 0);
                Debug.Assert(convoSearch.Count() == 1);
                var convo = convoSearch.First();
                var id = triggerNode.StartNodeID;
                Trigger = new TriggerNode(convo, id);
            }
        }

        public TriggerNode Trigger { get; private set; }

        public string DetailedInfo
        {
            get
            {
                var conditionals = new List<string>();
                GetConditionals(_node.Conditionals, conditionals);
                var condString = string.Join("\n", conditionals);
                var scripts = GetAllScriptCalls();
                var scriptString = string.Join("\n", scripts.Select(s => GetMethodString(s.Data)));
                return String.Format("{0}\n{1}\n{2}", ToString(), condString, scriptString);
            }
        }

        public int ID
        {
            get { return _node.NodeID; }
        }

        private static string BuildConditionalString(ConditionalCall call)
        {
            var not = call.Not ? "NOT" : "   ";
            var method = GetMethodString(call.Data);
            var op = call.Operator.ToString();
            return String.Format("{0} {1} {2}", not, method, op);
        }

        private static string GetMethodString(ScriptCallData data)
        {
            var method = data.FullName.Split('(')[0].Split()[1];
            var args = string.Join(", ", data.Parameters);
            return string.Join(" ", method, args);
        }

        private static void GetConditionals(ExpressionComponent component,
            ICollection<string> exprList)
        {
            var call = component as ConditionalCall;
            if (call != null)
            {
                exprList.Add(BuildConditionalString(call));
                return;
            }
            var expression = component as ConditionalExpression;
            if (expression != null)
            {
                foreach (var expressionComponent in expression.Components)
                {
                    GetConditionals(expressionComponent, exprList);
                }
            }
        }

        private IEnumerable<ScriptCallData> GetAllConditionalCalls()
        {
            var l = new List<ScriptCallData>();
            _node.Conditionals.GetAllScriptCalls(l);
            return l;
        }

        private IEnumerable<ScriptCall> GetAllScriptCalls()
        {
            return _node.OnEnterScripts.Concat(_node.OnUpdateScripts).Concat(_node.OnExitScripts);
        }

        public override string ToString()
        {
            if (_node.IsRootNode || _node is BankNode)
            {
                return _node.ToString();
            }
            if (Trigger != null)
            {
                return string.Format("Trigger Node {0} in {1}", Trigger.Id, Trigger.File);
            }
            var id = _node is PlayerResponseNode ? "P" + _node.NodeID : _node.NodeID.ToString();
            var description = _text == null ? id + " - Empty" : _text.ToString();
            var flags = "";
            if (_node is PlayerResponseNode)
            {
                flags += "P";
            }
            if (GetAllConditionalCalls().Any())
            {
                flags += "C";
            }
            if (_node.HasScripts)
            {
                flags += "S";
            }
            if (_isBankChild)
            {
                flags += "B";
            }
            else if (!_node.Links.Any())
            {
                flags += "E";
            }
            return string.IsNullOrEmpty(flags)
                ? description
                : string.Format("[{0}] {1}", flags, description);
        }
    }
}