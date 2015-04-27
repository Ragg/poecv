using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OEIFormats.FlowCharts;
using OEIFormats.FlowCharts.Conversations;
using OEIFormats.Strings;

namespace PoECV
{
    //Trigger nodes reference a node in another conversation file.
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

    //Represents one node in the conversation tree.
    public class ConversationNodeViewModel
    {
        private readonly bool _isBankChild;
        private readonly FlowChartNode _node;
        private readonly StringTableFile.Entry _text;
        public readonly bool IsMain;

        public ConversationNodeViewModel(FlowChartNode node, StringTableFile.Entry text, bool isMain,
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
        //Display more info when a node is selected, with detailed information in conditional and script calls.
        public string DetailedInfo
        {
            get
            {
                var conditionals = new List<string>();
                GetConditionals(_node.Conditionals, conditionals);
                var condString = string.Join("\n", conditionals);
                var scripts = GetAllScriptCalls();
                var scriptString = string.Join("\n", scripts.Select(s => GetMethodString(s.Data)));
                return string.Format("{0}\n{1}\n{2}", ToString(), condString, scriptString);
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
            return string.Format("{0} {1} {2}", not, method, op);
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

        //String to show in the conversation node tree, with different flags prepended depending on the node type.
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