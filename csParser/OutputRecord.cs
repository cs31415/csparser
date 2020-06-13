namespace csParser
{
    /// <summary>
    /// Represents a record in the output file
    /// </summary>
    class OutputRecord 
    {
        public string FileName { get;set; }
        public int LineNumber { get;set; }
        public string CommandText { get;set; }
        public bool IsVariable { get;set; }
        public string ErrorMsg { get;set; }

        public OutputRecord(string fileName, int lineNumber, string commandText, bool isVariable, string errorMsg = null)
        {
            FileName = fileName;
            LineNumber = lineNumber;
            CommandText = commandText;
            IsVariable = isVariable;
            ErrorMsg = errorMsg;
        }

        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                // Suitable nullity checks etc, of course :)
                if (this.FileName != null)
                {
                    hash = hash * 23 + this.FileName.GetHashCode();
                }

                hash = hash * 23 + this.LineNumber.GetHashCode();
                if (this.CommandText != null)
                {
                    hash = hash * 23 + this.CommandText.GetHashCode();
                }

                hash = hash * 23 + this.IsVariable.GetHashCode();
                if (this.ErrorMsg != null)
                {
                    hash = hash * 23 + this.ErrorMsg.GetHashCode();
                }

                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            var other = obj as OutputRecord;
            if (other == null)
                return false;
            return (this.FileName == other.FileName || (this.FileName == null && other.FileName == null))
                   && this.LineNumber == other.LineNumber
                   && (this.CommandText == other.CommandText || (this.CommandText == null && other.CommandText == null))
                   && this.IsVariable == other.IsVariable
                   && (this.ErrorMsg == other.ErrorMsg ||
                       (this.ErrorMsg == null && other.ErrorMsg == null));
        }
    }
}
