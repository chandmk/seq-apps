using System.IO;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.Ontime
{
    class TextTemplateToken : TemplateToken
    {
        readonly string _text;

        public TextTemplateToken(string text)
        {
            _text = text;
        }

        public override void Render(TextWriter output, Event<LogEventData> evt)
        {
            output.Write(_text);
        }
    }
}