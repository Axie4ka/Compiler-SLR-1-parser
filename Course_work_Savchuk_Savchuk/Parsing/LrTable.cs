using System.Collections.Generic;

namespace Translator.Parsing
{
    public enum ActionKind
    {
        Shift,
        Reduce,
        Accept
    }

    // Value: номер состояния (Shift) или номер правила (Reduce)
    public record LrAction(ActionKind Kind, int Value);

    public class LrTable
    {
        public required Grammar Grammar { get; set; }

        // Action[состояние][терминал]
        public List<Dictionary<string, LrAction>> Action { get; } =
            new List<Dictionary<string, LrAction>>();

        // Goto[состояние][нетерминал]
        public List<Dictionary<string, int>> Goto { get; } =
            new List<Dictionary<string, int>>();

        // Конфликты, найденные при построении, и принятые решения
        public List<string> Conflicts { get; } = new List<string>();

        public int StateCount
        {
            get { return Action.Count; }
        }
    }
}
