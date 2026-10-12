using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GTIP
{
    // Engine-independent transaction layer. Validate the entire batch before writes.
    public sealed class LiveEditField
    {
        public string Label;
        public Func<object> Read;
        public Action<object> Write;
        public Func<string, object> Parse;
        public Func<object, string> Format = value => Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    public sealed class LiveEditSession
    {
        sealed class Change
        {
            internal LiveEditField Field;
            internal object Before, After;
        }
        readonly Stack<Change[]> undo = new Stack<Change[]>(), redo = new Stack<Change[]>();
        readonly Action guard, refresh;
        public int UndoCount => undo.Count;
        public int RedoCount => redo.Count;
        public LiveEditSession(Action guard, Action refresh) { this.guard = guard; this.refresh = refresh; }

        public void Apply(IReadOnlyList<LiveEditField> fields, IReadOnlyList<string> text)
        {
            guard();
            if (fields.Count != text.Count) throw new ArgumentException("Field count changed; reopen the selection.");
            var changes = fields.Select((f, i) => { var before = f.Read(); return new Change { Field = f, Before = before, After = text[i] == f.Format(before) ? before : f.Parse(text[i]) }; })
                .Where(c => !Equals(c.Before, c.After)).ToArray();
            if (changes.Length == 0) return;
            Write(changes, false);
            undo.Push(changes); redo.Clear();
        }
        public void Undo()
        {
            guard(); if (undo.Count == 0) return;
            var changes = undo.Peek(); CheckUnchanged(changes, true); Write(changes, true);
            undo.Pop(); redo.Push(changes);
        }
        public void Redo()
        {
            guard(); if (redo.Count == 0) return;
            var changes = redo.Peek(); CheckUnchanged(changes, false); Write(changes, false);
            redo.Pop(); undo.Push(changes);
        }
        static void CheckUnchanged(Change[] changes, bool expectAfter)
        {
            if (changes.Any(c => !Equals(c.Field.Read(), expectAfter ? c.After : c.Before)))
                throw new InvalidOperationException("The game changed this record; refresh before editing it again.");
        }
        void Write(Change[] changes, bool backwards)
        {
            int attempted = 0;
            try
            {
                for (int i = 0; i < changes.Length; i++)
                { attempted = i + 1; changes[i].Field.Write(backwards ? changes[i].Before : changes[i].After); }
                refresh();
            }
            catch (Exception original)
            {
                var failures = new List<Exception> { original };
                for (int i = attempted - 1; i >= 0; i--)
                    try { changes[i].Field.Write(backwards ? changes[i].After : changes[i].Before); }
                    catch (Exception error) { failures.Add(error); }
                try { refresh(); } catch (Exception error) { failures.Add(error); }
                if (failures.Count > 1) throw new AggregateException("An edit could not be fully restored. Reload your last save.", failures);
                throw;
            }
        }
        public void Clear() { undo.Clear(); redo.Clear(); }

        public static object Name(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 120 || text.Any(char.IsControl))
                throw new ArgumentException("Names must contain 1–120 characters without line breaks or control characters.");
            return text.Trim();
        }
        public static float Number(string text, float minimum, float maximum, float scale = 1)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
                float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentException("Enter a number from " + minimum + " to " + maximum + ".");
            return value / scale;
        }
    }
}
