using System;
using System.Text;

namespace Jenson
{
    public class SourceWriter
    {
        private readonly StringBuilder _stringBuilder;
        private string _indentationString = "    ";

        public bool IsNewline { get; private set; } = false;
        public int Indentation { get; private set; }


        public SourceWriter()
        {
            _stringBuilder = new StringBuilder();
        }

        public void Indent() => Indentation++;
        public void Dedent() => Indentation = Math.Max(0, Indentation - 1);

        public void Write(string s) 
        {
            HandleIndent();
            _stringBuilder.Append(s);
            IsNewline = false;
        }

        public void Line()
        {
            _stringBuilder.AppendLine(string.Empty);
            IsNewline = true;
        }

        public void Line(string s) 
        {
            if (s.Contains(Environment.NewLine) || s.Contains("\n"))
            {
                foreach (var l in s.Split(new [] { Environment.NewLine, "\n" }, StringSplitOptions.None))
                {
                    HandleIndent();
                    _stringBuilder.AppendLine(l);
                    IsNewline = true;
                }
            }
            else
            {
                HandleIndent();
                _stringBuilder.AppendLine(s);
                IsNewline = true;
            }
        }

        public override string ToString() => _stringBuilder.ToString();

        private void HandleIndent()
        {
            if (IsNewline)
            {
                for (var i = 0; i < Indentation; i++)
                    _stringBuilder.Append(_indentationString);
            }
        }
    }
}
