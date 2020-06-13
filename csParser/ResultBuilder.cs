using System.Text;

namespace csParser
{
    /// <summary>
    /// Build the parse results table
    /// </summary>
    class ResultBuilder
    {
        readonly StringBuilder _sbResults;

        public string ResultsText => _sbResults.ToString();

        public ResultBuilder()
        {
            _sbResults = new StringBuilder();
            _sbResults.AppendLine($"File,LineNumber,CommandText,IsVariable,ErrorMsg");
        }

        public void AppendResults(OutputRecord outRec)
        {
            // write body
            var cleanProc = outRec
                .CommandText?
                .Replace("[", "")
                .Replace("]", "")
                .Replace("dbo.", "");

            bool referencedMethods = cleanProc != null && cleanProc.StartsWith(ParseHelper.ReferencedMethodPrefix);
            var procs = 
                referencedMethods ? 
                new [] { cleanProc } :
                cleanProc?.Replace(ParseHelper.ReferencedMethodPrefix,"").Split('|');
            if (procs != null)
            {
                foreach (var proc in procs)
                {
                    _sbResults.AppendLine($"{outRec.FileName},{outRec.LineNumber},{proc},{outRec.IsVariable},");
                }
            }
        }
    }
}
